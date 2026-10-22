using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>"When an attack target is switched" · "change the attack target to 1 of your ~ Digimon" · "This Digimon's attack target can't change"</summary>
public class AttackTargetSwitchTests
{
    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, int level = 4, int dp = 6000, CardColor color = CardColor.Red)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 5;
        for (int i = 0; i < 8; i++)
        {
            state.Player.MainDeck.Add(Card("AT-FILL" + i, level: 3, dp: 1000, color: CardColor.White));
            state.DummyOpponent.MainDeck.Add(Card("AT-OFILL" + i, level: 3, dp: 1000, color: CardColor.White));
        }
        for (int i = 0; i < 3; i++)
        {
            state.Player.SecurityStack.Add(Card("AT-SEC" + i, level: 3, dp: 1000, color: CardColor.White));
            state.DummyOpponent.SecurityStack.Add(Card("AT-OSEC" + i, level: 3, dp: 1000, color: CardColor.White));
        }
        return (state, new GoldfishEngine(state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raid_FiresTargetSwitched_UnlessLocked(bool locked)
    {
        var (state, engine) = NewMainPhase();
        var tamer = Card("AT-TAMER", "[Your Turn] When an attack target is switched, gain 1 memory.", CardType.Tamer, level: 0, dp: 0);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(tamer).Level);
        state.Player.Tamers.Add(tamer);
        var raider = new CardInstance(Card("AT-RAIDER-" + locked, locked ? "＜Raid＞\n[Your Turn] This Digimon's attack target can't change." : "＜Raid＞", dp: 9000), 1);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(raider.TopCard).Level);
        state.Player.BattleArea.Add(raider);
        var victim = new CardInstance(Card("AT-VICTIM", level: 5, dp: 4000), 0);   // 액티브 Lv.5 — ≪돌진≫ 추천 대상
        state.DummyOpponent.BattleArea.Add(victim);

        engine.AttackSecurityOrDirect(raider);

        Assert.Equal(locked, state.DummyOpponent.BattleArea.Contains(victim));          // 바꿨으면 배틀해서 소멸
        Assert.Equal(locked ? 2 : 3, state.DummyOpponent.SecurityStack.Count);          // 잠겼으면 시큐리티 체크
        Assert.Equal(locked ? 5 : 6, state.MemoryGauge);                                // 대상이 바뀌었을 때 메모리 +1
    }

    [Fact]
    public void ChangeAttackTargetToOwnSuspendedDigimon()
    {
        var (state, engine) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        var tamer = Card("AT-GUARD", "[Opponent's Turn] When an opponent's Digimon attacks, change the attack target to 1 of your suspended Digimon.", CardType.Tamer, level: 0, dp: 0);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(tamer).Level);
        state.Player.Tamers.Add(tamer);
        var wall = new CardInstance(Card("AT-WALL", dp: 15000), 1) { IsSuspended = true };
        state.Player.BattleArea.Add(wall);
        var aiAttacker = new CardInstance(Card("AT-AI", dp: 7000), 0);
        state.DummyOpponent.BattleArea.Add(aiAttacker);

        engine.PassTurn();                                                               // AI가 시큐리티를 어택 → 대상을 레스트 상태의 내 디지몬으로

        Assert.Equal(3, state.Player.SecurityStack.Count);
        Assert.DoesNotContain(aiAttacker, state.DummyOpponent.BattleArea);              // 15000 디지몬과 배틀해서 소멸
        Assert.Contains(wall, state.Player.BattleArea);
    }
}
