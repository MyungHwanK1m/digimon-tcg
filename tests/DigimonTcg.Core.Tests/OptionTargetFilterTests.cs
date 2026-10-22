using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>옵션 [메인]의 대상 조건 — 조건에 안 맞는 디지몬에 놓으면 메모리를 쓰기 전에 막는다 (사용자 신고: "전기 충격이 액티브 디지몬에도 적용된다")</summary>
public class OptionTargetFilterTests
{
    private static readonly CardData ElectricShock = StarterDecks.GetAllCatalogCards().First(c => c.CardId == "ST4-16");
    private static readonly CardData Tamer = StarterDecks.GetAllCatalogCards().First(c => c.CardId == "ST4-14");   // 그린 — 옵션 색 조건

    private static CardData Digimon(string id) => new()
    {
        CardId = id, Name = id, Type = CardType.Digimon, Color = CardColor.Red, Level = 4, DP = 4000, PlayCost = 5,
    };

    private static (GameState State, GoldfishEngine Engine, CardInstance Active, CardInstance Rested) Setup()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, TurnSequence = 5, MemoryGauge = 5 };
        state.Player.Tamers.Add(Tamer);
        var active = new CardInstance(Digimon("OT-ACTIVE"), 1);
        var rested = new CardInstance(Digimon("OT-RESTED"), 1) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(active);
        state.DummyOpponent.BattleArea.Add(rested);
        state.Player.Hand.Add(ElectricShock);
        return (state, new GoldfishEngine(state), active, rested);
    }

    [Fact]
    public void ActiveTarget_IsRefusedBeforePaying()
    {
        var (state, engine, active, rested) = Setup();

        var result = engine.PlayCard(ElectricShock, active);

        Assert.False(result.CanExecute);
        Assert.Contains("레스트 상태인 상대 디지몬에게만", result.Reason);
        Assert.Equal(5, state.MemoryGauge);                                             // 메모리를 쓰지 않았다
        Assert.Contains(ElectricShock, state.Player.Hand);
        Assert.Contains(active, state.DummyOpponent.BattleArea);
        Assert.Contains(rested, state.DummyOpponent.BattleArea);                        // 다른 레스트 디지몬을 말없이 되돌리지 않는다
    }

    [Fact]
    public void RestedTarget_IsReturnedToHand()
    {
        var (state, engine, active, rested) = Setup();

        var result = engine.PlayCard(ElectricShock, rested);

        Assert.True(result.CanExecute);
        Assert.DoesNotContain(rested, state.DummyOpponent.BattleArea);
        Assert.Contains(active, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void IsValidMainTarget_ForScreenHighlight()
    {
        var (_, _, active, rested) = Setup();

        Assert.False(CardScripts.IsValidMainTarget(ElectricShock, active));
        Assert.True(CardScripts.IsValidMainTarget(ElectricShock, rested));
    }
}
