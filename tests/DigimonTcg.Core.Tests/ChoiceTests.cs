using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과 확장 4단계 — 효과 대상 · "~할 수 있다" · 고르는 효과를 플레이어가 고르고, AI는 평가 함수로 고른다</summary>
public class ChoiceTests
{
    /// <summary>테스트용 선택: 정해 둔 답을 돌려주고 물은 내용을 기록한다</summary>
    private sealed class ScriptedChoices : IChoiceProvider
    {
        public Func<IReadOnlyList<CardInstance>, CardInstance?>? Digimon { get; init; }
        public bool ConfirmAnswer { get; init; } = true;
        public int OptionAnswer { get; init; }
        public List<string> Asked { get; } = new();

        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) { Asked.Add("confirm:" + question); return ConfirmAnswer; }
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional)
        {
            Asked.Add("digimon:" + question);
            return Digimon?.Invoke(options) ?? recommended;
        }
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) { Asked.Add("option:" + question); return OptionAnswer; }
    }

    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static CardData Api(string id, string main, CardType type = CardType.Digimon, int playCost = 1, CardColor color = CardColor.Red)
    {
        var card = new CardData { CardId = id, Name = id, Type = type, Color = color, Level = 3, DP = 3000, PlayCost = playCost, EvoRequirementLevel = 2, EvoRequirementColor = color, EffectText = main };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 5, TurnSequence = 3 };
        return (state, new GoldfishEngine(state), Cards());
    }

    [Fact]
    public void PlayerTarget_ShouldBeAskedAndHonored()
    {
        var (state, engine, cards) = NewMainPhase();
        var weak = new CardInstance(cards["ST1-02"], 1);    // DP 3000
        var strong = new CardInstance(cards["ST2-08"], 1);  // DP 7000 — AI라면 이쪽
        state.DummyOpponent.BattleArea.AddRange(new[] { weak, strong });
        var choices = new ScriptedChoices { Digimon = options => options.Single(d => d.TopCard.CardId == "ST1-02") };
        engine.PlayerChoices = choices;
        var killer = Api("CH-TARGET", "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Single(choices.Asked, q => q.StartsWith("digimon:"));
        Assert.DoesNotContain(weak, state.DummyOpponent.BattleArea);
        Assert.Contains(strong, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void WithoutPlayerChoices_TargetShouldFollowAiEvaluation()
    {
        var (state, engine, cards) = NewMainPhase();
        var weak = new CardInstance(cards["ST1-02"], 1);
        var strong = new CardInstance(cards["ST2-08"], 1);
        state.DummyOpponent.BattleArea.AddRange(new[] { weak, strong });
        var killer = Api("CH-AITARGET", "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Contains(weak, state.DummyOpponent.BattleArea);
        Assert.DoesNotContain(strong, state.DummyOpponent.BattleArea);   // 위협도가 가장 높은 디지몬
    }

    [Fact]
    public void SingleCandidate_ShouldNotAsk()
    {
        var (state, engine, cards) = NewMainPhase();
        var only = new CardInstance(cards["ST1-02"], 1);
        state.DummyOpponent.BattleArea.Add(only);
        var choices = new ScriptedChoices();
        engine.PlayerChoices = choices;
        var killer = Api("CH-SINGLE", "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Empty(choices.Asked);
        Assert.Empty(state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void OptionDraggedOntoTarget_ShouldUseThatTargetWithoutAsking()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 10;
        state.Player.Tamers.Add(cards["ST1-12"]);   // 옵션 색 조건 (레드)
        var a = new CardInstance(cards["ST1-02"], 1);
        var b = new CardInstance(cards["ST2-08"], 1);
        state.DummyOpponent.BattleArea.AddRange(new[] { a, b });
        var choices = new ScriptedChoices();
        engine.PlayerChoices = choices;
        state.Player.Hand.Add(cards["ST1-16"]);   // 가이아 포스

        engine.PlayCard(cards["ST1-16"], a);

        Assert.Empty(choices.Asked);
        Assert.DoesNotContain(a, state.DummyOpponent.BattleArea);
        Assert.Contains(b, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void YouMay_DeclinedByPlayer_ShouldNotResolve()
    {
        var (state, engine, cards) = NewMainPhase();
        for (int i = 0; i < 2; i++) state.Player.MainDeck.Add(cards["ST1-02"]);
        var choices = new ScriptedChoices { ConfirmAnswer = false };
        engine.PlayerChoices = choices;
        var drawer = Api("CH-MAY", "[On Play] You may ＜Draw 2＞ (Draw 2 cards from your deck.)");
        state.Player.Hand.Add(drawer);

        engine.PlayCard(drawer);

        Assert.Single(choices.Asked, q => q.StartsWith("confirm:"));
        Assert.Equal(2, state.Player.MainDeck.Count);
    }

    [Fact]
    public void ChoiceEffect_ShouldUseThePlayersOption()
    {
        var (state, engine, cards) = NewMainPhase();
        for (int i = 0; i < 2; i++) state.Player.MainDeck.Add(cards["ST1-02"]);
        var choices = new ScriptedChoices { OptionAnswer = 0 };   // 추천(≪2 드로우≫) 대신 메모리 +1
        engine.PlayerChoices = choices;
        var chooser = Api("CH-OPTION", "[On Play] Activate 1 of the effects below:\n・Gain 1 memory.\n・＜Draw 2＞ (Draw 2 cards from your deck.)");
        state.Player.Hand.Add(chooser);

        engine.PlayCard(chooser);

        Assert.Single(choices.Asked, q => q.StartsWith("option:"));
        Assert.Equal(5, state.MemoryGauge);   // 5 - 1 + 1
        Assert.Equal(2, state.Player.MainDeck.Count);
    }

    [Fact]
    public void AiEvaluator_ShouldValueRemovalOverSmallBuffs()
    {
        Assert.True(AiEvaluator.EstimateSentenceValue("Delete 1 of your opponent's Digimon with 6000 DP or less.")
                    > AiEvaluator.EstimateSentenceValue("1 of your Digimon gets +2000 DP for the turn."));
        Assert.True(AiEvaluator.EstimateSentenceValue("＜Draw 2＞") > AiEvaluator.EstimateSentenceValue("Gain 1 memory."));
        Assert.True(AiEvaluator.EstimateSentenceValue("Trash 2 cards in your hand.") < 0);
    }
}
