using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>진화하기 직전의 진화 코스트 감소: "When this Digimon / one of your Digimon would digivolve (into ~), reduce the digivolution cost by N"</summary>
public class EvoCostModifierTests
{
    private static CardData Card(string id, string main = "", int level = 3, int evoCost = 3, CardType type = CardType.Digimon, string inherited = "", params string[] traits)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = CardColor.Green, Level = level, DP = level * 1000, PlayCost = 3, EvoCost = evoCost,
            EvoRequirementLevel = level - 1, EvoRequirementColor = CardColor.Green, EffectText = main, InheritedEffectText = inherited, Traits = new(traits),
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("EC-FILL" + i));
            state.DummyOpponent.MainDeck.Add(Card("EC-OFILL" + i));
        }
        return (state, new GoldfishEngine(state));
    }

    private static int PaidToEvolve(GameState state, GoldfishEngine engine, CardInstance target, CardData evo)
    {
        state.Player.Hand.Add(evo);
        int before = state.MemoryGauge;
        Assert.True(engine.EvolveDigimon(target, evo).CanExecute);
        Assert.Same(evo, target.TopCard);
        return before - state.MemoryGauge;
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    public void ThisDigimon_IntoTraitCard(bool cs, int paid)
    {
        var (state, engine) = NewMainPhase();
        var baseCard = Card("EC-BASE", "[Your Turn] [Once Per Turn] When this Digimon would digivolve into a Digimon card with the [CS] trait, reduce the digivolution cost by 2.");
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(baseCard).Level);
        var target = new CardInstance(baseCard, 1);
        state.Player.BattleArea.Add(target);
        var evo = cs ? Card("EC-EVO-CS", level: 4, traits: "CS") : Card("EC-EVO", level: 4);

        Assert.Equal(paid, engine.PreviewEvoCost(state.Player, target, evo));
        Assert.Equal(paid, PaidToEvolve(state, engine, target, evo));
    }

    [Fact]
    public void Tamer_OncePerTurn()
    {
        var (state, engine) = NewMainPhase();
        var tamer = Card("EC-TAMER", "[Your Turn] [Once Per Turn] When one of your Digimon would digivolve into a Digimon card with ＜Save＞ in its text, reduce the digivolution cost by 1.", level: 0, type: CardType.Tamer);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(tamer).Level);
        state.Player.Tamers.Add(tamer);
        var a = new CardInstance(Card("EC-A"), 1);
        var b = new CardInstance(Card("EC-B"), 1);
        state.Player.BattleArea.Add(a);
        state.Player.BattleArea.Add(b);

        Assert.Equal(2, PaidToEvolve(state, engine, a, Card("EC-SAVE-1", "＜Save＞ (When this card would be trashed, you may place it under 1 of your Tamers.)", level: 4)));
        Assert.Equal(3, PaidToEvolve(state, engine, b, Card("EC-SAVE-2", "＜Save＞ (When this card would be trashed, you may place it under 1 of your Tamers.)", level: 4)));   // 턴에 1회
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    public void IntoThisCardInHand_WithTamerSource(bool hasTamerSource, int paid)
    {
        var (state, engine) = NewMainPhase();
        var target = new CardInstance(Card("EC-STACK"), 1);
        if (hasTamerSource) target.AddBottomSource(Card("EC-TAMER-SRC", level: 0, type: CardType.Tamer));
        state.Player.BattleArea.Add(target);
        var evo = Card("EC-HAND", "When one of your Digimon with a Tamer card in its digivolution cards would digivolve into this card in your hand, reduce the memory cost of the digivolution by 2.", level: 4);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(evo).Level);

        Assert.Equal(paid, PaidToEvolve(state, engine, target, evo));
    }

    [Fact]
    public void InheritedEffect_FromDigivolutionCard()
    {
        var (state, engine) = NewMainPhase();
        var source = Card("EC-SRC", inherited: "[Your Turn] When this Digimon would digivolve, reduce the digivolution cost by 1.", level: 2);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(source).Level);
        var target = new CardInstance(Card("EC-TOP"), 1);
        target.AddBottomSource(source);
        state.Player.BattleArea.Add(target);

        Assert.Equal(2, PaidToEvolve(state, engine, target, Card("EC-EVO-4", level: 4)));
    }
}
