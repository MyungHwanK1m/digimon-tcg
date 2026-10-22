using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과로 옵션을 코스트 없이 사용: "You may use 1 Option card with a cost of 5 or less in your hand without paying the cost"</summary>
public class OptionUseByEffectTests
{
    private sealed class AcceptAll : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => true;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended ?? options.FirstOrDefault();
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Card(string id, string main = "", CardType type = CardType.Option, int level = 0, int playCost = 3, CardColor color = CardColor.Yellow)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = level * 1000, PlayCost = playCost, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private const string UserText = "[On Play] You may use 1 Option card with a cost of 5 or less in your hand without paying the cost.";

    private static (GameState State, GoldfishEngine Engine) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("OU-FILL" + i, type: CardType.Digimon, level: 3));
            state.DummyOpponent.MainDeck.Add(Card("OU-OFILL" + i, type: CardType.Digimon, level: 3));
        }
        return (state, new GoldfishEngine(state) { PlayerChoices = new AcceptAll() });
    }

    private static void PlayUser(GameState state, GoldfishEngine engine)
    {
        var user = Card("OU-USER", UserText, CardType.Digimon, level: 4);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(user).Level);
        state.Player.Hand.Add(user);
        Assert.True(engine.PlayCard(user).CanExecute);
    }

    [Fact]
    public void UsesOptionFromHandWithoutPaying()
    {
        var (state, engine) = NewMainPhase();
        var option = Card("OU-GAIN", "[Main] Gain 2 memory.", playCost: 4);
        state.Player.Hand.Add(option);

        PlayUser(state, engine);

        Assert.Equal(8 - 3 + 2, state.MemoryGauge);                                      // 디지몬 코스트만 내고 옵션 효과
        Assert.Contains(option, state.Player.Trash);
        Assert.DoesNotContain(option, state.Player.Hand);
    }

    [Theory]
    [InlineData(CardColor.Purple, 4)]   // 색 조건을 만족하지 않는다 (필드에 퍼플이 없음)
    [InlineData(CardColor.Yellow, 6)]   // 코스트가 5보다 크다
    public void NotUsable_StaysInHand(CardColor color, int cost)
    {
        var (state, engine) = NewMainPhase();
        var option = Card("OU-NO-" + color + cost, "[Main] Gain 2 memory.", playCost: cost, color: color);
        state.Player.Hand.Add(option);

        PlayUser(state, engine);

        Assert.Equal(8 - 3, state.MemoryGauge);
        Assert.Contains(option, state.Player.Hand);
    }
}
