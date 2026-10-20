using System;
using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>카드별 효과 지원 상태 (도감 배지 · EFFECT 필터 · 카드 상세 · 지원률 리포트)</summary>
public class EffectSupportTests
{
    /// <summary>API 카드처럼 영어 효과 문장을 가진 카드 (지원 상태는 카드 번호로 캐시되므로 번호는 테스트마다 달라야 한다)</summary>
    private static CardData Api(string id, string main = "", string inherited = "") => new()
    {
        CardId = id, Name = id, Type = CardType.Digimon, Color = CardColor.Red, Level = 4, DP = 5000, PlayCost = 4, EvoCost = 2,
        EvoRequirementLevel = 3, EvoRequirementColor = CardColor.Red, EffectText = main, InheritedEffectText = inherited
    };

    private const string Blocker = "＜Blocker＞ (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)";
    private const string Nonsense = "[Main] Shuffle the moon into your opponent's breeding area and declare a new season.";

    [Fact]
    public void BuiltInCards_ShouldBeFull_ExceptTheTextOnlyList()
    {
        foreach (var card in StarterDecks.GetAllCatalogCards())
        {
            var info = EffectSupport.For(card);
            Assert.True(info.IsBuiltIn);
            bool hasText = !string.IsNullOrWhiteSpace(card.EffectText) || !string.IsNullOrWhiteSpace(card.InheritedEffectText);
            var expected = !hasText ? EffectSupportLevel.NoEffect
                : StarterDecks.IsTextOnlyEffectCard(card.CardId) ? EffectSupportLevel.Partial
                : EffectSupportLevel.Full;
            Assert.Equal(expected, info.Level);
        }
    }

    [Fact]
    public void ApiCard_WithOnlyKeywordClause_ShouldBeFull()
    {
        var info = EffectSupport.For(Api("ES-FULL", main: Blocker));

        Assert.Equal(EffectSupportLevel.Full, info.Level);
        Assert.Equal(1, info.TotalClauses);
        Assert.Equal(1, info.SupportedClauses);
        Assert.Null(info.BadgeText);
        Assert.False(info.IsBuiltIn);
    }

    [Fact]
    public void ApiCard_WithSupportedAndUnsupportedClauses_ShouldBePartialWithReasons()
    {
        var info = EffectSupport.For(Api("ES-PARTIAL", main: Blocker + "\n" + Nonsense));

        Assert.Equal(EffectSupportLevel.Partial, info.Level);
        Assert.Equal(2, info.TotalClauses);
        Assert.Equal(1, info.SupportedClauses);
        Assert.Equal("⚠ 일부 동작 1/2", info.BadgeText);
        var unsupported = Assert.Single(info.UnsupportedClauses);
        Assert.Contains("moon", unsupported.Text);
        Assert.False(string.IsNullOrWhiteSpace(unsupported.Note));   // 동작하지 않는 이유
    }

    [Fact]
    public void ApiCard_WithOnlyUnsupportedClauses_ShouldBeNone()
    {
        var info = EffectSupport.For(Api("ES-NONE", main: Nonsense));

        Assert.Equal(EffectSupportLevel.None, info.Level);
        Assert.Equal(0, info.SupportedClauses);
        Assert.Equal("⚠ 효과 미지원", info.BadgeText);
    }

    [Fact]
    public void ApiCard_WithoutText_ShouldBeNoEffect()
    {
        var info = EffectSupport.For(Api("ES-VANILLA"));

        Assert.Equal(EffectSupportLevel.NoEffect, info.Level);
        Assert.Null(info.BadgeText);
    }

    [Fact]
    public void Level_ShouldAgreeWithAnalyze()
    {
        var card = Api("ES-AGREE",
            main: "＜Rush＞ ＜Piercing＞ (This Digimon can attack the turn it comes into play.)\n[On Play] [When Digivolving] ＜De-Digivolve 1＞ 1 of your opponent's Digimon. (Trash the top card.)\n[Opponent's Turn] When an opponent's Digimon is played, gain 1 memory.",
            inherited: "[Your Turn] This Digimon gets +1000 DP.");
        var clauses = KeywordDictionary.Analyze(card);

        var info = EffectSupport.For(card);

        Assert.Equal(clauses.Count, info.TotalClauses);
        Assert.Equal(clauses.Count(c => c.Support == ClauseSupport.Supported), info.SupportedClauses);
    }

    [Fact]
    public void Results_ShouldBeCachedByCardId_UntilCleared()
    {
        var card = Api("ES-CACHE", main: Blocker);
        var first = EffectSupport.For(card);
        Assert.Same(first, EffectSupport.For(card));

        EffectSupport.ClearCache();
        Assert.NotSame(first, EffectSupport.For(card));
    }

    [Fact]
    public void Summarize_ShouldCountLevelsAndClauses()
    {
        var cards = new[]
        {
            Api("ES-SUM-1", main: Blocker),
            Api("ES-SUM-2", main: Blocker + "\n" + Nonsense),
            Api("ES-SUM-3", main: Nonsense),
            Api("ES-SUM-4")
        };

        var s = EffectSupport.Summarize(cards);

        Assert.Equal((4, 1, 1, 1, 1), (s.Cards, s.Full, s.Partial, s.None, s.NoEffect));
        Assert.Equal((2, 4), (s.SupportedClauses, s.TotalClauses));
        Assert.Equal(50.0, s.ClauseRate);
        Assert.Equal(100.0 / 3, s.FullCardRate, precision: 6);
    }

    [Fact]
    public void WholeCatalog_ShouldAnalyzeWithoutErrors()
    {
        // 실제 data/cards_cache.json을 임시 폴더에 복사해서 연다
        // (기본 경로로 열면 다른 세션 · 앱이 쓰는 중에 읽기가 실패할 때 도감 파일을 건드릴 수 있다)
        string? source = FindRepoCardCache();
        string dir = Path.Combine(Path.GetTempPath(), "digimontcg-effectsupport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            if (source != null)
                File.Copy(source, Path.Combine(dir, "cards_cache.json"));
            var catalog = new CardCatalogService(dir);
            if (source != null)
                Assert.True(catalog.Catalog.Count > 1000, $"복사한 도감을 읽지 못함 ({catalog.Catalog.Count}장)");

            var s = EffectSupport.Summarize(catalog.Catalog);

            Assert.Equal(catalog.Catalog.Count, s.Cards);
            Assert.True(s.Full > 0);
            Assert.InRange(s.ClauseRate, 0, 100);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string? FindRepoCardCache()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "data", "cards_cache.json");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
