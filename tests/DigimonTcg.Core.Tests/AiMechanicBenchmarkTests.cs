using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Ai;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// AI 메커닉 판단 측정 — AI 쪽에만 DNA 진화 · ≪볼텍스≫ / ≪에그제큐트≫ 위주 덱을 주고, 같은 덱 · 같은 시드 · 같은 상대(PlayerBot + 추천 덱)로
/// 기존 AI와 개선 AI를 비교한다. 승률과 함께 그 판단을 실제로 한 횟수(로그)를 센다.
/// DNA 조건은 공식 한글 데이터(data/kr/cards_ko.json)에서 읽으므로 데이터가 없는 PC에서는 그 덱을 건너뛴다.
/// 기본은 시드 2(몇 초), 크게 재려면 환경 변수 AI_BENCH_SEEDS.
/// </summary>
[Collection("KoreanCardData")]
public class AiMechanicBenchmarkTests
{
    private readonly ITestOutputHelper _output;
    public AiMechanicBenchmarkTests(ITestOutputHelper output) => _output = output;

    public sealed record MechanicResult(string Policy, int Games, int AiWins, int Unfinished, int DnaEvolutions, int EndOfTurnAttacks)
    {
        public double AiWinRate => Games == 0 ? 0 : 100.0 * AiWins / Games;
    }

    [Fact]
    public void MechanicDecks_LegacyVsSmart_ShouldFinishAndReport()
    {
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("AI_BENCH_SEEDS"), out int n) && n > 0 ? n : 2;
        var catalog = LoadCatalogCopy();
        var decks = new List<(string Name, List<CardData> Main, List<CardData> Egg)>();
        if (DnaDeck(catalog) is { } dna) decks.Add(dna);
        if (EndOfTurnAttackDeck(catalog) is { } eot) decks.Add(eot);
        if (decks.Count == 0)
        {
            _output.WriteLine("메커닉 덱을 만들 카드가 없습니다 (공식 한글 데이터 · 카드 캐시 확인) — 건너뜀");
            return;
        }

        foreach (var deck in decks)
        {
            _output.WriteLine($"[{deck.Name}] 메인 {deck.Main.Count}장 · 디지타마 {deck.Egg.Count}장");
            foreach (var policy in new Func<IAiPolicy>[] { () => new LegacyAiPolicy(), () => new SmartAiPolicy() })
            {
                var r = Run(policy, deck.Main, deck.Egg, seeds);
                _output.WriteLine($"  {r.Policy}: {r.Games}판 · AI 승 {r.AiWins} ({r.AiWinRate:F1}%) · 미종료 {r.Unfinished} · DNA 진화 {r.DnaEvolutions}번 · 턴 종료 시 어택 {r.EndOfTurnAttacks}번");
                Assert.True(r.Games > 0);
            }
        }
    }

    /// <summary>AI 쪽 덱을 고정하고, 플레이어 쪽은 추천 덱 6종 × 시드로 돌린다 (선공은 시드로 번갈아)</summary>
    private static MechanicResult Run(Func<IAiPolicy> policy, List<CardData> aiMain, List<CardData> aiEgg, int seedsPerPreset)
    {
        int games = 0, aiWins = 0, unfinished = 0, dna = 0, eot = 0;
        string name = policy().Name;
        foreach (var preset in StarterDecks.GetRecommendedDeckPresets())
        for (int seed = 0; seed < seedsPerPreset; seed++)
        {
            var state = new GameState();
            var engine = new GoldfishEngine(state) { AiPolicy = policy() };
            var (main, egg) = preset.DeckFactory();
            engine.StartNewGame(main, egg, seed: seed * 7919 + games, stage: PracticeStage.Stage3_AiBattle,
                opponentMainDeck: aiMain.ToList(), opponentDigitamaDeck: aiEgg.ToList(), opponentDeckName: "메커닉 덱");
            engine.DecideFirstPlayer(playerGoesFirst: seed % 2 == 0);
            engine.BeginGame();
            for (int step = 0; step < 2000 && !state.IsGameOver && state.CurrentTurn <= 40; step++)
                PlayerBot.Act(state, engine);

            games++;
            dna += state.ActionLogs.Count(l => l.Contains("AI DNA 진화"));
            eot += state.ActionLogs.Count(l => l.Contains("턴 종료 시 어택"));
            if (!state.IsGameOver) unfinished++;
            else if (!state.IsVictory) aiWins++;
        }
        return new MechanicResult(name, games, aiWins, unfinished, dna, eot);
    }

    // =====================================================================
    // 메커닉 덱 만들기 (도감 = 실제 카드 캐시의 복사본)
    // =====================================================================

    private static List<CardData> Copies(CardData card, int count) => Enumerable.Repeat(card, count).ToList();

    /// <summary>colors의 Lv.3 ~ 5 디지몬으로 50장까지 채운다 (진화 이어 가기용)</summary>
    private static void FillToFifty(List<CardData> main, IReadOnlyList<CardData> catalog, IEnumerable<CardColor> colors)
    {
        var colorSet = colors.ToHashSet();
        foreach (var c in catalog.Where(c => c.Type == CardType.Digimon && c.Level is >= 3 and <= 5 && colorSet.Contains(c.Color) && !main.Contains(c))
                                 .OrderBy(c => c.Level).ThenBy(c => c.CardId, StringComparer.Ordinal))
        {
            if (main.Count >= 50) break;
            main.AddRange(Copies(c, Math.Min(4, 50 - main.Count)));
        }
    }

    private static List<CardData> Eggs(IReadOnlyList<CardData> catalog, CardColor color)
    {
        var egg = catalog.FirstOrDefault(c => c.Type == CardType.Digitama && c.Color == color)
                  ?? catalog.First(c => c.Type == CardType.Digitama);
        return Copies(egg, 5);
    }

    /// <summary>조그레스 조건이 있는 카드 4장 + 조건 왼쪽 · 오른쪽에 맞는 디지몬 각 4장씩 + 그 색의 진화 재료</summary>
    internal static (string, List<CardData>, List<CardData>)? DnaDeck(IReadOnlyList<CardData> catalog)
    {
        foreach (var host in catalog.Where(c => c.Type == CardType.Digimon && GoldfishEngine.HasDna(c)).OrderBy(c => c.CardId, StringComparer.Ordinal))
        {
            var req = GoldfishEngine.DnaOf(host)!;
            var digimon = catalog.Where(c => c.Type == CardType.Digimon && c.Level >= 3 && !GoldfishEngine.HasDna(c)).ToList();
            var left = digimon.Where(c => req.Left(c)).Take(3).ToList();
            var right = digimon.Where(c => req.Right(c) && !left.Contains(c)).Take(3).ToList();
            if (left.Count < 2 || right.Count < 2) continue;

            var main = Copies(host, 4);
            foreach (var c in left.Concat(right)) main.AddRange(Copies(c, 4));
            var colors = left.Concat(right).Select(c => c.Color).Append(host.Color).Distinct().ToList();
            FillToFifty(main, catalog, colors);
            return ($"DNA 덱 · {host.CardId} {host.DisplayName}", main.Take(50).ToList(), Eggs(catalog, left[0].Color));
        }
        return null;
    }

    /// <summary>≪볼텍스≫ · ≪에그제큐트≫ 디지몬이 가장 많은 색으로 그 카드 12장 + 같은 색 디지몬</summary>
    internal static (string, List<CardData>, List<CardData>)? EndOfTurnAttackDeck(IReadOnlyList<CardData> catalog)
    {
        var eotCards = catalog.Where(c => c.Type == CardType.Digimon
                                          && CardScripts.For(c)?.Keywords is { } k && (k.Contains(Keyword.Vortex) || k.Contains(Keyword.Execute)))
                              .ToList();
        if (eotCards.Count == 0) return null;
        var color = eotCards.GroupBy(c => c.Color).OrderByDescending(g => g.Count()).First().Key;
        var picked = eotCards.Where(c => c.Color == color).OrderBy(c => c.Level).Take(3).ToList();
        var main = new List<CardData>();
        foreach (var c in picked) main.AddRange(Copies(c, 4));
        FillToFifty(main, catalog, new[] { color });
        return ($"볼텍스 · 에그제큐트 덱 · {color} ({string.Join(", ", picked.Select(c => c.CardId))})", main.Take(50).ToList(), Eggs(catalog, color));
    }

    /// <summary>실제 data/cards_cache.json을 임시 폴더에 복사해서 연다 (기본 경로로 열면 쓰는 중인 파일을 건드릴 수 있다)</summary>
    internal static IReadOnlyList<CardData> LoadCatalogCopy()
    {
        string? source = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && source == null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "data", "cards_cache.json");
            if (File.Exists(candidate)) source = candidate;
        }
        string dir = Path.Combine(Path.GetTempPath(), "digimontcg-aimech-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            if (source != null)
                File.Copy(source, Path.Combine(dir, "cards_cache.json"));
            return new CardCatalogService(dir).Catalog.ToList();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
