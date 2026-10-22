using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>비용("By ~ing, ~")을 낼 수 없거나 내지 않으면 로그를 남긴다 — 조용히 넘어가면 효과가 안 나온 것처럼 보인다 (ST21-07 팔몬 신고)</summary>
public class CostSkipLogTests
{
    private sealed class Answer(bool yes) : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => yes;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended ?? options.FirstOrDefault();
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, params string[] traits)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = CardColor.Green, Level = 3, DP = 3000, PlayCost = 3, EvoCost = 0,
            EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Green, EffectText = main, Traits = traits.ToList(),
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private const string PalmonText = "[On Play] By trashing 1 card with the [ADVENTURE] trait in your hand, ＜Draw 2＞ (Draw 2 cards from your deck.)";

    private static (GameState State, GoldfishEngine Engine, CardData Palmon) Setup(bool yes, bool hasAdventure)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3, MemoryGauge = 5 };
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("CS-FILL" + i, type: CardType.Option));
            state.DummyOpponent.MainDeck.Add(Card("CS-OFILL" + i, type: CardType.Option));
        }
        state.Player.Hand.Add(hasAdventure ? Card("CS-ADV", "", CardType.Digimon, "ADVENTURE") : Card("CS-OTHER", type: CardType.Option));
        var palmon = Card("CS-PALMON", PalmonText, CardType.Digimon, "ADVENTURE");
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(palmon).Level);
        state.Player.Hand.Add(palmon);
        return (state, new GoldfishEngine(state) { PlayerChoices = new Answer(yes) }, palmon);
    }

    [Fact]
    public void CannotPay_LogsWhyTheEffectDidNotHappen()
    {
        var (state, engine, palmon) = Setup(yes: true, hasAdventure: false);

        engine.PlayCard(palmon);

        Assert.Contains(state.ActionLogs, l => l.Contains("비용을 낼 수 없어 효과를 발휘하지 않습니다"));
        Assert.Single(state.Player.Hand);                                               // 드로우 없음
    }

    [Fact]
    public void Declined_LogsThatTheEffectWasNotUsed()
    {
        var (state, engine, palmon) = Setup(yes: false, hasAdventure: true);

        engine.PlayCard(palmon);

        Assert.Contains(state.ActionLogs, l => l.Contains("효과를 발휘하지 않았습니다"));
        Assert.Single(state.Player.Hand);
    }

    [Fact]
    public void Paid_DrawsWithoutSkipLog()
    {
        var (state, engine, palmon) = Setup(yes: true, hasAdventure: true);

        engine.PlayCard(palmon);

        Assert.DoesNotContain(state.ActionLogs, l => l.Contains("발휘하지 않"));
        Assert.Equal(2, state.Player.Hand.Count);                                       // ADVENTURE 1장 버리고 2장 드로우
    }
}
