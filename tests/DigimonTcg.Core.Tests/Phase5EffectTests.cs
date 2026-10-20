using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과 확장 5단계 — [메인] 기동 효과 · ≪디지버스트≫ · 지속 효과(다른 디지몬에게) · 새 발동 시점 · 비용 · 진화 코스트</summary>
public class Phase5EffectTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    /// <summary>API 카드 (카드 번호는 테스트마다 달라야 한다 — 자동 스크립트가 번호로 캐시된다)</summary>
    private static CardData Api(string id, int level, int dp, string main = "", string inherited = "",
        CardColor color = CardColor.Red, CardType type = CardType.Digimon, int playCost = 3, int evoCost = 1)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = playCost, EvoCost = evoCost,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main, InheritedEffectText = inherited,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3, TurnSequence = 3 };
        return (state, new GoldfishEngine(state), Cards());
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewAiBattle()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        for (int i = 0; i < 3; i++)
        {
            state.Player.SecurityStack.Add(cards["ST2-02"]);
            state.Player.MainDeck.Add(cards["ST2-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST1-16"]);
        }
        return (state, engine, cards);
    }

    [Fact]
    public void TamerMainAbility_ShouldPayCostAndOnlyBeUsableOncePerTurn()
    {
        var (state, engine, _) = NewMainPhase();
        var tamer = Api("P5-TAMER-MAIN", 0, 0, main: "[Main] By suspending this Tamer, gain 1 memory.", type: CardType.Tamer);
        state.Player.Tamers.Add(tamer);

        Assert.Contains(engine.GetMainAbilities(state.Player), a => a.Card == tamer);
        Assert.True(engine.ActivateMainAbility(null, tamer).CanExecute);

        Assert.Equal(4, state.MemoryGauge);
        Assert.False(state.Player.HasActiveTamer(tamer));
        Assert.False(engine.ActivateMainAbility(null, tamer).CanExecute);   // 이번 턴에 이미 썼다
    }

    [Fact]
    public void TamerMainAbility_ShouldBeUsableOncePerCopyAndNotCountWhenTheCostIsNotPaid()
    {
        var (state, engine, _) = NewMainPhase();
        var tamer = Api("P5-TAMER-TWO", 0, 0, main: "[Main] By suspending this Tamer, gain 1 memory.", type: CardType.Tamer);
        state.Player.Tamers.Add(tamer);
        state.Player.Tamers.Add(tamer);   // 같은 카드 2장 (같은 CardData 참조)

        Assert.True(engine.ActivateMainAbility(null, tamer).CanExecute);
        Assert.True(engine.ActivateMainAbility(null, tamer).CanExecute);
        Assert.Equal(5, state.MemoryGauge);

        // 이미 둘 다 레스트 — 비용을 낼 수 없어 아무것도 하지 않으면 사용으로 치지 않는다 (장수만큼 이미 썼으므로 목록에서는 빠진다)
        Assert.DoesNotContain(engine.GetMainAbilities(state.Player), a => a.Card == tamer);
    }

    [Fact]
    public void MainAbilityThatDidNothing_ShouldStayAvailable()
    {
        var (state, engine, _) = NewMainPhase();
        var tamer = Api("P5-TAMER-NOPAY", 0, 0, main: "[Main] By suspending this Tamer, gain 1 memory.", type: CardType.Tamer);
        state.Player.Tamers.Add(tamer);
        state.Player.SuspendedTamers.Add(tamer);   // 이미 레스트 — 비용을 낼 수 없다

        engine.ActivateMainAbility(null, tamer);

        Assert.Equal(3, state.MemoryGauge);
        Assert.Contains(engine.GetMainAbilities(state.Player), a => a.Card == tamer);
    }

    [Fact]
    public void Digisorption_ShouldNotReduceBelowTheSpecialEvolutionCost()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 0;
        var baseCard = Api("P5-DSS-BASE", 3, 3000, color: CardColor.Yellow);
        baseCard.Name = "Shoutmon";
        var baseDigimon = new CardInstance(baseCard, 1);
        var other = new CardInstance(cards["ST1-02"], 1);
        state.Player.BattleArea.AddRange(new[] { baseDigimon, other });
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var evo = Api("P5-DSS", 6, 12000, evoCost: 5,
            main: "＜Digisorption -3＞\nYour [Shoutmon] can digivolve into this card in your hand for a memory cost of 1, ignoring this card's digivolution requirements.");
        state.Player.Hand.Add(evo);

        Assert.True(engine.EvolveDigimon(baseDigimon, evo).CanExecute);

        Assert.True(other.IsSuspended);
        Assert.Equal(0, state.MemoryGauge);   // 코스트 1 - 1 (3이 아니라 실제 코스트만큼만 줄인다)
    }

    [Fact]
    public void DigiBurst_ShouldTrashSourcesRunTheEffectAndTriggerTheTrashedCard()
    {
        var (state, engine, _) = NewMainPhase();
        var returner = Api("P5-BURST-SRC", 3, 3000,
            inherited: "[Your Turn] When this card is trashed due to activating this Digimon's ＜Digi-Burst＞, return this card to its owner's hand.");
        var filler = Api("P5-BURST-FILL", 4, 5000);
        var burster = Api("P5-BURST", 5, 7000,
            main: "[Main] ＜Digi-Burst 1＞ (You may trash 1 of this Digimon's digivolution cards to activate the effect below.)\n・Gain 2 memory.");
        var inst = new CardInstance(returner, 1);
        inst.Evolve(filler);
        inst.Evolve(burster);
        state.Player.BattleArea.Add(inst);

        Assert.True(engine.ActivateMainAbility(inst, burster).CanExecute);

        Assert.Equal(5, state.MemoryGauge);
        Assert.Single(inst.EvolutionSources);           // 맨 아래 1장 파기
        Assert.Contains(returner, state.Player.Hand);   // 트래시된 진화원의 효과로 패에
        Assert.DoesNotContain(returner, state.Player.Trash);
    }

    [Fact]
    public void KeywordAura_ShouldGiveTheKeywordToOtherDigimon()
    {
        var (state, engine, cards) = NewMainPhase();
        var ally = new CardInstance(cards["ST1-02"], 1);
        state.Player.BattleArea.Add(ally);
        var giver = Api("P5-AURA", 4, 5000, main: "[Your Turn] All of your Digimon gain ＜Piercing＞.");
        state.Player.Hand.Add(giver);

        engine.PlayCard(giver);

        Assert.Contains(Keyword.Piercing, ally.ActiveKeywords);
    }

    [Fact]
    public void TwicePerTurnOptionTrigger_ShouldResolveOnlyTwice()
    {
        var (state, engine, cards) = NewMainPhase();
        var watcher = Api("P5-TWICE", 4, 5000, main: "[Your Turn] [Twice Per Turn] When you use an Option card, gain 1 memory.");
        state.Player.BattleArea.Add(new CardInstance(watcher, 1));
        var option = Api("P5-TWICE-OPT", 0, 0, main: "[Main] ＜Draw 1＞ (Draw 1 card from your deck.)", type: CardType.Option, playCost: 0);
        for (int i = 0; i < 3; i++)
        {
            state.Player.Hand.Add(option);
            state.Player.MainDeck.Add(cards["ST1-02"]);
        }

        for (int i = 0; i < 3; i++)
            Assert.True(engine.PlayCard(option).CanExecute);

        Assert.Equal(5, state.MemoryGauge);   // 3 + 1 + 1 (세 번째는 발동하지 않음)
    }

    [Fact]
    public void DoesNotUnsuspend_ShouldKeepTheOpponentDigimonSuspendedThroughItsUnsuspendPhase()
    {
        var (state, engine, cards) = NewAiBattle();
        var enemy = new CardInstance(cards["ST1-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        var locker = Api("P5-LOCK", 4, 5000,
            main: "[On Play] Suspend 1 of your opponent's Digimon. It doesn't unsuspend during your opponent's next unsuspend phase.");
        state.Player.Hand.Add(locker);

        engine.PlayCard(locker);
        Assert.True(enemy.IsSuspended);
        engine.PassTurn();

        Assert.True(enemy.IsSuspended);   // 상대의 액티브 페이즈를 지나도 레스트 상태
    }

    [Fact]
    public void DiaboromonToken_ShouldBePlayed()
    {
        var (state, engine, _) = NewMainPhase();
        var maker = Api("P5-TOKEN", 4, 5000, main: "[On Play] You may play 1 [Diaboromon] Token without paying the cost. (Digimon/Cost 14/Lv.6/White/Mega/Unknown/Unidentified/3000 DP)");
        state.Player.Hand.Add(maker);

        engine.PlayCard(maker);

        var token = Assert.Single(state.Player.BattleArea, d => d.TopCard.Name == "Diaboromon Token");
        Assert.Equal(3000, token.GetCurrentDP());
        Assert.StartsWith("TOKEN-", token.TopCard.CardId);
    }

    [Fact]
    public void InsteadSentence_ShouldReplaceThePreviousActionWhenTheConditionHolds()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.Tamers.Add(Api("P5-RED-TAMER", 0, 0, type: CardType.Tamer));
        var weak = new CardInstance(cards["ST1-02"], 1);    // DP 3000
        var strong = new CardInstance(cards["ST2-08"], 1);  // DP 7000
        state.DummyOpponent.BattleArea.AddRange(new[] { weak, strong });
        var killer = Api("P5-INSTEAD", 4, 5000,
            main: "[On Play] Delete 1 of your opponent's Digimon with 3000 DP or less. If you have a red Tamer in play, delete 1 of your opponent's Digimon with 7000 DP or less instead.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.DoesNotContain(strong, state.DummyOpponent.BattleArea);   // 대신 처리 (7000 이하 중 DP가 가장 높은 것)
        Assert.Contains(weak, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void Digisorption_ShouldSuspendAnotherDigimonToReduceTheCost()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 2;
        var baseDigimon = new CardInstance(Api("P5-DS-BASE", 3, 3000), 1);
        var other = new CardInstance(cards["ST1-02"], 1);
        state.Player.BattleArea.AddRange(new[] { baseDigimon, other });
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var evo = Api("P5-DS", 4, 6000, evoCost: 3,
            main: "＜Digisorption -2＞ (When one of your Digimon would digivolve into this card from your hand, you may suspend 1 of your Digimon to reduce the memory cost of the digivolution by 2.)");
        state.Player.Hand.Add(evo);

        Assert.True(engine.EvolveDigimon(baseDigimon, evo).CanExecute);

        Assert.True(other.IsSuspended);
        Assert.Equal(1, state.MemoryGauge);   // 2 - (3 - 2)
    }

    [Fact]
    public void SpecialEvolution_ShouldIgnoreRequirementsAndUseTheWrittenCost()
    {
        var (state, engine, cards) = NewMainPhase();
        var shoutCard = Api("P5-SHOUT", 3, 3000, color: CardColor.Yellow);
        shoutCard.Name = "Shoutmon";
        var shoutmon = new CardInstance(shoutCard, 1);
        state.Player.BattleArea.Add(shoutmon);
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var evo = Api("P5-SPECIAL", 6, 12000, evoCost: 5,
            main: "Your [Shoutmon] can digivolve into this card in your hand for a memory cost of 1, ignoring this card's digivolution requirements.");
        state.Player.Hand.Add(evo);

        Assert.True(engine.EvolveDigimon(shoutmon, evo).CanExecute);

        Assert.Equal("P5-SPECIAL", shoutmon.TopCard.CardId);
        Assert.Equal(2, state.MemoryGauge);
    }

    [Fact]
    public void SpecialEvolution_ExactName_ShouldNotMatchLongerNames()
    {
        var (state, engine, cards) = NewMainPhase();
        var snowCard = Api("P5-SNOW", 3, 3000, color: CardColor.Yellow);
        snowCard.Name = "SnowShoutmon";
        var snow = new CardInstance(snowCard, 1);
        state.Player.BattleArea.Add(snow);
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var evo = Api("P5-SPECIAL-X", 6, 12000, evoCost: 5,
            main: "Your [Shoutmon] can digivolve into this card in your hand for a memory cost of 1, ignoring this card's digivolution requirements.");
        state.Player.Hand.Add(evo);

        // "[Shoutmon]"은 카드명이 정확히 Shoutmon인 디지몬만 — 색 · Lv 조건도 안 맞으므로 진화할 수 없다
        Assert.False(engine.EvolveDigimon(snow, evo).CanExecute);
        Assert.Equal("P5-SNOW", snow.TopCard.CardId);
    }

    [Fact]
    public void AllyAttackEvent_ShouldBuffThatDigimon()
    {
        var (state, engine, cards) = NewMainPhase();
        var tamer = Api("P5-ATK-TAMER", 0, 0, type: CardType.Tamer, color: CardColor.Blue,
            main: "[Your Turn] When one of your blue Digimon attacks, you may suspend this Tamer to give that Digimon +1000 DP for the turn.");
        state.Player.Tamers.Add(tamer);
        var attacker = new CardInstance(Api("P5-ATK", 4, 5000, color: CardColor.Blue), 1);
        state.Player.BattleArea.Add(attacker);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);   // DP 3000 시큐리티 디지몬

        Assert.True(engine.AttackSecurityOrDirect(attacker).CanExecute);

        Assert.False(state.Player.HasActiveTamer(tamer));
        Assert.Equal(6000, attacker.GetCurrentDP());
    }
}
