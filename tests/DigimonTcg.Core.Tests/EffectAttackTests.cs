using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과로 하는 어택: "1 of your Digimon may attack" · "~ may attack your opponent's Digimon" · "~ may attack without suspending" (플레이어 · AI)</summary>
public class EffectAttackTests
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

    private static (GameState State, GoldfishEngine Engine, CardInstance Attacker) NewMainPhase(int turnPlayed = 1)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 8; i++)
        {
            state.Player.MainDeck.Add(Card("EA-FILL" + i, level: 3, dp: 1000, color: CardColor.White));
            state.DummyOpponent.MainDeck.Add(Card("EA-OFILL" + i, level: 3, dp: 1000, color: CardColor.White));
        }
        for (int i = 0; i < 3; i++) state.DummyOpponent.SecurityStack.Add(Card("EA-OSEC" + i, level: 3, dp: 1000, color: CardColor.White));
        var attacker = new CardInstance(Card("EA-ATTACKER"), turnPlayed);
        state.Player.BattleArea.Add(attacker);
        return (state, new GoldfishEngine(state), attacker);
    }

    private static void Use(GameState state, GoldfishEngine engine, string main)
    {
        var opt = Card("EA-OPT-" + main.GetHashCode(), main, CardType.Option, level: 0, dp: 0);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(opt).Level);
        state.Player.Hand.Add(opt);
        engine.PlayCard(opt);
    }

    [Fact]
    public void MayAttack_PlayerAttacksSecurity()
    {
        var (state, engine, attacker) = NewMainPhase();

        Use(state, engine, "[Main] 1 of your Digimon may attack.");

        Assert.Equal(2, state.DummyOpponent.SecurityStack.Count);
        Assert.True(attacker.IsSuspended);
    }

    [Fact]
    public void MayAttack_NotOnTheTurnItWasPlayed()
    {
        var (state, engine, attacker) = NewMainPhase(turnPlayed: 2);

        Use(state, engine, "[Main] 1 of your Digimon may attack.");

        Assert.Equal(3, state.DummyOpponent.SecurityStack.Count);
        Assert.False(attacker.IsSuspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MayAttackOpponentsDigimon_NeedsSuspendedTarget(bool targetSuspended)
    {
        var (state, engine, attacker) = NewMainPhase();
        var victim = new CardInstance(Card("EA-VICTIM", level: 3, dp: 3000), 0) { IsSuspended = targetSuspended };
        state.DummyOpponent.BattleArea.Add(victim);

        Use(state, engine, "[Main] 1 of your Digimon may attack your opponent's Digimon.");

        Assert.Equal(!targetSuspended, state.DummyOpponent.BattleArea.Contains(victim));   // 레스트 상대 디지몬만 어택 대상
        Assert.Equal(3, state.DummyOpponent.SecurityStack.Count);                           // 시큐리티는 어택하지 않는다
        Assert.Equal(targetSuspended, attacker.IsSuspended);
    }

    [Fact]
    public void MayAttackWithoutSuspending_StaysActive()
    {
        var (state, engine, attacker) = NewMainPhase();

        Use(state, engine, "[Main] 1 of your Digimon may attack without suspending.");

        Assert.Equal(2, state.DummyOpponent.SecurityStack.Count);
        Assert.False(attacker.IsSuspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AiEndOfTurn_MayAttackWithoutSuspending(bool hasEffect)
    {
        var (state, engine, _) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        for (int i = 0; i < 4; i++) state.Player.SecurityStack.Add(Card("EA-SEC" + i, level: 3, dp: 1000, color: CardColor.White));
        var ai = new CardInstance(Card("EA-AI-" + hasEffect, hasEffect ? "[End of Your Turn] This Digimon may attack without suspending." : "", dp: 9000), 0);
        if (hasEffect) Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(ai.TopCard).Level);
        state.DummyOpponent.BattleArea.Add(ai);
        state.Player.BattleArea.Clear();

        engine.PassTurn();                                                              // AI 턴: 메인에서 어택 → 턴 종료 시 한 번 더

        Assert.Equal(hasEffect ? 2 : 3, state.Player.SecurityStack.Count);
    }
}
