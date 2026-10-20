using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과 확장 3단계 — 문장 파서가 API 카드(영어 문장)의 효과를 실제로 동작시키는지</summary>
public class SentenceParserTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    /// <summary>API 카드 (카드 번호는 테스트마다 달라야 한다 — 자동 스크립트가 번호로 캐시된다)</summary>
    private static CardData Api(string id, int level, int dp, string main = "", string inherited = "",
        CardColor color = CardColor.Red, CardType type = CardType.Digimon, int playCost = 3, int evoCost = 1, params string[] traits)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = playCost, EvoCost = evoCost,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main, InheritedEffectText = inherited, Traits = traits.ToList(),
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
    public void Analyze_ShouldMarkParsedSentencesSupported()
    {
        var card = Api("SP-ANALYZE", 4, 5000,
            main: "[On Play] Delete 1 of your opponent's Digimon with 4000 DP or less.",
            inherited: "[Your Turn] This Digimon gets +1000 DP.");
        var clauses = KeywordDictionary.Analyze(card);
        Assert.All(clauses, c => Assert.Equal(ClauseSupport.Supported, c.Support));
        Assert.All(clauses, c => Assert.Equal("문장", c.Note));
    }

    [Fact]
    public void StaticYourTurnDp_ShouldApplyOnlyOnOwnersTurn()
    {
        var (state, engine, cards) = NewMainPhase();
        var host = new CardInstance(Api("SP-STATIC", 3, 3000, inherited: "[Your Turn] This Digimon gets +2000 DP."), 1);
        host.Evolve(cards["ST1-07"]);   // 그레이몬 DP 4000 (진화원: SP-STATIC)
        state.Player.BattleArea.Add(host);

        engine.RecalculateFieldBuffs();
        Assert.Equal(6000, host.GetCurrentDP());
        host.IsOwnersTurn = false;
        engine.RecalculateFieldBuffs();
        Assert.Equal(4000, host.GetCurrentDP());
    }

    [Fact]
    public void OnPlayDeleteWithDpFilter_ShouldDeleteOnlyMatchingDigimon()
    {
        var (state, engine, cards) = NewMainPhase();
        var weak = new CardInstance(cards["ST1-02"], 1);    // DP 3000
        var strong = new CardInstance(cards["ST2-08"], 1);  // DP 7000
        state.DummyOpponent.BattleArea.AddRange(new[] { weak, strong });
        var killer = Api("SP-DELETE", 4, 5000, main: "[On Play] Delete 1 of your opponent's Digimon with 4000 DP or less.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.DoesNotContain(weak, state.DummyOpponent.BattleArea);
        Assert.Contains(strong, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void WhenDigivolvingDpMinusForTheTurn_ShouldDeleteAtZero()
    {
        var (state, engine, cards) = NewMainPhase();
        var enemy = new CardInstance(cards["ST1-02"], 1);   // DP 3000
        state.DummyOpponent.BattleArea.Add(enemy);
        var baseDigimon = new CardInstance(cards["ST1-02"], 1);
        state.Player.BattleArea.Add(baseDigimon);
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var evo = Api("SP-DPMINUS", 4, 5000, main: "[When Digivolving] 1 of your opponent's Digimon gets -3000 DP for the turn.", evoCost: 1);
        evo.EvoRequirementLevel = 3;
        state.Player.Hand.Add(evo);

        Assert.True(engine.EvolveDigimon(baseDigimon, evo).CanExecute);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void RevealTopCards_ShouldAddMatchingCardAndReturnTheRestToBottom()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.MainDeck.AddRange(new[] { cards["ST1-02"], cards["ST1-07"], cards["ST2-02"], cards["ST1-16"] });   // 피요몬, 그레이몬, 쉬라몬 | 가이아 포스
        var revealer = Api("SP-REVEAL", 3, 3000,
            main: "[On Play] Reveal the top 3 cards of your deck. Add 1 card with [그레이몬] in its name among them to the hand. Return the rest to the bottom of the deck.");
        state.Player.Hand.Add(revealer);

        engine.PlayCard(revealer);

        Assert.Contains(cards["ST1-07"], state.Player.Hand);
        Assert.Equal(new[] { "ST1-16", "ST1-02", "ST2-02" }, state.Player.MainDeck.Select(c => c.CardId));
    }

    [Fact]
    public void CostByTrashingHandCard_ShouldPayThenResolve()
    {
        var (state, engine, cards) = NewMainPhase();
        for (int i = 0; i < 3; i++) state.Player.MainDeck.Add(cards["ST1-02"]);
        state.Player.Hand.AddRange(new[] { cards["ST2-02"], cards["ST2-02"], cards["ST2-02"] });
        var drawer = Api("SP-COST", 3, 3000, main: "[On Play] By trashing 1 card in your hand, ＜Draw 2＞ (Draw 2 cards from your deck.)");
        state.Player.Hand.Add(drawer);

        engine.PlayCard(drawer);

        Assert.Single(state.Player.Trash);            // 패 1장을 버렸다
        Assert.Equal(4, state.Player.Hand.Count);     // 3 - 1 + 2
    }

    [Fact]
    public void ChoiceEffect_WithoutPlayerChoice_ShouldPickTheHigherValueOption()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.MainDeck.Add(cards["ST1-02"]);
        var chooser = Api("SP-CHOICE", 3, 3000, main: "[On Play] Activate 1 of the effects below:\n・Gain 1 memory.\n・＜Draw 2＞ (Draw 2 cards from your deck.)", playCost: 1);
        state.Player.Hand.Add(chooser);

        engine.PlayCard(chooser);

        Assert.Equal(2, state.MemoryGauge);    // 3 - 1 (메모리 +1은 고르지 않았다)
        Assert.Empty(state.Player.MainDeck);   // AI 평가: ≪2 드로우≫ > 메모리 +1
        Assert.All(KeywordDictionary.Analyze(chooser), c => Assert.Equal(ClauseSupport.Supported, c.Support));
    }

    [Fact]
    public void TamerStartOfTurnSetMemory_ShouldRaiseMemory()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.Tamers.Add(Api("SP-SETMEM", 0, 0, main: "[Start of Your Turn] If you have 2 or less memory, set it to 3.", type: CardType.Tamer));
        state.Player.MainDeck.Add(cards["ST1-02"]);
        state.MemoryGauge = -1;   // 연습 모드: 상대 턴 후 메모리 3으로 시작 → 테이머 효과로 3 유지

        engine.CheckTurnEndAndAutoReset();

        Assert.True(state.MemoryGauge >= 3);
    }

    [Fact]
    public void OpponentDigimonAttacksEvent_ShouldTriggerDuringAiAttack()
    {
        var (state, engine, cards) = NewAiBattle();
        state.Player.Tamers.Add(Api("SP-ATKEVENT", 0, 0, main: "[Opponent's Turn] When an opponent's Digimon attacks, ＜Draw 1＞ (Draw 1 card from your deck.)", type: CardType.Tamer));
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST1-05"], 1));   // DP 5000 어택

        engine.PassTurn();

        Assert.Contains(state.ActionLogs, l => l.Contains("SP-ATKEVENT") && l.Contains("드로우"));
    }

    [Fact]
    public void PlayCostReduction_ShouldPayLessWhenConditionHolds()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 5;
        state.Player.Tamers.Add(cards["ST1-12"]);
        var cheap = Api("SP-REDUCE", 5, 7000, main: "When this card would be played, if you have a Tamer, reduce the play cost by 3.", playCost: 7);
        state.Player.Hand.Add(cheap);

        Assert.True(engine.PlayCard(cheap).CanExecute);

        Assert.Equal(1, state.MemoryGauge);   // 5 - (7 - 3)
    }

    [Fact]
    public void OptionIgnoreColorRequirement_ShouldAllowUseWithTamer()
    {
        var (state, _, cards) = NewMainPhase();
        state.Player.Tamers.Add(cards["ST1-12"]);   // 레드 테이머
        var option = Api("SP-IGNORECOLOR", 0, 0, main: "[Main] Gain 1 memory.\nWhile you have a Tamer, you can ignore this card's color requirements.",
            color: CardColor.Purple, type: CardType.Option, playCost: 1);

        Assert.Empty(ActionValidator.MissingOptionColors(state.Player, option));
    }

    [Fact]
    public void TimedKeywordGrant_ShouldLastUntilEndOfOpponentsTurn()
    {
        var (state, engine, cards) = NewMainPhase();
        var mine = new CardInstance(cards["ST1-07"], 1);
        state.Player.BattleArea.Add(mine);
        var granter = Api("SP-GRANT", 3, 3000, main: "[On Play] 1 of your Digimon gains ＜Piercing＞ until the end of your opponent's turn.");
        state.Player.Hand.Add(granter);

        engine.PlayCard(granter);
        var target = state.Player.BattleArea.Single(d => CardScripts.HasKeyword(d, Keyword.Piercing));   // "1 of your Digimon" — 효과를 가진 디지몬을 먼저 고른다

        state.TurnSequence += 1;   // 상대 턴 중: 아직 유지
        engine.RecalculateFieldBuffs();
        Assert.True(CardScripts.HasKeyword(target, Keyword.Piercing));

        state.TurnSequence += 1;   // 상대 턴이 끝났다
        engine.RecalculateFieldBuffs();
        Assert.False(CardScripts.HasKeyword(target, Keyword.Piercing));
        Assert.False(CardScripts.HasKeyword(mine, Keyword.Piercing));
    }

    [Fact]
    public void AceOverflow_ShouldLoseMemoryWhenTheCardLeavesTheField()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 5;
        var ace = new CardInstance(Api("SP-ACE", 6, 12000, main: "＜Blocker＞\n", inherited: "Ace Overflow ＜-3＞"), 1);
        state.DummyOpponent.BattleArea.Add(ace);
        state.Player.Tamers.Add(cards["ST1-12"]);
        state.Player.Hand.Add(cards["ST1-16"]);   // 가이아 포스: 상대 디지몬 1마리 소멸
        state.MemoryGauge = 10;

        engine.PlayCard(cards["ST1-16"], ace);

        Assert.DoesNotContain(ace, state.DummyOpponent.BattleArea);
        Assert.Equal(10 - 8 + 3, state.MemoryGauge);   // 상대의 메모리 -3 = 내 쪽 +3
    }
}
