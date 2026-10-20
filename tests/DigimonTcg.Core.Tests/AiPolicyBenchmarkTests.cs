using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Ai;
using DigimonTcg.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// AI 정책 승률 비교 — 추천 덱끼리, 같은 시드 · 같은 상대(플레이어 쪽 고정 봇)로 기존 AI와 개선 AI를 대전시킨다.
/// 플레이어 쪽은 공개 API로 움직이는 봇: 기존 AI와 같은 규칙(부화 · 이동, 첫 진화, 전부 시큐리티 어택, 낮은 Lv 등장)이라 같은 조건의 상대다.
/// </summary>
public class AiPolicyBenchmarkTests
{
    private readonly ITestOutputHelper _output;
    public AiPolicyBenchmarkTests(ITestOutputHelper output) => _output = output;

    public sealed record Result(string Policy, int Games, int AiWins, int PlayerWins, int Unfinished, double AvgTurns, int AiOptionUses)
    {
        public double AiWinRate => Games == 0 ? 0 : 100.0 * AiWins / Games;
    }

    /// <summary>정책 하나로 덱 조합 × 시드를 모두 돌린다 (결과는 시드로 재현된다)</summary>
    public static Result Run(Func<IAiPolicy> policy, int seedsPerPair, int maxTurns = 40)
    {
        var presets = StarterDecks.GetRecommendedDeckPresets();
        int games = 0, aiWins = 0, playerWins = 0, unfinished = 0, turns = 0, optionUses = 0;
        string name = policy().Name;
        foreach (var mine in presets)
        foreach (var theirs in presets)
        for (int seed = 0; seed < seedsPerPair; seed++)
        {
            var state = new GameState();
            var engine = new GoldfishEngine(state) { AiPolicy = policy() };
            var (main, egg) = mine.DeckFactory();
            var (oMain, oEgg) = theirs.DeckFactory();
            int gameSeed = seed * 7919 + games;
            engine.StartNewGame(main, egg, seed: gameSeed, stage: PracticeStage.Stage3_AiBattle,
                opponentMainDeck: oMain, opponentDigitamaDeck: oEgg, opponentDeckName: theirs.Name);
            engine.DecideFirstPlayer(playerGoesFirst: seed % 2 == 0);
            engine.BeginGame();

            for (int step = 0; step < 2000 && !state.IsGameOver && state.CurrentTurn <= maxTurns; step++)
                PlayerBot.Act(state, engine);

            games++;
            turns += state.CurrentTurn;
            optionUses += state.ActionLogs.Count(l => l.Contains("[AI 옵션 사용]"));
            if (!state.IsGameOver) unfinished++;
            else if (state.IsVictory) playerWins++;
            else aiWins++;
        }
        return new Result(name, games, aiWins, playerWins, unfinished, games == 0 ? 0 : (double)turns / games, optionUses);
    }

    [Fact]
    public void SmartPolicy_ShouldWinMoreThanLegacy_AgainstTheSameOpponent()
    {
        // 기본은 덱 36조합 × 시드 4 = 정책마다 144판 (몇 초). 더 크게 재려면 환경 변수 AI_BENCH_SEEDS로 시드 수를 늘린다
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("AI_BENCH_SEEDS"), out int n) && n > 0 ? n : 4;
        var legacy = Run(() => new LegacyAiPolicy(), seedsPerPair: seeds);
        var smart = Run(() => new SmartAiPolicy(), seedsPerPair: seeds);

        foreach (var r in new[] { legacy, smart })
            _output.WriteLine($"{r.Policy}: {r.Games}판 · AI 승 {r.AiWins} ({r.AiWinRate:F1}%) · 플레이어 승 {r.PlayerWins} · 미종료 {r.Unfinished} · 평균 {r.AvgTurns:F1}턴 · AI 옵션 사용 {r.AiOptionUses}번");

        Assert.Equal(legacy.Games, smart.Games);
        Assert.True(smart.AiWins > legacy.AiWins, $"개선 AI {smart.AiWins}승 ≤ 기존 AI {legacy.AiWins}승");
    }
}

/// <summary>플레이어 쪽을 공개 API로 움직이는 고정 봇 (기존 AI와 같은 규칙) — 한 번 부를 때 한 가지 행동</summary>
internal static class PlayerBot
{
    public static void Act(GameState state, GoldfishEngine engine)
    {
        var me = state.Player;
        if (state.PendingBlock is { } pending)
        {
            // 블로커가 이기면 막고, 아니면 시큐리티로 받는다 (시큐리티 0장이면 희생해서 막는다)
            var stopper = pending.Candidates.FirstOrDefault(b => b.GetCurrentDP() > pending.AttackerDp)
                          ?? (me.SecurityStack.Count == 0 ? pending.Candidates.FirstOrDefault() : null);
            engine.ResolvePendingBlock(stopper);
            return;
        }

        if (state.CurrentPhase == Phase.Breeding)
        {
            if (me.BreedingArea == null && me.DigitamaDeck.Count > 0) engine.HatchDigitama();
            else if (me.BreedingArea is { } b && b.TopCard.Level >= 3) engine.MoveFromBreedingArea();
            else engine.PassBreedingPhase();
            return;
        }

        if (state.CurrentPhase != Phase.Main)
        {
            engine.PassTurn();
            return;
        }

        var bases = me.BattleArea.ToList();
        if (me.BreedingArea != null) bases.Add(me.BreedingArea);

        // 1) 메모리를 넘기지 않는 진화
        foreach (var field in bases)
            foreach (var card in me.Hand.Where(c => c.Type == CardType.Digimon).Distinct().ToList())
                if (ActionValidator.EvolutionCost(field, card) <= state.MemoryGauge
                    && engine.EvolveDigimon(field, card).CanExecute)
                    return;

        // 2) 어택할 수 있는 디지몬은 전부 시큐리티로 (DP 높은 순)
        foreach (var attacker in me.BattleArea.Where(d => !d.IsSuspended && d.TurnPlayed < state.CurrentTurn).OrderByDescending(d => d.GetCurrentDP()).ToList())
            if (engine.AttackSecurityOrDirect(attacker).CanExecute)
                return;

        // 3) 디지몬 등장 (Lv.4 이하 중 코스트 큰 것 → 작은 것, 메모리 3까지 넘겨도 됨)
        var play = me.Hand
            .Where(c => c.Type == CardType.Digimon && c.PlayCost <= state.MemoryGauge + 3)
            .OrderByDescending(c => c.Level <= 4 ? c.PlayCost : 0).ThenBy(c => c.PlayCost)
            .FirstOrDefault();
        if (play != null && engine.PlayCard(play).CanExecute)
            return;

        // 4) 메모리를 3까지 넘기는 진화
        foreach (var field in bases)
            foreach (var card in me.Hand.Where(c => c.Type == CardType.Digimon).Distinct().ToList())
                if (ActionValidator.EvolutionCost(field, card) <= state.MemoryGauge + 3
                    && engine.EvolveDigimon(field, card).CanExecute)
                    return;

        engine.PassTurn();
    }
}
