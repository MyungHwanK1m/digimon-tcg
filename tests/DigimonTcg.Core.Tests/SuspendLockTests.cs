using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>주인을 말하지 않은 대상("suspend 1 Digimon") · "If this effect suspended your Digimon" · "can't unsuspend / can't suspend until ~"</summary>
public class SuspendLockTests
{
    /// <summary>대상을 고를 때 자신의 디지몬을 고르는 플레이어</summary>
    private sealed class PickOwn : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => true;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) =>
            options.FirstOrDefault(o => state.Player.BattleArea.Contains(o)) ?? recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Card(string id, string main = "", CardType type = CardType.Option, int level = 0, int dp = 0, CardColor color = CardColor.Purple)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, CardInstance Mine, CardInstance Theirs) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 8; i++)
        {
            state.Player.MainDeck.Add(Card("SL-FILL" + i, type: CardType.Digimon, level: 3, dp: 1000, color: CardColor.Red));
            state.DummyOpponent.MainDeck.Add(Card("SL-OFILL" + i, type: CardType.Digimon, level: 3, dp: 1000, color: CardColor.Red));
        }
        var mine = new CardInstance(Card("SL-MINE", type: CardType.Digimon, level: 4, dp: 5000), 1);
        var theirs = new CardInstance(Card("SL-THEIRS", type: CardType.Digimon, level: 5, dp: 9000), 0);
        state.Player.BattleArea.Add(mine);
        state.DummyOpponent.BattleArea.Add(theirs);
        return (state, new GoldfishEngine(state), mine, theirs);
    }

    private static void Use(GameState state, GoldfishEngine engine, string main)
    {
        var opt = Card("SL-OPT-" + main.GetHashCode(), main);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(opt).Level);
        state.Player.Hand.Add(opt);
        engine.PlayCard(opt);
    }

    [Fact]
    public void SuspendOneDigimon_DefaultsToOpponentsDigimon()
    {
        var (state, engine, mine, theirs) = NewMainPhase();

        Use(state, engine, "[Main] Suspend 1 Digimon.");

        Assert.True(theirs.IsSuspended);
        Assert.False(mine.IsSuspended);
    }

    [Fact]
    public void UnsuspendOneDigimon_DefaultsToOwnDigimon()
    {
        var (state, engine, mine, theirs) = NewMainPhase();
        mine.IsSuspended = true;
        theirs.IsSuspended = true;

        Use(state, engine, "[Main] Unsuspend 1 Digimon.");

        Assert.False(mine.IsSuspended);
        Assert.True(theirs.IsSuspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfThisEffectSuspendedYourDigimon(bool pickOwn)
    {
        var (state, engine, mine, theirs) = NewMainPhase();
        if (pickOwn) engine.PlayerChoices = new PickOwn();

        Use(state, engine, "[Main] You may suspend 1 Digimon. If this effect suspended your Digimon, gain 2 memory.");

        Assert.Equal(pickOwn, mine.IsSuspended);
        Assert.Equal(!pickOwn, theirs.IsSuspended);
        Assert.Equal(8 - 3 + (pickOwn ? 2 : 0), state.MemoryGauge);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CantUnsuspend_StaysSuspendedThroughOpponentsUnsuspendPhase(bool locked)
    {
        var (state, engine, _, theirs) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        for (int i = 0; i < 3; i++) state.Player.SecurityStack.Add(Card("SL-SEC" + i, type: CardType.Digimon, level: 3, dp: 1000));
        theirs.IsSuspended = true;
        if (locked) Use(state, engine, "[Main] 1 of your opponent's Digimon can't unsuspend until the end of their turn.");

        engine.PassTurn();                                                              // 상대 턴(액티브 페이즈) → 내 턴

        Assert.True(theirs.IsSuspended);                                                // 잠겼으면 레스트 그대로, 아니면 액티브가 된 뒤 어택해서 레스트
        Assert.Equal(locked ? 3 : 2, state.Player.SecurityStack.Count);                 // 잠겼으면 레스트라서 어택하지 못했다
    }

    [Fact]
    public void CantUnsuspend_BlocksUnsuspendEffects()
    {
        var (state, engine, mine, _) = NewMainPhase();
        mine.IsSuspended = true;
        mine.CannotUnsuspendUntil = state.TurnSequence + 1;

        Use(state, engine, "[Main] Unsuspend 1 of your Digimon.");

        Assert.True(mine.IsSuspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CantSuspend_PreventsAttack(bool locked)
    {
        var (state, engine, _, theirs) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.MemoryGauge = 3;
        for (int i = 0; i < 3; i++) state.Player.SecurityStack.Add(Card("SL-SEC" + i, type: CardType.Digimon, level: 3, dp: 1000));
        if (locked) Use(state, engine, "[Main] 1 of your opponent's Digimon can't suspend until their turn ends.");

        engine.PassTurn();

        Assert.Equal(locked ? 3 : 2, state.Player.SecurityStack.Count);                 // 레스트가 될 수 없으면 어택하지 못한다
        Assert.Equal(!locked, theirs.IsSuspended);
    }

    [Fact]
    public void CantSuspend_PreventsBlockAndSuspendEffects()
    {
        var (state, engine, mine, _) = NewMainPhase();
        var blocker = new CardInstance(Card("SL-BLOCKER", "＜Blocker＞", CardType.Digimon, level: 4, dp: 6000), 0);
        state.DummyOpponent.BattleArea.Add(blocker);
        Assert.True(EffectRules.CanBlockAgainst(state, blocker, mine));
        blocker.CannotSuspendUntil = state.TurnSequence + 1;

        Assert.False(EffectRules.CanBlockAgainst(state, blocker, mine));
        Use(state, engine, "[Main] Suspend all of your opponent's Digimon.");
        Assert.False(blocker.IsSuspended);
    }
}
