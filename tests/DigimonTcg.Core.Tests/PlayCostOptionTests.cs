using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>등장할 때 비용을 내고 코스트를 줄이는 효과: "When this card would be played, by ~ing, reduce the play cost by N"</summary>
public class PlayCostOptionTests
{
    private sealed class Answer(bool yes) : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => yes;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended ?? options.FirstOrDefault();
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, int level = 5, int playCost = 6, CardColor color = CardColor.Purple)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = level * 1000, PlayCost = playCost, EvoCost = 2,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine) NewMainPhase(bool pay)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("PO-FILL" + i, color: CardColor.White));
            state.DummyOpponent.MainDeck.Add(Card("PO-OFILL" + i, color: CardColor.White));
        }
        return (state, new GoldfishEngine(state) { PlayerChoices = new Answer(pay) });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TrashFromHand_ReducesCost(bool pay)
    {
        var (state, engine) = NewMainPhase(pay);
        var discard = Card("PO-DISCARD", type: CardType.Option, level: 0, playCost: 1);
        state.Player.Hand.Add(discard);
        var card = Card("PO-1", "When this card would be played, by trashing 1 card in your hand, reduce the play cost by 2.");
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(card).Level);
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.Equal(8 - (pay ? 4 : 6), state.MemoryGauge);
        Assert.Equal(pay, state.Player.Trash.Contains(discard));
        Assert.Contains(state.Player.BattleArea, d => ReferenceEquals(d.TopCard, card));
    }

    [Fact]
    public void DeleteOwnDigimon_DoesNotPickThePlayedCard()
    {
        var (state, engine) = NewMainPhase(pay: true);
        var fodder = new CardInstance(Card("PO-FODDER", level: 3, playCost: 3), 1);
        state.Player.BattleArea.Add(fodder);
        var card = Card("PO-2", "When you would play this card from your hand, you may delete 1 of your Digimon to reduce this card's play cost by 4.");
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(card).Level);
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.DoesNotContain(fodder, state.Player.BattleArea);
        Assert.Contains(state.Player.BattleArea, d => ReferenceEquals(d.TopCard, card));
        Assert.Equal(8 - 2, state.MemoryGauge);
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(0, 6)]
    public void Preview_UsesRecommendedPayableReductions(int spareCards, int expected)
    {
        var (state, engine) = NewMainPhase(pay: false);
        var card = Card("PO-4", "When this card would be played, by trashing 1 card in your hand, reduce the play cost by 2.");
        for (int i = 0; i < spareCards; i++) state.Player.Hand.Add(Card("PO-SPARE" + i, type: CardType.Option, level: 0, playCost: 1));
        state.Player.Hand.Add(card);
        int handBefore = state.Player.Hand.Count;

        Assert.Equal(expected, engine.PreviewPlayCostWithOptions(state.Player, card));   // 패가 넉넉하면 추천 = 비용을 낸다
        Assert.Equal(handBefore, state.Player.Hand.Count);                                // 미리보기는 아무것도 바꾸지 않는다
        Assert.Equal(8, state.MemoryGauge);
        Assert.Equal("trashing 1 card in your hand", CardScripts.For(card)!.PlayCostOptions[0].CostText);
    }

    [Fact]
    public void NothingToPay_FullCost()
    {
        var (state, engine) = NewMainPhase(pay: true);
        var card = Card("PO-3", "When this card would be played, by deleting 1 of your Digimon, reduce the play cost by 4.");
        state.Player.Hand.Add(card);

        engine.PlayCard(card);

        Assert.Equal(8 - 6, state.MemoryGauge);                                         // 소멸시킬 디지몬이 없다 (이 카드는 아직 필드에 없음)
    }
}
