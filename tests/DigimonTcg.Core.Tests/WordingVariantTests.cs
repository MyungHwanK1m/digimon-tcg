using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// 같은 뜻인데 표기만 다른 효과 문장 (어순 · 동의어 · 관사 생략 · 원문 오타)이 지원 문장과 똑같이 동작하는지.
/// 카드 번호는 테스트마다 달라야 한다 (자동 스크립트가 카드 번호로 캐시됨).
/// </summary>
public class WordingVariantTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static CardData Api(string id, string name, string main, int level = 3, int dp = 3000, CardType type = CardType.Digimon, CardColor color = CardColor.Red, int playCost = 2)
    {
        var card = new CardData
        {
            CardId = id, Name = name, Type = type, Color = color, Level = level, DP = dp, PlayCost = playCost, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        var cards = Cards();
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(cards["ST1-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST2-02"]);
        }
        return (state, new GoldfishEngine(state), cards);
    }

    private static bool FullySupported(CardData card) => EffectSupport.For(card).Level == EffectSupportLevel.Full;

    [Fact]
    public void While_HandCountCondition_WorksLikeIf()
    {
        var (state, engine, cards) = NewMainPhase();
        var card = Api("WV-HAND-1", "Hand Watcher", "[Your Turn] While you have 3 or fewer cards in your hand, this Digimon gets +2000 DP.");
        Assert.True(FullySupported(card));
        var digi = new CardInstance(card, 1);
        state.Player.BattleArea.Add(digi);

        state.Player.Hand.AddRange(new[] { cards["ST1-02"], cards["ST1-02"] });
        engine.RecalculateFieldBuffs();
        Assert.Equal(5000, digi.GetCurrentDP());

        state.Player.Hand.AddRange(new[] { cards["ST1-02"], cards["ST1-02"] });
        engine.RecalculateFieldBuffs();
        Assert.Equal(3000, digi.GetCurrentDP());
    }

    [Theory]
    [InlineData(6000, 1)]
    [InlineData(4000, 0)]
    public void If_ThisDigimonHasDpOrMore(int dp, int drawn)
    {
        var (state, engine, cards) = NewMainPhase();
        var card = Api("WV-DP-" + dp, "Strong", "[When Attacking] If this Digimon has 5000 DP or more, ＜Draw 1＞ (Draw 1 card from your deck.)", dp: dp);
        Assert.True(FullySupported(card));
        var digi = new CardInstance(card, 1);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        int hand = state.Player.Hand.Count;

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(hand + drawn, state.Player.Hand.Count);
    }

    [Fact]
    public void If_YouHaveNamedCardInPlay_WithoutArticle()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(Api("WV-AGU", "Agumon", ""), 1));
        var card = Api("WV-NOART", "Partner", "[On Play] If you have [Agumon] in play, gain 1 memory.");
        Assert.True(FullySupported(card));
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.Equal(8 - 2 + 1, state.MemoryGauge);
    }

    [Fact]
    public void LeadingDuration_UntilOpponentsTurnEnds()
    {
        var (state, engine, cards) = NewMainPhase();
        var card = Api("WV-LEAD", "Wall", "[On Play] Until your opponent's turn ends, this Digimon gains ＜Blocker＞ (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)");
        Assert.True(FullySupported(card));
        state.Player.Hand.Add(card);

        engine.PlayCard(card);
        var digi = state.Player.BattleArea.Single();
        engine.RecalculateFieldBuffs();

        Assert.True(digi.HasBlocker);
    }

    [Fact]
    public void ToTargetGiveDp_WordOrder()
    {
        var (state, engine, cards) = NewMainPhase();
        var enemy = new CardInstance(cards["ST2-02"], 1);                              // DP 3000
        state.DummyOpponent.BattleArea.Add(enemy);
        var card = Api("WV-TOGIVE", "Shrinker", "[On Play] To 1 of your opponent's Digimon, give -3000 DP for the turn.");
        Assert.True(FullySupported(card));
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);                  // DP 0 → 소멸
    }

    [Theory]
    [InlineData(2, 2, 1)]   // 합계 4 → 조건 충족
    [InlineData(3, 3, 0)]   // 합계 6 → 불충족
    public void BothPlayersSecurityTotal(int mine, int theirs, int gained)
    {
        var (state, engine, cards) = NewMainPhase();
        for (int i = 0; i < mine; i++) state.Player.SecurityStack.Add(cards["ST1-02"]);
        for (int i = 0; i < theirs; i++) state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        var card = Api("WV-SECSUM-" + mine + theirs, "Counter", "[On Play] If there're 4 or fewer total cards in both players' security stacks, gain 1 memory.");
        Assert.True(FullySupported(card));
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.Equal(8 - 2 + gained, state.MemoryGauge);
    }

    [Fact]
    public void OpponentHasNoUnsuspendedDigimon()
    {
        var (state, engine, cards) = NewMainPhase();
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-02"], 1) { IsSuspended = true });
        var card = Api("WV-NOACTIVE", "Opportunist", "[On Play] If your opponent has no unsuspended Digimon, gain 1 memory.");
        Assert.True(FullySupported(card));
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.Equal(8 - 2 + 1, state.MemoryGauge);
    }

    [Fact]
    public void ItsYourOpponentsTurn_IsRecognized()
    {
        var card = Api("WV-OPPTURN", "Night Watch", "[All Turns] When this Digimon becomes suspended, if it's your opponent's turn, gain 1 memory.");
        Assert.True(FullySupported(card));
    }

    [Fact]
    public void Typo_Batttle_IsReadAsBattle()
    {
        var typo = Api("WV-TYPO-B", "Typo", "[On Play] If you have 2 or fewer Digimon in the batttle area, gain 1 memory.");
        var fixedText = Api("WV-TYPO-OK", "Fixed", "[On Play] If you have 2 or fewer Digimon in the battle area, gain 1 memory.");
        Assert.Equal(EffectSupport.For(fixedText).Level, EffectSupport.For(typo).Level);
    }
}
