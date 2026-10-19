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
        foreach (var preset in StarterDecks.GetRecommendedDeckPresets())
        {
            var (main, digitama) = preset.DeckFactory();
            var (valid, errors) = DeckValidator.ValidateDeck(main, digitama);
            Assert.True(valid, $"{preset.Name}: " + string.Join(", ", errors));

            var ratio = DeckValidator.CalculateRatios(main, digitama);
            Assert.All(ratio.Items, item => Assert.Equal("적정", item.StatusText));
        }
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

    [Fact]
    public void CoinToss_OpponentFirst_ShouldRunAiOpeningTurnThenPlayerDrawsOnTurnOne()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var (oppMain, oppDigitama) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, digitama, seed: 7, stage: PracticeStage.Stage3_AiBattle,
            opponentMainDeck: oppMain, opponentDigitamaDeck: oppDigitama, opponentDeckName: "테스트 상대");

        // 플레이어 선공으로 시작하면 1턴째 드로우 없이 손패 5장
        Assert.True(state.PlayerGoesFirst);
        Assert.Equal(5, state.Player.Hand.Count);
        int oppDeckBefore = state.DummyOpponent.MainDeck.Count;

        var res = engine.GiveFirstTurnToOpponent();

        Assert.True(res.CanExecute);
        Assert.False(state.PlayerGoesFirst);
        Assert.Equal(1, state.CurrentTurn);
        // 선공 AI는 첫 턴 드로우 없음 (덱 매수가 드로우로 줄지 않음)
        Assert.True(state.DummyOpponent.MainDeck.Count <= oppDeckBefore);
        Assert.Contains(state.ActionLogs, l => l.Contains("상대 AI 선공"));
        if (!state.IsGameOver)
        {
            // 후공 플레이어는 1턴째에도 드로우 → 손패 6장, 육성 페이즈에서 시작
            Assert.Equal(6, state.Player.Hand.Count);
            Assert.Equal(Phase.Breeding, state.CurrentPhase);
            Assert.True(state.MemoryGauge >= 0);
        }

        // 두 번 넘길 수는 없음
        Assert.False(engine.GiveFirstTurnToOpponent().CanExecute);
    }
}
