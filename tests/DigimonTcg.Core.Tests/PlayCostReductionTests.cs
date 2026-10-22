using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>등장 코스트 감소: 뒤에 붙는 조건 "~ if ~" · "for each ~" · 상대 필드 조건 · 이름 여러 개 · "from the hand"</summary>
public class PlayCostReductionTests
{
    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, int level = 4, int playCost = 6, CardColor color = CardColor.Red, string? name = null)
    {
        var card = new CardData
        {
            CardId = id, Name = name ?? id, Type = type, Color = color, Level = level, DP = level * 1000, PlayCost = playCost, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
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
            state.Player.MainDeck.Add(Card("PC-FILL" + i, color: CardColor.White));
            state.DummyOpponent.MainDeck.Add(Card("PC-OFILL" + i, color: CardColor.White));
        }
        return (state, new GoldfishEngine(state));
    }

    /// <summary>카드를 패에서 등장시키고 낸 메모리를 돌려준다</summary>
    private static int PaidToPlay(GameState state, GoldfishEngine engine, CardData card)
    {
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(card).Level);
        state.Player.Hand.Add(card);
        int before = state.MemoryGauge;
        Assert.True(engine.PlayCard(card).CanExecute);
        return before - state.MemoryGauge;
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    public void TrailingIfCondition(bool hasBlue, int paid)
    {
        var (state, engine) = NewMainPhase();
        if (hasBlue) state.Player.BattleArea.Add(new CardInstance(Card("PC-BLUE", color: CardColor.Blue), 1));
        var card = Card("PC-1", "When you would play this card from your hand, reduce its play cost by 1 if you have a blue Digimon in play.", playCost: 3);

        Assert.Equal(paid, PaidToPlay(state, engine, card));
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(2, 2)]
    public void ForEachOfOpponentsDigimon(int opponentDigimon, int paid)
    {
        var (state, engine) = NewMainPhase();
        for (int i = 0; i < opponentDigimon; i++) state.DummyOpponent.BattleArea.Add(new CardInstance(Card("PC-OPP" + i), 0));
        var card = Card("PC-2", "When this card would be played, reduce the play cost by 2 for each of your opponent's Digimon.");

        Assert.Equal(paid, PaidToPlay(state, engine, card));
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 6)]
    public void FromTheHand_WithNameList(bool hasMirei, int paid)
    {
        var (state, engine) = NewMainPhase();
        if (hasMirei) state.Player.Tamers.Add(Card("PC-MIREI", type: CardType.Tamer, level: 0, playCost: 2, name: "Mirei Mikagura"));
        var card = Card("PC-3", "When this card would be played from the hand, if you have [Angewomon] or [Mirei Mikagura], reduce the play cost by 3.");

        Assert.Equal(paid, PaidToPlay(state, engine, card));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 6)]
    public void OpponentSideCondition(int opponentDigimon, int paid)
    {
        var (state, engine) = NewMainPhase();
        for (int i = 0; i < opponentDigimon; i++) state.DummyOpponent.BattleArea.Add(new CardInstance(Card("PC-OPP" + i), 0));
        var card = Card("PC-4", "When this card would be played, if your opponent has 2 or more Digimon, reduce the play cost by 4.");

        Assert.Equal(paid, PaidToPlay(state, engine, card));
    }

    [Fact]
    public void WithoutOpponentZones_ConditionIsNotMet()
    {
        var (state, _) = NewMainPhase();
        for (int i = 0; i < 2; i++) state.DummyOpponent.BattleArea.Add(new CardInstance(Card("PC-OPP" + i), 0));
        var card = Card("PC-5", "When this card would be played, if your opponent has 2 or more Digimon, reduce the play cost by 4.");

        Assert.Equal(6, CardScripts.EffectivePlayCost(state.Player, card));                // 상대 필드를 모르면 줄이지 않는다
        Assert.Equal(2, CardScripts.EffectivePlayCost(state.Player, card, state.DummyOpponent));
    }
}
