using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>"시큐리티에서 카드가 빠졌을 때 / 놓였을 때" 발동 시점 (체크 · 효과 파기 · ≪리커버리≫)</summary>
public class SecurityEventTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static CardData Api(string id, string main, int level = 5, int dp = 9000, CardType type = CardType.Digimon, CardColor color = CardColor.Yellow)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        var cards = Cards();
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(cards["ST1-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST2-02"]);
        }
        return (state, new GoldfishEngine(state), cards);
    }

    [Fact]
    public void OpponentSecurityRemoved_ByMyCheck_Triggers()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = new CardInstance(Api("SE-OPP-1", "[Your Turn] When your opponent's security stack is removed from, ＜Draw 1＞ (Draw 1 card from your deck.)"), 1);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(digi.TopCard).Level);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);                        // DP 1000
        int hand = state.Player.Hand.Count;

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(hand + 1, state.Player.Hand.Count);
    }

    [Fact]
    public void OncePerTurn_TriggersOnlyOnceForTwoChecks()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = new CardInstance(Api("SE-OPP-2", "＜Security A. +1＞ (This Digimon checks 1 additional security card.)\n[Your Turn] [Once Per Turn] When a card is removed from your opponent's security stack, ＜Draw 1＞ (Draw 1 card from your deck.)"), 1);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        int hand = state.Player.Hand.Count;

        engine.AttackSecurityOrDirect(digi);

        Assert.Single(state.DummyOpponent.SecurityStack);                              // 2장 체크
        Assert.Equal(hand + 1, state.Player.Hand.Count);                               // 턴에 1회
    }

    [Fact]
    public void AllySecurityRemoved_ByOwnEffect_Triggers()
    {
        var (state, engine, cards) = NewMainPhase();
        for (int i = 0; i < 3; i++) state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.Player.BattleArea.Add(new CardInstance(Api("SE-ALLY-1", "[Your Turn] When a card is removed from your security stack, gain 1 memory."), 1));
        var opt = Api("SE-ALLY-OPT", "[Main] Trash your top security card.", level: 0, dp: 0, type: CardType.Option);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(opt).Level);
        state.Player.Hand.Add(opt);

        engine.PlayCard(opt);

        Assert.Equal(2, state.Player.SecurityStack.Count);
        Assert.Equal(8 - 3 + 1, state.MemoryGauge);
    }

    [Fact]
    public void AnySecurityRemoved_FiresForEitherSide()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = new CardInstance(Api("SE-ANY-1", "[Your Turn] When security stacks are removed from, ＜Draw 1＞ (Draw 1 card from your deck.)"), 1);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(digi.TopCard).Level);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        int hand = state.Player.Hand.Count;

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(hand + 1, state.Player.Hand.Count);
    }

    [Fact]
    public void AllySecurityAdded_ByRecovery_Triggers()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(Api("SE-ADD-1", "[Your Turn] When a card is added to your security stack, gain 1 memory."), 1));
        state.Player.Hand.Add(cards["BT1-060"]);                                       // [등장 시] ≪리커버리 +1≫, 코스트 7

        engine.PlayCard(cards["BT1-060"]);

        Assert.Single(state.Player.SecurityStack);
        Assert.Equal(8 - cards["BT1-060"].PlayCost + 1, state.MemoryGauge);
    }

    [Fact]
    public void AllySecurityRemoved_DuringOpponentsAttack_Triggers()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.MemoryGauge = 0;
        for (int i = 0; i < 3; i++) state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.Player.Tamers.Add(Api("SE-TAMER", "[Opponent's Turn] When a card is removed from your security stack, ＜Draw 1＞ (Draw 1 card from your deck.)", level: 0, dp: 0, type: CardType.Tamer));
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-08"], 0));     // DP 7000 → 내 시큐리티 1장 체크
        int hand = state.Player.Hand.Count;

        engine.PassTurn();

        Assert.Equal(2, state.Player.SecurityStack.Count);
        Assert.Equal(hand + 2, state.Player.Hand.Count);                               // 테이머 효과 1장 + 내 드로우 페이즈 1장
    }
}
