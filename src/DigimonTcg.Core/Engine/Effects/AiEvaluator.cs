using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// AI 평가 함수 (효과 확장 4단계) — 효과의 대상 · 선택지를 고를 때 AI가 쓰는 점수, 플레이어에게 보여 줄 추천 답.
/// 엔진은 언제나 이 추천을 함께 넘기고, AI 쪽 선택은 이 추천을 그대로 쓴다.
/// </summary>
public static class AiEvaluator
{
    /// <summary>
    /// 디지몬의 위협도: DP가 가장 크고, 같으면 Lv · ≪블로커≫ · 진화원 · 액티브(다음 턴 어택 가능) 순.
    /// 상대에게 불리한 효과(소멸 · DP − · 레스트 · 패로 …)는 위협도가 높은 것부터, 자신에게 유리한 효과도 가장 강한 디지몬부터.
    /// </summary>
    public static double ThreatScore(CardInstance d) =>
        d.GetCurrentDP()
        + d.TopCard.Level * 0.1
        + (d.HasBlocker ? 0.05 : 0)
        + Math.Min(d.EvolutionSources.Count, 9) * 0.001
        + (d.IsSuspended ? 0 : 0.0001);

    /// <summary>후보를 추천 순서로 (preferred가 있으면 맨 앞)</summary>
    public static List<CardInstance> Rank(IEnumerable<CardInstance> candidates, CardInstance? preferred = null) =>
        candidates
            .OrderByDescending(d => ReferenceEquals(d, preferred))
            .ThenByDescending(d => d.GetCurrentDP())
            .ThenByDescending(d => d.TopCard.Level)
            .ThenByDescending(ThreatScore)
            .ToList();

    /// <summary>
    /// 효과 문장 하나가 효과 주인에게 얼마나 이득인지 대략의 점수 (고르는 효과에서 AI가 선택지를 고를 때).
    /// 상대 디지몬 제거 &gt; 드로우 · 메모리 &gt; 강화 순으로, 비용 · 불리한 문장은 깎는다.
    /// </summary>
    public static double EstimateSentenceValue(string sentence)
    {
        string s = sentence.ToLowerInvariant();
        double v = 0;
        int N(string pattern) => Regex.Match(s, pattern) is { Success: true } m && int.TryParse(m.Groups[1].Value, out int n) ? n : 1;

        bool opponent = s.Contains("opponent");
        if (Regex.IsMatch(s, @"\bdelete\b")) v += opponent ? 6 : -4;
        if (s.Contains("to the hand") || s.Contains("to its owner's hand")) v += opponent ? 4.5 : 2;
        if (s.Contains("bottom of") && s.Contains("deck") && opponent) v += 5;
        if (Regex.IsMatch(s, @"＜de-digivolve (\d+)＞")) v += 2.5 * N(@"＜de-digivolve (\d+)＞");
        if (Regex.IsMatch(s, @"-(\d+) dp")) v += (opponent ? 1 : -1) * N(@"-(\d+) dp") / 2000.0;
        if (Regex.IsMatch(s, @"\+(\d+) dp")) v += N(@"\+(\d+) dp") / 3000.0;
        if (Regex.IsMatch(s, @"\bsuspend\b") && opponent) v += 2;
        if (s.Contains("unsuspend")) v += 1.5;
        if (Regex.IsMatch(s, @"＜draw (\d+)＞")) v += 1.6 * N(@"＜draw (\d+)＞");
        if (Regex.IsMatch(s, @"gain (\d+) memory")) v += 1.3 * N(@"gain (\d+) memory");
        if (Regex.IsMatch(s, @"lose (\d+) memory")) v -= 1.3 * N(@"lose (\d+) memory");
        if (Regex.IsMatch(s, @"＜recovery \+(\d+)")) v += 3 * N(@"＜recovery \+(\d+)");
        if (s.Contains("security") && s.Contains("trash") && opponent) v += 3.5;
        if (Regex.IsMatch(s, @"\bplay\b") && s.Contains("without paying")) v += 4;
        if (s.Contains("reveal the top")) v += 1.5;
        if (s.Contains("from your trash to") && s.Contains("hand")) v += 1.5;
        if (Regex.IsMatch(s, @"gains? ＜")) v += 1.2;
        if (s.Contains("can't attack") || s.Contains("can't block")) v += opponent ? 2 : -2;
        if (Regex.IsMatch(s, @"trash (\d+) cards? in your hand")) v -= 1.2 * N(@"trash (\d+) cards? in your hand");
        return v;
    }
}
