using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>여러 장을 한 번에 고르는 선택 (디지몬 N마리 · 패 / 트래시 카드) — 플레이어만 묻고 AI · 테스트는 추천 답</summary>
public class MultiChoiceTests
{
    /// <summary>정해 둔 답을 돌려주는 선택기 (호출 횟수와 받은 인자를 기록)</summary>
    private sealed class FixedChoices : IChoiceProvider
    {
        public IReadOnlyList<CardInstance>? DigimonAnswer;
        public IReadOnlyList<int>? CardAnswer;
        public int Calls;
        public (int Min, int Max, IReadOnlyList<int> Recommended)? LastCardArgs;

        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => recommended;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;

        public IReadOnlyList<CardInstance> ChooseDigimons(GameState state, string question, IReadOnlyList<CardInstance> options, int min, int max, IReadOnlyList<CardInstance> recommended)
        {
            Calls++;
            return DigimonAnswer ?? recommended;
        }

        public IReadOnlyList<int> ChooseCards(GameState state, string question, CardChoiceZone zone, IReadOnlyList<CardData> options, int min, int max, IReadOnlyList<int> recommended)
        {
            Calls++;
            LastCardArgs = (min, max, recommended);
            return CardAnswer ?? recommended;
        }
    }

    private static (GameState State, GoldfishEngine Engine, FixedChoices Choices, Dictionary<string, CardData> Cards) Setup()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3, TurnSequence = 3 };
        var choices = new FixedChoices();
        var engine = new GoldfishEngine(state) { PlayerChoices = choices };
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        return (state, engine, choices, cards);
    }

    [Fact]
    public void AskDigimons_ForPlayer_ShouldAskOnceAndUseTheAnswer()
    {
        var (state, engine, choices, cards) = Setup();
        var a = new CardInstance(cards["ST1-02"], 1);
        var b = new CardInstance(cards["ST1-03"], 1);
        var c = new CardInstance(cards["ST1-04"], 1);
        choices.DigimonAnswer = new[] { c, a };

        var picked = engine.AskDigimons(state.Player, new[] { a, b, c }, 2, upTo: false, "2마리");

        Assert.Equal(1, choices.Calls);                     // N번 이어서 묻지 않고 한 번에
        Assert.Equal(new[] { c, a }, picked);
    }

    [Fact]
    public void AskDigimons_ForAi_ShouldUseRecommendationWithoutAsking()
    {
        var (state, engine, choices, cards) = Setup();
        var ranked = new[] { new CardInstance(cards["ST1-02"], 1), new CardInstance(cards["ST1-03"], 1), new CardInstance(cards["ST1-04"], 1) };

        var picked = engine.AskDigimons(state.DummyOpponent, ranked, 2, upTo: false, "AI");

        Assert.Equal(0, choices.Calls);
        Assert.Equal(ranked.Take(2), picked);
    }

    [Fact]
    public void AskDigimons_InvalidAnswer_ShouldFallBackToRecommendation()
    {
        var (state, engine, choices, cards) = Setup();
        var ranked = new[] { new CardInstance(cards["ST1-02"], 1), new CardInstance(cards["ST1-03"], 1), new CardInstance(cards["ST1-04"], 1) };
        choices.DigimonAnswer = new[] { ranked[0] };       // 2마리를 골라야 하는데 1마리

        Assert.Equal(ranked.Take(2), engine.AskDigimons(state.Player, ranked, 2, upTo: false, "2마리"));

        choices.DigimonAnswer = new[] { ranked[0], ranked[0] };   // 같은 디지몬 두 번
        Assert.Equal(ranked.Take(2), engine.AskDigimons(state.Player, ranked, 2, upTo: false, "2마리"));
    }

    [Fact]
    public void AskDigimons_UpTo_ShouldAllowChoosingNone_AndNotAskWhenThereIsNothingToChoose()
    {
        var (state, engine, choices, cards) = Setup();
        var ranked = new[] { new CardInstance(cards["ST1-02"], 1), new CardInstance(cards["ST1-03"], 1) };
        choices.DigimonAnswer = new List<CardInstance>();

        Assert.Empty(engine.AskDigimons(state.Player, ranked, 2, upTo: true, "2마리까지"));
        Assert.Equal(1, choices.Calls);

        Assert.Equal(ranked, engine.AskDigimons(state.Player, ranked, 2, upTo: false, "2마리"));   // 후보 2 = 고를 수 2 → 묻지 않음
        Assert.Equal(1, choices.Calls);
    }

    [Fact]
    public void ChooseCards_ShouldTellCopiesOfTheSameCardApartByIndex()
    {
        var (state, engine, choices, cards) = Setup();
        var agumon = cards["ST1-03"];
        var hand = new List<CardData> { agumon, agumon, cards["ST1-07"] };   // 같은 카드 객체 2장
        choices.CardAnswer = new[] { 0, 1 };

        var picked = engine.ChooseCards(state.Player, CardChoiceZone.Hand, hand, 2, upTo: false, "버릴 카드 2장");

        Assert.Equal(new[] { agumon, agumon }, picked);
        Assert.Equal((2, 2), (choices.LastCardArgs!.Value.Min, choices.LastCardArgs.Value.Max));
        Assert.Equal(new[] { 0, 1 }, choices.LastCardArgs.Value.Recommended);   // 추천 = 엔진이 넘긴 순서의 앞 2장
    }

    [Fact]
    public void ChooseCards_InvalidIndexes_ShouldFallBackToRecommendation()
    {
        var (state, engine, choices, cards) = Setup();
        var trash = new List<CardData> { cards["ST1-07"], cards["ST1-03"], cards["ST1-02"] };

        choices.CardAnswer = new[] { 0, 0 };                    // 같은 번호 두 번
        Assert.Equal(trash.Take(2), engine.ChooseCards(state.Player, CardChoiceZone.Trash, trash, 2, upTo: false, "q"));
        choices.CardAnswer = new[] { 5 };                       // 후보 밖
        Assert.Equal(trash.Take(1), engine.ChooseCards(state.Player, CardChoiceZone.Trash, trash, 1, upTo: false, "q"));
    }

    [Fact]
    public void ChooseCards_ForAiOrWithoutUi_ShouldUseRecommendation()
    {
        var (state, engine, choices, cards) = Setup();
        var trash = new List<CardData> { cards["ST1-07"], cards["ST1-03"], cards["ST1-02"] };

        Assert.Equal(trash.Take(1), engine.ChooseCards(state.DummyOpponent, CardChoiceZone.Trash, trash, 1, upTo: false, "q"));
        engine.PlayerChoices = null;
        Assert.Equal(trash.Take(1), engine.ChooseCards(state.Player, CardChoiceZone.Trash, trash, 1, upTo: false, "q"));
        Assert.Equal(0, choices.Calls);
    }

    [Fact]
    public void ProvidersThatDoNotImplementTheNewMethods_ShouldGetTheRecommendation()
    {
        // 기본 구현(추천 답 그대로) — 4단계에 만든 선택기 · 테스트용 랜덤 선택기가 깨지지 않는다
        IChoiceProvider minimal = new MinimalChoices();
        var options = new List<CardData> { new() { CardId = "X" }, new() { CardId = "Y" } };
        Assert.Equal(new[] { 1 }, minimal.ChooseCards(new GameState(), "q", CardChoiceZone.Hand, options, 1, 1, new[] { 1 }));
    }

    private sealed class MinimalChoices : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => recommended;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }
}
