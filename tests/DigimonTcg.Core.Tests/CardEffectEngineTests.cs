using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>카드 스크립트(Engine/Effects) 기반 효과 처리 — 플레이어 · AI가 같은 스크립트를 쓰는지</summary>
public class CardEffectEngineTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3, TurnSequence = 3 };
        return (state, new GoldfishEngine(state), Cards());
    }

    /// <summary>실전 AI 대전의 내 메인 페이즈 — PassTurn하면 AI가 메모리 +3으로 턴을 시작한다</summary>
    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewAiBattle()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        for (int i = 0; i < 2; i++)
            state.Player.SecurityStack.Add(cards["ST2-02"]);
        for (int i = 0; i < 3; i++)
        {
            state.Player.MainDeck.Add(cards["ST2-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST1-16"]);   // AI 드로우용 (옵션은 AI가 내지 않는다)
        }
        return (state, engine, cards);
    }

    // ===== 스크립트 정의 =====

    [Fact]
    public void CardScripts_ShouldOnlyReferenceBuiltInCards()
    {
        var cards = Cards();
        Assert.All(CardScripts.All, s => Assert.True(cards.ContainsKey(s.CardId), $"{s.CardId}는 내장 카드가 아닙니다"));
    }

    [Fact]
    public void BuiltInCardsWithEffectText_ShouldHaveScriptsOrBeListed()
    {
        // 카드 데이터 수치만으로 동작 (InheritedDpBonus · InheritedSecurityAttackBonus · IsBlocker)
        var dataOnly = new HashSet<string> { "ST1-03", "ST1-07", "BT1-015", "ST5-03" };
        // 아직 문장만 있는 효과 (테이머 레스트 상태 · 메인 페이즈 액티브 시점이 필요) — docs/card_effect_engine_plan.md
        var textOnly = new HashSet<string> { "ST4-14", "ST5-14", "ST6-14", "BT2-021", "BT2-032" };

        var missing = Cards().Values
            .Where(c => (c.EffectText + c.InheritedEffectText).Trim().Length > 0)
            .Where(c => CardScripts.Get(c.CardId) == null && !dataOnly.Contains(c.CardId) && !textOnly.Contains(c.CardId))
            .Select(c => $"{c.CardId} {c.Name}")
            .ToList();

        Assert.True(missing.Count == 0, "스크립트가 없는 효과 카드: " + string.Join(", ", missing));
    }

    [Fact]
    public void OptionTargetKinds_ShouldComeFromScripts()
    {
        var cards = Cards();
        Assert.Equal(TargetKind.OpponentDigimon, CardScripts.MainTargetOf(cards["ST1-16"]));
        Assert.Equal(TargetKind.OwnDigimon, CardScripts.MainTargetOf(cards["ST1-13"]));
        Assert.Equal(TargetKind.OwnDigimonWithDigimonSource, CardScripts.MainTargetOf(cards["ST2-15"]));
        Assert.Equal(TargetKind.None, CardScripts.MainTargetOf(cards["ST2-13"]));
    }

    [Fact]
    public void Omegamon_ShouldEvolveFromTheColorsOnItsCard()
    {
        var cards = Cards();
        var redLv6 = new CardInstance(cards.Values.First(c => c.Type == CardType.Digimon && c.Level == 6 && c.Color == CardColor.Red), 1);
        var blackLv6 = new CardInstance(cards.Values.First(c => c.Type == CardType.Digimon && c.Level == 6 && c.Color == CardColor.Black), 1);

        Assert.True(ActionValidator.ValidateEvolution(redLv6, cards["BT1-084"]).CanExecute);    // 레드 · 블루
        Assert.False(ActionValidator.ValidateEvolution(blackLv6, cards["BT1-084"]).CanExecute);
        Assert.True(ActionValidator.ValidateEvolution(blackLv6, cards["BT3-112"]).CanExecute);  // 레드 · 블랙
    }

    // ===== AI도 같은 카드 스크립트를 쓴다 =====

    [Fact]
    public void AiEvolution_ShouldTriggerWhenDigivolvingEffect()
    {
        var (state, engine, cards) = NewAiBattle();
        var greymon = new CardInstance(cards["ST1-07"], currentTurn: 1);
        state.DummyOpponent.BattleArea.Add(greymon);
        state.DummyOpponent.Hand.Add(cards["ST1-08"]);   // 가루다몬 (진화 코스트 3): [진화 시] 이 턴 동안 DP +3000

        engine.PassTurn();

        Assert.Equal("ST1-08", greymon.TopCard.CardId);
        Assert.Contains(state.ActionLogs, l => l.Contains("🤖 [진화 시] 가루다몬"));
        Assert.Contains(state.OpponentTurnEvents, e => e.Kind == OpponentActionKind.Effect && e.Card?.CardId == "ST1-08");
    }

    [Fact]
    public void AiAttack_ShouldTriggerWhenAttackingEffect()
    {
        var (state, engine, cards) = NewAiBattle();
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST3-11"], currentTurn: 1));   // 세라피몬: [어택 시] 상대 디지몬 1마리 DP -4000
        var myPiyomon = new CardInstance(cards["ST1-02"], currentTurn: 1);                       // DP 3000
        state.Player.BattleArea.Add(myPiyomon);

        engine.PassTurn();

        Assert.DoesNotContain(myPiyomon, state.Player.BattleArea);
        Assert.Contains(cards["ST1-02"], state.Player.Trash);
    }

    [Fact]
    public void AiPiercing_AfterBeatingMyBlocker_ShouldCheckMySecurity()
    {
        var (state, engine, cards) = NewAiBattle();
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST4-13"], currentTurn: 1));   // 헤라클레스캅테리몬 ≪관통≫ DP 12000
        var myBlocker = new CardInstance(cards["ST1-06"], currentTurn: 1);                       // 코어드라몬 ≪블로커≫ DP 6000
        state.Player.BattleArea.Add(myBlocker);

        engine.PassTurn();   // 테스트는 가장 강한 블로커로 자동 블록

        Assert.DoesNotContain(myBlocker, state.Player.BattleArea);
        Assert.Single(state.Player.SecurityStack);   // 블로커를 쓰러뜨린 뒤 시큐리티 1장 체크
    }

    [Fact]
    public void AiWarGreymon_ShouldNegateMySecurityOptionEffect()
    {
        var (state, engine, cards) = NewAiBattle();
        var warGreymon = new CardInstance(cards["BT1-025"], currentTurn: 1);   // 체크한 옵션 카드의 [시큐리티] 효과는 발휘하지 않는다
        state.DummyOpponent.BattleArea.Add(warGreymon);
        state.Player.SecurityStack.Insert(0, cards["ST1-16"]);                // 가이아 포스 [시큐리티]: 어택 중인 디지몬 소멸

        engine.PassTurn();

        Assert.Contains(warGreymon, state.DummyOpponent.BattleArea);
        Assert.Contains(state.ActionLogs, l => l.Contains("무효"));
    }

    // ===== [자신의 턴] 조건 · 배틀 중 DP =====

    [Fact]
    public void Patamon_Inherited_ShouldGainMemoryWhenOpponentDigimonHitsZeroDpOnMyTurn()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 7;
        var angemon = new CardInstance(cards["ST3-04"], currentTurn: 1);   // 파닥몬 진화원
        angemon.Evolve(cards["ST3-05"]);
        state.Player.BattleArea.Add(angemon);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-08"], currentTurn: 1));   // DP 7000
        state.Player.Hand.Add(cards["ST3-16"]);

        engine.PlayCard(cards["ST3-16"]);   // 세븐 헤븐즈 (코스트 7): DP -10000 → 소멸

        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Equal(1, state.MemoryGauge);   // 7 - 7 + 1
    }

    [Fact]
    public void Patamon_Inherited_ShouldNotTriggerOnOpponentsTurn()
    {
        var (state, engine, cards) = NewAiBattle();
        var angemon = new CardInstance(cards["ST3-04"], currentTurn: 1);
        angemon.Evolve(cards["ST3-05"]);
        state.Player.BattleArea.Add(angemon);
        var aiAttacker = new CardInstance(cards["ST1-05"], currentTurn: 1);   // DP 5000
        state.DummyOpponent.BattleArea.Add(aiAttacker);
        state.Player.SecurityStack.Insert(0, cards["ST3-16"]);               // 세븐 헤븐즈 [시큐리티]: 어택 중인 디지몬 DP -10000

        engine.PassTurn();

        Assert.DoesNotContain(aiAttacker, state.DummyOpponent.BattleArea);
        Assert.DoesNotContain(state.ActionLogs, l => l.Contains("파닥몬: 상대 디지몬이 DP 0으로 소멸"));
    }

    [Fact]
    public void Hornmon_Inherited_ShouldAddDpOnlyWhileBattlingDigimonWithoutSources()
    {
        var (state, engine, cards) = NewMainPhase();
        var gabumon = new CardInstance(cards["ST2-01"], currentTurn: 1);   // 뿔몬 진화원
        gabumon.Evolve(cards["ST2-02"]);                                    // 쉬라몬 DP 3000
        state.Player.BattleArea.Add(gabumon);
        var enemy = new CardInstance(cards["ST1-02"], currentTurn: 1) { IsSuspended = true };   // DP 3000, 진화원 없음
        state.DummyOpponent.BattleArea.Add(enemy);

        Assert.Equal(3000, gabumon.GetCurrentDP());   // 배틀 밖에서는 그대로
        engine.AttackOpponentDigimon(gabumon, enemy);

        Assert.Contains(gabumon, state.Player.BattleArea);       // 4000 vs 3000 — 동귀어진이 아니라 승리
        Assert.Empty(state.DummyOpponent.BattleArea);
    }
}
