using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// [등장 시] · [진화 시] · [어택 시] · [소멸 시] 효과가 대상 · 조건이 없어 아무것도 안 하면 로그를 남긴다 —
/// 조용히 넘어가면 "효과가 안 탔다"로 보인다 (사용자 신고 · 여러 덱 시뮬레이션의 로제몬 · 쥬드몬 · 엔젤우몬)
/// </summary>
public class NoEffectLogTests
{
    private static CardData Builtin(string id) => StarterDecks.GetAllCatalogCards().First(c => c.CardId == id);

    private static (GameState State, GoldfishEngine Engine, CardInstance Base) Setup()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, TurnSequence = 5, MemoryGauge = 5 };
        var filler = new CardData { CardId = "NE-FILL", Name = "NE-FILL", Type = CardType.Digimon, Color = CardColor.Green, Level = 3, DP = 3000, PlayCost = 3 };
        for (int i = 0; i < 5; i++) state.Player.MainDeck.Add(filler);
        var lv5 = new CardInstance(StarterDecks.GetAllCatalogCards().First(c => c.Type == CardType.Digimon && c.Level == 5 && c.Color == CardColor.Green), 1);
        state.Player.BattleArea.Add(lv5);
        return (state, new GoldfishEngine(state), lv5);
    }

    [Fact]
    public void NoTarget_LogsThatNothingHappened()
    {
        var (state, engine, baseDigimon) = Setup();                                     // 상대 디지몬 없음
        var rosemon = Builtin("ST4-12");
        state.Player.Hand.Add(rosemon);

        Assert.True(engine.EvolveDigimon(baseDigimon, rosemon).CanExecute);

        Assert.Contains(state.ActionLogs, l => l.Contains("[진화 시] 로제몬: 대상 · 조건에 맞는 것이 없어 효과가 일어나지 않았습니다"));
    }

    [Fact]
    public void EffectHappened_NoExtraLog()
    {
        var (state, engine, baseDigimon) = Setup();
        state.DummyOpponent.BattleArea.Add(new CardInstance(new CardData { CardId = "NE-FOE", Name = "NE-FOE", Type = CardType.Digimon, Level = 3, DP = 3000 }, 1));
        var rosemon = Builtin("ST4-12");
        state.Player.Hand.Add(rosemon);

        engine.EvolveDigimon(baseDigimon, rosemon);

        Assert.Contains(state.ActionLogs, l => l.Contains("어택 · 블록 불가"));
        Assert.DoesNotContain(state.ActionLogs, l => l.Contains("효과가 일어나지 않았습니다"));
    }
}
