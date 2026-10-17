using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.DeckBuilder;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

public class RuleEngineTests
{
    [Fact]
    public void StarterDeckPresets_ShouldPassDeckValidationAndGoldenRatio()
    {
        var (st1Main, st1Digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var (valid1, errors1) = DeckValidator.ValidateDeck(st1Main, st1Digitama);
        Assert.True(valid1, string.Join(", ", errors1));

        var ratio1 = DeckValidator.CalculateRatios(st1Main, st1Digitama);
        Assert.All(ratio1.Items, item => Assert.Equal("적정", item.StatusText));

        var (st2Main, st2Digitama) = StarterDecks.CreateSt2CocytusBluePreset();
        var (valid2, errors2) = DeckValidator.ValidateDeck(st2Main, st2Digitama);
        Assert.True(valid2, string.Join(", ", errors2));
    }

    [Fact]
    public void ActionValidator_ShouldBlockSummoningSicknessAndWrongPhase()
    {
        var state = new GameState { CurrentTurn = 1, CurrentPhase = Phase.Breeding, MemoryGauge = 3 };
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        // 육성 페이즈에는 등장 불가
        var playRes = ActionValidator.ValidatePlay(state, cards["ST1-03"]);
        Assert.False(playRes.CanExecute);

        // 메인 페이즈 전환 후 등장한 디지몬은 소환 멀미로 공격 불가
        state.CurrentPhase = Phase.Main;
        var engine = new GoldfishEngine(state);
        engine.PlayCard(cards["ST1-02"]);

        var playedDigimon = state.Player.BattleArea.Single();
        var attackRes = ActionValidator.ValidateAttack(state, playedDigimon);
        Assert.False(attackRes.CanExecute);
        Assert.Contains("소환 멀미", attackRes.Reason);
    }

    [Fact]
    public void GoldfishEngine_EvolutionShouldDrawBonusCardAndResetMemoryOnTurnEnd()
    {
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        engine.StartNewGame(main, digitama, seed: 10);

        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);
        state.Player.Hand.Clear();
        state.Player.Hand.Add(cards["ST1-03"]); // 아구몬 (진화 0)
        state.Player.Hand.Add(cards["ST1-07"]); // 그레이몬 (진화 2)
        state.Player.Hand.Add(cards["ST1-09"]); // 메탈그레이몬 (진화 3)

        engine.HatchDigitama();
        Assert.Equal(Phase.Main, state.CurrentPhase);

        int handBefore = state.Player.Hand.Count;
        var evo1 = engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-03"]);
        Assert.True(evo1.CanExecute);
        Assert.Equal(handBefore, state.Player.Hand.Count);

        engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-07"]); // 메모리: 3 -> 1
        Assert.Equal(1, state.MemoryGauge);

        engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-09"]); // 메모리: 1 - 3 = -2 -> 턴 종료 및 2턴 +3 리셋
        Assert.Equal(2, state.CurrentTurn);
        Assert.Equal(3, state.MemoryGauge);
        Assert.Equal(Phase.Breeding, state.CurrentPhase);
    }

    [Fact]
    public void SecurityBattle_ShouldDestroyAttackerWhenSecurityDpIsEqualOrHigher()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3 };
        var engine = new GoldfishEngine(state);
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        // 공격자: 아구몬 (DP 2000, 이전 턴 등장)
        var attacker = new CardInstance(cards["ST1-03"], currentTurn: 1);
        state.Player.BattleArea.Add(attacker);

        // 더미 시큐리티: 그레이몬 (DP 4000)
        state.DummyOpponent.SecurityStack.Add(cards["ST1-07"]);

        var result = engine.AttackSecurityOrDirect(attacker);
        Assert.True(result.CanExecute);
        Assert.Empty(state.Player.BattleArea); // 공격자 소멸 확인
        Assert.Empty(state.DummyOpponent.SecurityStack); // 시큐리티도 트래시로 이동 확인
        Assert.Contains(cards["ST1-03"], state.Player.Trash);
    }

    [Fact]
    public void DirectAttack_WhenSecurityIsEmpty_ShouldTriggerVictory()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, MemoryGauge = 3 };
        var engine = new GoldfishEngine(state);
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        var attacker = new CardInstance(cards["ST1-11"], currentTurn: 2);
        state.Player.BattleArea.Add(attacker);
        state.DummyOpponent.SecurityStack.Clear(); // 시큐리티 0장

        var result = engine.AttackSecurityOrDirect(attacker);
        Assert.True(result.CanExecute);
        Assert.True(state.IsGameOver);
        Assert.True(state.IsVictory);
    }

    [Fact]
    public void CardCatalogService_ShouldFilterByColorLevelAndKeyword()
    {
        var catalog = new CardCatalogService();
        var redLv6 = catalog.SearchCards(color: CardColor.Red, level: 6);
        Assert.NotEmpty(redLv6);
        Assert.Contains(redLv6, c => c.CardId == "ST1-11" && c.Name == "워그레이몬");

        var blueCards = catalog.SearchCards(color: CardColor.Blue);
        Assert.NotEmpty(blueCards);
        Assert.All(blueCards, c => Assert.Equal(CardColor.Blue, c.Color));
    }
}
