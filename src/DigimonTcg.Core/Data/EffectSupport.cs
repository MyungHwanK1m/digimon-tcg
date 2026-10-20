using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Data;

/// <summary>카드 효과가 대전에서 얼마나 동작하는지</summary>
public enum EffectSupportLevel
{
    NoEffect,   // 효과 문장이 없는 카드 (바닐라)
    Full,       // 모든 절이 동작
    Partial,    // 일부 절만 동작
    None        // 효과가 있지만 동작하는 절이 없음
}

/// <summary>카드 한 장의 효과 지원 상태. 내장 카드는 절 목록 없이 판정한다 (Clauses 비어 있음).</summary>
public sealed record EffectSupportInfo(
    EffectSupportLevel Level,
    int SupportedClauses,
    int TotalClauses,
    IReadOnlyList<EffectClause> Clauses,
    bool IsBuiltIn)
{
    public IEnumerable<EffectClause> UnsupportedClauses => Clauses.Where(c => c.Support == ClauseSupport.Unsupported);

    /// <summary>바인더 배지 · 토스트용 짧은 표시 (전부 동작 · 효과 없음이면 null)</summary>
    public string? BadgeText => Level switch
    {
        EffectSupportLevel.Partial when TotalClauses > 0 => $"⚠ 일부 동작 {SupportedClauses}/{TotalClauses}",
        EffectSupportLevel.Partial => "⚠ 일부 동작",
        EffectSupportLevel.None => "⚠ 효과 미지원",
        _ => null
    };
}

/// <summary>여러 카드(덱 · 세트 · 도감 전체)의 지원 상태 합계</summary>
public sealed record EffectSupportSummary(int Cards, int Full, int Partial, int None, int NoEffect, int SupportedClauses, int TotalClauses)
{
    /// <summary>효과가 있는 카드 중 모든 절이 동작하는 비율 (0~100)</summary>
    public double FullCardRate => Cards - NoEffect > 0 ? 100.0 * Full / (Cards - NoEffect) : 100.0;

    /// <summary>절 단위 동작 비율 (0~100, API 카드 절 기준)</summary>
    public double ClauseRate => TotalClauses > 0 ? 100.0 * SupportedClauses / TotalClauses : 100.0;
}

/// <summary>
/// 카드별 효과 지원 상태 (도감 배지 · EFFECT 필터 · 카드 상세 · 덱 추가 안내 · 지원률 리포트).
/// - 내장 한글 카드: 카드별 스크립트로 구현 → 전부 동작. 단 StarterDecks.IsTextOnlyEffectCard 목록은 일부 동작.
/// - API 카드(영어 문장): KeywordDictionary.Analyze의 절 단위 결과로 판정.
/// 문장 분석은 비싸므로 카드 번호별로 캐시한다 (도감 동기화 뒤에는 ClearCache).
/// </summary>
public static class EffectSupport
{
    private static readonly ConcurrentDictionary<string, EffectSupportInfo> Cache = new(StringComparer.Ordinal);

    public static EffectSupportInfo For(CardData card) =>
        Cache.GetOrAdd(card.CardId, _ => Compute(card));

    public static void ClearCache() => Cache.Clear();

    private static EffectSupportInfo Compute(CardData card)
    {
        bool hasText = !string.IsNullOrWhiteSpace(card.EffectText) || !string.IsNullOrWhiteSpace(card.InheritedEffectText);

        if (StarterDecks.IsBuiltInCard(card.CardId))
        {
            if (!hasText)
                return new EffectSupportInfo(EffectSupportLevel.NoEffect, 0, 0, Array.Empty<EffectClause>(), IsBuiltIn: true);
            var level = StarterDecks.IsTextOnlyEffectCard(card.CardId) ? EffectSupportLevel.Partial : EffectSupportLevel.Full;
            return new EffectSupportInfo(level, 0, 0, Array.Empty<EffectClause>(), IsBuiltIn: true);
        }

        if (!hasText)
            return new EffectSupportInfo(EffectSupportLevel.NoEffect, 0, 0, Array.Empty<EffectClause>(), IsBuiltIn: false);

        List<EffectClause> clauses;
        try
        {
            clauses = KeywordDictionary.Analyze(card);
        }
        catch (Exception)
        {
            // 분석 중 예외가 나는 문장은 동작하지 않는 것으로 본다 (대전에서도 스크립트가 붙지 않음)
            clauses = new List<EffectClause> { new(false, card.EffectText, ClauseSupport.Unsupported, "문장을 분석하지 못함") };
        }

        int total = clauses.Count;
        int supported = clauses.Count(c => c.Support == ClauseSupport.Supported);
        var lvl = total == 0 ? EffectSupportLevel.NoEffect
            : supported == total ? EffectSupportLevel.Full
            : supported == 0 ? EffectSupportLevel.None
            : EffectSupportLevel.Partial;
        return new EffectSupportInfo(lvl, supported, total, clauses, IsBuiltIn: false);
    }

    public static EffectSupportSummary Summarize(IEnumerable<CardData> cards)
    {
        int count = 0, full = 0, partial = 0, none = 0, noEffect = 0, sup = 0, total = 0;
        foreach (var card in cards)
        {
            var info = For(card);
            count++;
            switch (info.Level)
            {
                case EffectSupportLevel.Full: full++; break;
                case EffectSupportLevel.Partial: partial++; break;
                case EffectSupportLevel.None: none++; break;
                default: noEffect++; break;
            }
            sup += info.SupportedClauses;
            total += info.TotalClauses;
        }
        return new EffectSupportSummary(count, full, partial, none, noEffect, sup, total);
    }
}
