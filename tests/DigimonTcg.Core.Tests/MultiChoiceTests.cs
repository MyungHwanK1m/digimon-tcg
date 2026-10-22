using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>여러 장을 한 번에 고르는 선택 (디지몬 N마리 · 패 / 트래시 카드) — 플레이어만 묻고 AI · 테스트는 추천 답</summary>
[Collection("KoreanCardData")]   // 질문의 카드 이름을 공식 한글로 바꾸는 KoreanNameLocalizer가 전역 한글 데이터를 읽는다
public class MultiChoiceTests
{
    /// <summary>정해 둔 답을 돌려주는 선택기 (호출 횟수와 받은 인자를 기록)</summary>
    private sealed class FixedChoices : IChoiceProvider
    {
        public IReadOnlyList<CardInstance>? DigimonAnswer;
        public IReadOnlyList<int>? CardAnswer;
        public int Calls;
        public string? LastQuestion;
        public (int Min, int Max, IReadOnlyList<int> Recommended)? LastCardArgs;

        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => recommended;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;

        public IReadOnlyList<CardInstance> ChooseDigimons(GameState state, string question, IReadOnlyList<CardInstance> options, int min, int max, IReadOnlyList<CardInstance> recommended)
        {
            Calls++;
            LastQuestion = question;
            return DigimonAnswer ?? recommended;
        }

        public IReadOnlyList<int> ChooseCards(GameState state, string question, CardChoiceZone zone, IReadOnlyList<CardData> options, int min, int max, IReadOnlyList<int> recommended)
        {
            Calls++;
            LastQuestion = question;
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
    public void ChooseCards_ForPlayer_ShouldAskEvenWithOnlyOneCandidate()
    {
        // 트래시에 맞는 카드가 1장뿐이어도 패 · 트래시를 오가는 효과는 창을 건너뛰지 않는다 (그 1장을 골라 둔 채 확인)
        var (state, engine, choices, cards) = Setup();
        var trash = new List<CardData> { cards["ST1-07"] };

        var picked = engine.ChooseCards(state.Player, CardChoiceZone.Trash, trash, 1, upTo: false, "패로 되돌릴 카드 1장");

        Assert.Equal(1, choices.Calls);
        Assert.Equal((1, 1), (choices.LastCardArgs!.Value.Min, choices.LastCardArgs.Value.Max));
        Assert.Equal(new[] { 0 }, choices.LastCardArgs.Value.Recommended);
        Assert.Equal(trash, picked);
    }

    [Fact]
    public void ChoiceQuestions_ShouldShowOfficialKoreanCardNames()
    {
        // 효과 문장에서 온 영어 카드 이름 → 공식 한글 이름 (한글 데이터가 없는 PC에서는 그대로)
        var (state, engine, choices, cards) = Setup();
        const string question = "[MetalGarurumon] 효과로 고르세요.";
        var digimon = new[] { new CardInstance(cards["ST1-02"], 1), new CardInstance(cards["ST1-03"], 1) };

        engine.ChooseCards(state.Player, CardChoiceZone.Trash, new List<CardData> { cards["ST1-07"], cards["ST1-03"] }, 1, upTo: false, question);
        Assert.Equal(KoreanNameLocalizer.Localize(question), choices.LastQuestion);
        engine.AskDigimons(state.Player, digimon, 1, upTo: false, question);
        Assert.Equal(KoreanNameLocalizer.Localize(question), choices.LastQuestion);
        if (KoreanCardData.Count > 0)
            Assert.Contains("메탈가루몬", choices.LastQuestion);
    }

    [Fact]
    public void ReturnUpToFromTrash_ShouldLetThePlayerPickNothing()
    {
        // "up to 1" = 0장도 된다 → 창에 "고르지 않음"(min 0)이 있고, 빈 답이면 트래시에 그대로 남는다
        var (state, engine, choices, cards) = Setup();
        var digimon = cards["ST1-03"];
        state.Player.Trash.Add(digimon);
        choices.CardAnswer = new int[0];
        var option = new CardData { CardId = "MC-UPTO", Name = "MC-UPTO", Type = CardType.Option, Color = CardColor.Red,
                                    EffectText = "[Main] Return up to 1 Digimon card from your trash to the hand." };

        engine.RunEffects(EffectTrigger.Main, state.Player, null, option, includeInherited: false);

        Assert.Equal(1, choices.Calls);
        Assert.Equal(0, choices.LastCardArgs!.Value.Min);
        Assert.Contains(digimon, state.Player.Trash);
        Assert.DoesNotContain(digimon, state.Player.Hand);
    }

    [Fact]
    public void Dracmon_ReturnFromTrashIsOptional()
    {
        // 드라큐몬 [등장 시] "~패로 되돌릴 수 있다" — 고르지 않아도 된다
        var (state, engine, choices, cards) = Setup();
        var option = cards.Values.First(c => c.Type == CardType.Option && c.Color == CardColor.Purple && c.PlayCost is 1 or 7);
        state.Player.Trash.Add(option);
        choices.CardAnswer = new int[0];
        var dracmon = new CardInstance(cards["ST6-04"], 2);
        state.Player.BattleArea.Add(dracmon);

        engine.RunEffects(EffectTrigger.OnPlay, state.Player, dracmon, dracmon.TopCard, includeInherited: false);

        Assert.Equal(1, choices.Calls);
        Assert.Equal(0, choices.LastCardArgs!.Value.Min);
        Assert.Contains(option, state.Player.Trash);
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
