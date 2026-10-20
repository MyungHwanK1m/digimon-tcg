using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// 한국 공식 사이트 카드 Q&A(digimoncard.co.kr/qna)를 엔진 시나리오로 확인한다.
/// 저작권 원칙: Q&A 문장은 옮기지 않고 카드 번호 · Q 번호 · 기대 결과와 직접 쓴 짧은 요약만 남긴다.
/// 엔진이 공식 답과 다른 항목은 Skip 사유에 남기고 엔진 담당 세션에 전달한다.
/// </summary>
public partial class OfficialQnaTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase(int memory = 5)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = memory, TurnSequence = 3 };
        var cards = Cards();
        for (int i = 0; i < 10; i++)
        {
            state.Player.MainDeck.Add(cards["ST1-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST2-02"]);
        }
        return (state, new GoldfishEngine(state), cards);
    }

    /// <summary>levels 순서대로 진화시킨 디지몬 (첫 장이 맨 아래)</summary>
    private static CardInstance Stack(Dictionary<string, CardData> cards, int turnPlayed, params string[] ids)
    {
        var inst = new CardInstance(cards[ids[0]], turnPlayed);
        foreach (var id in ids.Skip(1))
            inst.Evolve(cards[id]);
        return inst;
    }

    // ===== 스타트 덱 가이아 레드 [STK-1] =====

    /// <summary>ST1-16 Q1 — 가이아 포스로 소멸한 디지몬의 진화원도 트래시로 간다</summary>
    [Fact]
    public void Qna_ST1_16_Q1_DeletedDigimonSourcesGoToTrash()
    {
        var (state, engine, cards) = NewMainPhase(memory: 8);
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-03"], 1));            // 레드 (옵션 색 조건)
        var enemy = Stack(cards, 1, "ST2-03", "ST2-06");
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST1-16"]);

        engine.PlayCard(cards["ST1-16"], enemy);

        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Contains(cards["ST2-06"], state.DummyOpponent.Trash);
        Assert.Contains(cards["ST2-03"], state.DummyOpponent.Trash);
    }

    /// <summary>ST1-15 Q1 — 기가 디스트로이어로 소멸한 디지몬의 진화원도 트래시로 간다</summary>
    [Fact]
    public void Qna_ST1_15_Q1_DeletedDigimonSourcesGoToTrash()
    {
        var (state, engine, cards) = NewMainPhase(memory: 8);
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-03"], 1));
        var enemy = Stack(cards, 1, "ST2-02", "ST2-03");                              // DP 2000 (4000 이하)
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST1-15"]);

        engine.PlayCard(cards["ST1-15"], enemy);

        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Contains(cards["ST2-02"], state.DummyOpponent.Trash);
        Assert.Contains(cards["ST2-03"], state.DummyOpponent.Trash);
    }

    /// <summary>ST1-13 Q1 — 섀도 윙 [시큐리티]의 시큐리티 어택 +1은 다음 자신의 턴에 새로 등장한 디지몬에게도 적용된다</summary>
    [Fact]
    public void Qna_ST1_13_Q1_SecurityBuffAppliesToDigimonPlayedNextTurn()
    {
        var (state, engine, cards) = NewMainPhase(memory: 0);
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.Player.SecurityStack.Add(cards["ST1-13"]);                               // 맨 위: 섀도 윙
        state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-02"], 0));     // 상대 턴에 어택할 디지몬
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);

        engine.PassTurn();                                                             // 상대 턴: 어택 → 섀도 윙 [시큐리티]
        Assert.False(state.IsGameOver);
        Assert.DoesNotContain(cards["ST1-13"], state.Player.SecurityStack);

        // 다음 자신의 턴: 새로 등장시킨 디지몬
        if (state.CurrentPhase == Phase.Breeding)
            engine.PassBreedingPhase();
        state.MemoryGauge = 5;
        state.Player.Hand.Add(cards["ST1-02"]);
        engine.PlayCard(cards["ST1-02"]);
        var fresh = state.Player.BattleArea.Last();
        engine.RecalculateFieldBuffs();

        Assert.Equal(2, fresh.GetSecurityAttackCount());
    }

    /// <summary>ST1-12 Q1 — 신태일이 여러 장이면 DP 보너스가 겹친다</summary>
    [Fact]
    public void Qna_ST1_12_Q1_MultipleTaiStack()
    {
        var (state, engine, cards) = NewMainPhase();
        var piyo = new CardInstance(cards["ST1-02"], 1);                               // DP 3000
        state.Player.BattleArea.Add(piyo);
        state.Player.Tamers.Add(cards["ST1-12"]);
        state.Player.Tamers.Add(cards["ST1-12"]);

        engine.RecalculateFieldBuffs();

        Assert.Equal(5000, piyo.GetCurrentDP());
    }

    /// <summary>ST1-11 Q1 — 진화원 3장이면 2장 단위로만 세어 시큐리티 어택 +1만 얻는다</summary>
    [Fact]
    public void Qna_ST1_11_Q1_ThreeSourcesGiveOnlyOneBonus()
    {
        var (state, engine, cards) = NewMainPhase();
        var war = Stack(cards, 1, "ST1-03", "ST1-07", "ST1-09", "ST1-11");             // 진화원 3장 (아구몬 · 그레이몬 · 메탈그레이몬)
        state.Player.BattleArea.Add(war);

        engine.RecalculateFieldBuffs();

        // 기본 1 + 워그레이몬 효과 +1 + 그레이몬 진화원 ≪시큐리티 어택 +1≫ = 3
        Assert.Equal(3, war.GetSecurityAttackCount());
    }

    /// <summary>ST1-09 Q1 — 상대 디지몬에게 어택해도 블록당하지 않으면 메모리를 얻지 않는다</summary>
    [Fact]
    public void Qna_ST1_09_Q1_NoMemoryWhenAttackingDigimonWithoutBlock()
    {
        var (state, engine, cards) = NewMainPhase(memory: 3);
        var attacker = Stack(cards, 1, "ST1-03", "ST1-07", "ST1-09", "BT1-020");       // 메탈그레이몬(ST1-09)을 진화원으로 가짐
        state.Player.BattleArea.Add(attacker);
        var target = new CardInstance(cards["ST2-02"], 1) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(target);

        engine.AttackOpponentDigimon(attacker, target);

        Assert.Equal(3, state.MemoryGauge);
    }

    /// <summary>ST1-09 Q1 — 어택 대상과 관계없이 블록당하면 메모리 +3</summary>
    [Fact]
    public void Qna_ST1_09_Q1_GainMemoryWhenBlocked()
    {
        var (state, engine, cards) = NewMainPhase(memory: 3);
        var attacker = Stack(cards, 1, "ST1-03", "ST1-07", "ST1-09", "BT1-020");
        state.Player.BattleArea.Add(attacker);
        var target = new CardInstance(cards["ST2-02"], 1) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(target);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-07"], 1));     // 액티브 ≪블로커≫
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);

        engine.AttackOpponentDigimon(attacker, target);

        Assert.Equal(6, state.MemoryGauge);
    }

    /// <summary>ST1-08 Q1 — 가루다몬의 [진화 시] DP +3000은 자기 자신에게도 줄 수 있다</summary>
    [Fact]
    public void Qna_ST1_08_Q1_CanTargetItself()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = Stack(cards, 1, "ST1-03", "ST1-07");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST1-08"]);

        engine.EvolveDigimon(digi, cards["ST1-08"]);

        Assert.Equal("ST1-08", digi.TopCard.CardId);
        // 가루다몬 7000 + 자기 자신에게 준 [진화 시] +3000 + 아구몬 진화원 +1000
        Assert.Equal(cards["ST1-08"].DP + 3000 + 1000, digi.GetCurrentDP());
    }

    /// <summary>ST1-06 Q1 — 메모리가 2 미만이어도 어택할 수 있고, 어택이 끝날 때까지 턴이 넘어가지 않는다</summary>
    [Fact]
    public void Qna_ST1_06_Q1_CanAttackWithLowMemoryAndTurnEndsAfterAttack()
    {
        var (state, engine, cards) = NewMainPhase(memory: 1);
        var core = new CardInstance(cards["ST1-06"], 1);                               // ≪블로커≫ [어택 시] 메모리 -2, DP 6000
        state.Player.BattleArea.Add(core);
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);                        // DP 3000
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);

        var result = engine.AttackSecurityOrDirect(core);

        Assert.True(result.CanExecute);
        Assert.Single(state.DummyOpponent.SecurityStack);                              // 체크까지 끝난 뒤
        Assert.Equal(3, state.CurrentTurn);                                            // 턴이 넘어감
    }

    /// <summary>ST1-01 Q1 — 코로몬 진화원 효과의 "진화원 4장"에는 코로몬 자신도 포함된다</summary>
    [Fact]
    public void Qna_ST1_01_Q1_KoromonCountsItself()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST1-01", "ST1-03", "ST1-07", "ST1-09", "ST1-11"); // 진화원 4장 (코로몬 포함)
        state.Player.BattleArea.Add(digi);

        engine.RecalculateFieldBuffs();

        // 워그레이몬 DP 12000 + 코로몬 +1000 + 아구몬 진화원 +1000 (그레이몬 · 메탈그레이몬 진화원은 DP 효과 없음)
        Assert.Equal(cards["ST1-11"].DP + 2000, digi.GetCurrentDP());
    }
}
