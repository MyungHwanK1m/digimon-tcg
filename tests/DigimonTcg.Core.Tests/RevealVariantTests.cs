using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>"덱 위 N장 공개" 효과의 여러 표기 (앞에 붙는 비용 · 조건, 패에 넣기 + 파기 · 등장 · 진화원 아래, "such", 2색 카드, "If you added cards")</summary>
public class RevealVariantTests
{
    private sealed class AcceptAll : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => true;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Api(string id, string main, CardType type = CardType.Option, int level = 0, CardColor color = CardColor.Yellow)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = type == CardType.Digimon ? 5000 : 0, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    /// <summary>덱에 넣을 카드 (장마다 다른 인스턴스)</summary>
    private static CardData Deck(string name, CardType type, CardColor color, CardColor? second = null, params string[] traits) => new()
    {
        CardId = "RV-" + name, Name = name, Type = type, Color = color, SecondaryColor = second, Level = type == CardType.Digimon ? 4 : 0,
        DP = type == CardType.Digimon ? 4000 : 0, PlayCost = 4, Traits = traits.ToList(),
    };

    private static (GameState State, GoldfishEngine Engine) NewMainPhase(params CardData[] deckTop)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        state.Player.MainDeck.AddRange(deckTop);
        for (int i = 0; i < 5; i++)
        {
            state.Player.MainDeck.Add(Deck("Filler" + i, CardType.Option, CardColor.White));
            state.DummyOpponent.MainDeck.Add(Deck("OppFiller" + i, CardType.Option, CardColor.White));
        }
        state.Player.BattleArea.Add(new CardInstance(Deck("YellowBase", CardType.Digimon, CardColor.Yellow), 1));   // 옵션 색 조건
        return (state, new GoldfishEngine(state));
    }

    private static void Use(GameState state, GoldfishEngine engine, CardData option)
    {
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(option).Level);
        state.Player.Hand.Add(option);
        engine.PlayCard(option);
    }

    [Fact]
    public void ConditionBeforeReveal_IsParsed()
    {
        var black = Deck("Black", CardType.Digimon, CardColor.Black);
        var (state, engine) = NewMainPhase(Deck("Red", CardType.Digimon, CardColor.Red), black, Deck("Blue", CardType.Digimon, CardColor.Blue));
        var opt = Api("RV-OPT-1", "[Main] If it's your turn, reveal the top 3 cards of your deck. Add 1 black card among them to your hand. Trash the rest.");

        Use(state, engine, opt);

        Assert.Contains(black, state.Player.Hand);
        Assert.Equal(3, state.Player.Trash.Count);                                     // 나머지 2장 + 사용한 옵션
        Assert.Equal(5, state.Player.MainDeck.Count);
    }

    [Fact]
    public void AddAndTrashSuchCard()
    {
        var a = Deck("DigiA", CardType.Digimon, CardColor.Red);
        var b = Deck("DigiB", CardType.Digimon, CardColor.Red);
        var tamer = Deck("Tamer", CardType.Tamer, CardColor.Red);
        var (state, engine) = NewMainPhase(a, tamer, b);
        var opt = Api("RV-OPT-2", "[Main] Reveal the top 3 cards of your deck. Among them, add 1 Digimon card to the hand and trash 1 such card. Return the rest to the top of the deck.");

        Use(state, engine, opt);

        Assert.Single(state.Player.Hand);
        Assert.Equal(CardType.Digimon, state.Player.Hand[0].Type);
        Assert.Contains(state.Player.Trash, x => x.Type == CardType.Digimon);           // 다른 디지몬 1장 파기
        Assert.Same(tamer, state.Player.MainDeck[0]);                                   // 나머지는 덱 위로
    }

    [Fact]
    public void AddAndPlayAmongThem()
    {
        var digi = Deck("PlayMe", CardType.Digimon, CardColor.Green);
        var tamer = Deck("TamerMe", CardType.Tamer, CardColor.Green);
        var (state, engine) = NewMainPhase(tamer, Deck("Opt", CardType.Option, CardColor.Green), digi);
        var opt = Api("RV-OPT-3", "[Main] Reveal the top 3 cards of your deck. You may add 1 Tamer card among them to your hand, and play 1 Digimon card among them without paying the cost. Place the rest at the bottom of your deck in any order.");

        Use(state, engine, opt);

        Assert.Contains(tamer, state.Player.Hand);
        Assert.Contains(state.Player.BattleArea, d => ReferenceEquals(d.TopCard, digi));
        Assert.Equal(8 - 3, state.MemoryGauge);                                         // 등장 코스트는 내지 않는다
    }

    [Fact]
    public void PlaceUnderThisDigimon()
    {
        var src = Deck("Source", CardType.Digimon, CardColor.Black);
        var (state, engine) = NewMainPhase(Deck("Opt", CardType.Option, CardColor.Black), src, Deck("Tamer", CardType.Tamer, CardColor.Black));
        var digi = Api("RV-DIGI-1", "[On Play] Reveal the top 3 cards of your deck. Place 1 Digimon card among them under this Digimon as its bottom digivolution card. Trash the rest.", CardType.Digimon, level: 5, color: CardColor.Black);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(digi).Level);
        state.Player.Hand.Add(digi);

        engine.PlayCard(digi);

        var played = state.Player.BattleArea.Single(d => ReferenceEquals(d.TopCard, digi));
        Assert.Contains(src, played.EvolutionSources);
        Assert.Equal(2, state.Player.Trash.Count);
    }

    [Fact]
    public void SuchTamerCard_KeepsColorFromPreviousPick()
    {
        var greenDigi = Deck("GreenDigi", CardType.Digimon, CardColor.Green);
        var redTamer = Deck("RedTamer", CardType.Tamer, CardColor.Red);
        var greenTamer = Deck("GreenTamer", CardType.Tamer, CardColor.Green);
        var (state, engine) = NewMainPhase(greenDigi, redTamer, greenTamer);
        var opt = Api("RV-OPT-4", "[Main] Reveal the top 3 cards of your deck. Add 1 green Digimon card and 1 such Tamer card among them to the hand. Return the rest to the bottom of the deck.");

        Use(state, engine, opt);

        Assert.Contains(greenDigi, state.Player.Hand);
        Assert.Contains(greenTamer, state.Player.Hand);
        Assert.DoesNotContain(redTamer, state.Player.Hand);
    }

    [Fact]
    public void TwoColorCard_Pick()
    {
        var mono = Deck("MonoBlack", CardType.Digimon, CardColor.Black);
        var dual = Deck("BlackYellow", CardType.Digimon, CardColor.Black, CardColor.Yellow);
        var (state, engine) = NewMainPhase(mono, dual, Deck("Opt", CardType.Option, CardColor.Red));
        var opt = Api("RV-OPT-5", "[Main] Reveal the top 3 cards of your deck. Add 1 2-color black card among them to your hand. Place the rest at the bottom of your deck in any order.");

        Use(state, engine, opt);

        Assert.Equal(new[] { dual }, state.Player.Hand);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfYouAddedCards_DependsOnPick(bool hasTamer)
    {
        var top = hasTamer ? Deck("Tamer", CardType.Tamer, CardColor.Red) : Deck("Digi", CardType.Digimon, CardColor.Red);
        var (state, engine) = NewMainPhase(top);
        var opt = Api("RV-OPT-6", "[Main] Reveal the top 3 cards of your deck. Add 1 Tamer card among them to the hand. Return the rest to the bottom of the deck. If you added cards, gain 2 memory.");

        Use(state, engine, opt);

        Assert.Equal(8 - 3 + (hasTamer ? 2 : 0), state.MemoryGauge);
    }

    [Theory]
    [InlineData(CardColor.Black, true)]
    [InlineData(CardColor.Red, false)]
    public void TopCard_IfItHasTrait_TrashTheRest(CardColor color, bool added)
    {
        var top = Deck("Top", CardType.Digimon, color, null, color == CardColor.Black ? "Twilight" : "Free");
        var (state, engine) = NewMainPhase(top);
        var opt = Api("RV-OPT-7", "[Main] Reveal the top card of your deck. If that card has the [Blue Flare] trait or [Twilight] trait, add it to your hand. Trash the rest.");

        Use(state, engine, opt);

        Assert.Equal(added, state.Player.Hand.Contains(top));
        Assert.Equal(!added, state.Player.Trash.Contains(top));
    }

    [Fact]
    public void CostBeforeReveal_ByForm()
    {
        var black = Deck("Black", CardType.Digimon, CardColor.Black);
        var (state, engine) = NewMainPhase(black);
        state.Player.Hand.Add(Deck("Discard", CardType.Option, CardColor.Red));
        engine.PlayerChoices = new AcceptAll();                                         // 비용을 낸다
        var opt = Api("RV-OPT-8", "[Main] By trashing 1 card in your hand, reveal the top 3 cards of your deck. Add 1 black card among them to your hand. Return the rest to the bottom of your deck in any order.");

        Use(state, engine, opt);

        Assert.Equal(new[] { black }, state.Player.Hand);                               // 패 1장을 버리고 1장 추가
    }
}
