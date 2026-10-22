using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>문장으로 만든 옵션 [메인]의 대상 종류 · 조건 (MainTarget · MainTargetFilter · MainTargetRule)</summary>
public class OptionMainTargetTests
{
    private static CardData Card(string id, string main = "", CardType type = CardType.Option, int level = 0, int dp = 0)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = CardColor.Red, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = CardColor.Red, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, CardInstance Active, CardInstance Suspended) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("OM-FILL" + i, type: CardType.Digimon, level: 3, dp: 1000));
            state.DummyOpponent.MainDeck.Add(Card("OM-OFILL" + i, type: CardType.Digimon, level: 3, dp: 1000));
        }
        state.Player.BattleArea.Add(new CardInstance(Card("OM-MINE", type: CardType.Digimon, level: 3, dp: 3000), 1));   // 옵션 색 조건
        var active = new CardInstance(Card("OM-ACTIVE", type: CardType.Digimon, level: 4, dp: 5000), 0);
        var suspended = new CardInstance(Card("OM-SUS", type: CardType.Digimon, level: 4, dp: 5000), 0) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(active);
        state.DummyOpponent.BattleArea.Add(suspended);
        return (state, new GoldfishEngine(state), active, suspended);
    }

    [Fact]
    public void SuspendedOnlyOption_RejectsActiveTargetBeforePaying()
    {
        var (state, engine, active, suspended) = NewMainPhase();
        var opt = Card("OM-DEL", "[Main] Delete 1 of your opponent's suspended Digimon.");
        Assert.Equal(TargetKind.OpponentDigimon, CardScripts.MainTargetOf(opt));
        Assert.False(CardScripts.IsValidMainTarget(opt, active));
        Assert.True(CardScripts.IsValidMainTarget(opt, suspended));
        state.Player.Hand.Add(opt);

        var refused = engine.PlayCard(opt, active);

        Assert.False(refused.CanExecute);
        Assert.Contains("레스트", refused.Reason);
        Assert.Equal(8, state.MemoryGauge);                                              // 메모리를 쓰지 않았다
        Assert.Contains(opt, state.Player.Hand);

        Assert.True(engine.PlayCard(opt, suspended).CanExecute);
        Assert.DoesNotContain(suspended, state.DummyOpponent.BattleArea);
        Assert.Contains(active, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void OwnTarget_WithoutCondition()
    {
        var opt = Card("OM-BUFF", "[Main] 1 of your Digimon gets +3000 DP for the turn.");

        Assert.Equal(TargetKind.OwnDigimon, CardScripts.MainTargetOf(opt));
        Assert.Null(CardScripts.For(opt)!.MainTargetFilter);
    }

    [Fact]
    public void AllTargets_NoMainTarget()
    {
        var opt = Card("OM-ALL", "[Main] Delete all of your opponent's Digimon with 3000 DP or less.");

        Assert.Equal(TargetKind.None, CardScripts.MainTargetOf(opt));
    }

    [Fact]
    public void DpCondition_Filter()
    {
        var (_, _, active, _) = NewMainPhase();
        var opt = Card("OM-DP", "[Main] Delete 1 of your opponent's Digimon with 4000 DP or less.");

        Assert.Equal(TargetKind.OpponentDigimon, CardScripts.MainTargetOf(opt));
        Assert.False(CardScripts.IsValidMainTarget(opt, active));                         // 5000 DP
    }
}
