using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 효과 문장의 대상 구절 — "1 of your opponent's Digimon with 4000 DP or less", "all of your opponent's level 4 or lower Digimon", "this Digimon" …
/// 파서가 만들고, 효과를 처리할 때 Resolve로 실제 디지몬을 고른다.
/// </summary>
public sealed class TargetSpec
{
    public bool IsThis { get; private init; }
    /// <summary>"that Digimon" · "it" — 앞 문장의 대상 (EffectContext.LastTargets, 없으면 이 디지몬)</summary>
    public bool IsThat { get; private init; }
    public bool Opponent { get; private init; }
    /// <summary>고르는 수 (0 = 전부)</summary>
    public int Count { get; private init; } = 1;
    public List<Func<CardInstance, bool>> Filters { get; } = new();
    /// <summary>효과 상황이 필요한 조건 (예: "with as much or less DP as this Digimon")</summary>
    public List<Func<EffectContext, CardInstance, bool>> ContextFilters { get; } = new();
    /// <summary>"가장 DP가 높은 / 낮은" 같은 고르기 기준 (null이면 효과 종류에 따라 엔진이 고른다)</summary>
    public Func<IEnumerable<CardInstance>, IEnumerable<CardInstance>>? Selector { get; private set; }
    public string Text { get; private init; } = string.Empty;

    private static readonly Regex Head = new(
        @"^(?:(?<count>\d+|all|up to \d+) of )?(?<side>your opponent's|your|their|its owner's)\s+(?<adj>(?:(?:unsuspended|suspended|(?:lowest|highest) (?:level|DP|play cost)|level \d+(?: or (?:lower|higher|more|less))?|\[[^\]]+\](?: trait)?)\s+)*)(?<kind>Digimon or Tamers|Digimon|Security Digimon)(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>대상 구절을 읽는다. 읽지 못하면 null</summary>
    public static TargetSpec? Parse(string phrase)
    {
        phrase = phrase.Trim().TrimEnd('.').Trim();
        if (Regex.IsMatch(phrase, @"^this Digimon$", RegexOptions.IgnoreCase))
            return new TargetSpec { IsThis = true, Text = phrase };
        if (Regex.IsMatch(phrase, @"^(?:it|that Digimon|those Digimon|them|that card)$", RegexOptions.IgnoreCase))
            return new TargetSpec { IsThat = true, Count = 0, Text = phrase };

        var m = Head.Match(phrase);
        if (!m.Success || m.Groups["kind"].Value.Equals("Security Digimon", StringComparison.OrdinalIgnoreCase))
            return null;
        string side = m.Groups["side"].Value.ToLowerInvariant();
        string count = m.Groups["count"].Value.ToLowerInvariant();
        bool countGiven = count.Length > 0;
        var spec = new TargetSpec
        {
            Opponent = side is "your opponent's" or "their",
            Count = !countGiven ? 0 : count == "all" ? 0 : int.Parse(Regex.Match(count, @"\d+").Value),
            Text = phrase,
        };
        // "your Digimon" (수 없음) = 자신의 디지몬 전부
        if (!countGiven && side is "its owner's")
            return null;

        var sel = Regex.Match(m.Groups["adj"].Value, @"(lowest|highest) (level|DP|play cost)", RegexOptions.IgnoreCase);
        if (sel.Success && !ParseFilters($"with the {sel.Groups[1].Value} {sel.Groups[2].Value}", spec))
            return null;
        foreach (Match a in Regex.Matches(m.Groups["adj"].Value, @"(?<!(?:lowest|highest) )unsuspended|(?<!(?:lowest|highest) )suspended|level (\d+)(?: or (lower|higher|more|less))?|\[([^\]]+)\](?: trait)?", RegexOptions.IgnoreCase))
        {
            string word = a.Value.ToLowerInvariant();
            if (word == "unsuspended") spec.Filters.Add(d => !d.IsSuspended);
            else if (word == "suspended") spec.Filters.Add(d => d.IsSuspended);
            else if (word.StartsWith("level"))
            {
                int lv = int.Parse(a.Groups[1].Value);
                string dir = a.Groups[2].Value.ToLowerInvariant();
                spec.Filters.Add(dir is "lower" or "less" ? d => d.TopCard.Level <= lv : dir is "higher" or "more" ? d => d.TopCard.Level >= lv : d => d.TopCard.Level == lv);
            }
            else
            {
                string x = a.Groups[3].Value;
                spec.Filters.Add(word.EndsWith("trait") ? d => HasTrait(d.TopCard, x) : d => NameHas(d.TopCard, x) || HasTrait(d.TopCard, x));
            }
        }

        string rest = m.Groups["rest"].Value.Trim();
        if (!ParseFilters(rest, spec))
            return null;
        return spec;
    }

    /// <summary>"with 4000 DP or less", "with the highest DP", "with no digivolution cards", "with [X] in its name" … (and로 이어질 수 있음)</summary>
    private static bool ParseFilters(string rest, TargetSpec spec)
    {
        rest = rest.Trim().TrimEnd(',').Trim();
        while (rest.Length > 0)
        {
            Match m;
            if ((m = Regex.Match(rest, @"^(?:with |that has |that have |and |or )?(?:(?:a|an) )?(?:(?<n>\d+) DP or (?<dir>less|more))", RegexOptions.IgnoreCase)).Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                bool less = m.Groups["dir"].Value.Equals("less", StringComparison.OrdinalIgnoreCase);
                spec.Filters.Add(less ? d => d.GetCurrentDP() <= n : d => d.GetCurrentDP() >= n);
            }
            else if ((m = Regex.Match(rest, @"^(?:with |and )?(?:a |the )?play costs? of (?<n>\d+) or (?<dir>less|more)", RegexOptions.IgnoreCase)).Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                bool less = m.Groups["dir"].Value.Equals("less", StringComparison.OrdinalIgnoreCase);
                spec.Filters.Add(less ? d => d.TopCard.PlayCost <= n : d => d.TopCard.PlayCost >= n);
            }
            else if ((m = Regex.Match(rest, @"^(?:with |and )?the (?<which>highest|lowest) (?<what>DP|level|play cost)", RegexOptions.IgnoreCase)).Success)
            {
                bool high = m.Groups["which"].Value.Equals("highest", StringComparison.OrdinalIgnoreCase);
                Func<CardInstance, int> key = m.Groups["what"].Value.ToLowerInvariant() switch
                {
                    "dp" => d => d.GetCurrentDP(),
                    "level" => d => d.TopCard.Level,
                    _ => d => d.TopCard.PlayCost,
                };
                spec.Selector = list =>
                {
                    var l = list.ToList();
                    if (l.Count == 0) return l;
                    int best = high ? l.Max(key) : l.Min(key);
                    return l.Where(d => key(d) == best);
                };
            }
            else if ((m = Regex.Match(rest, @"^(?:with no|without|with no digivolution cards|that has no) digivolution cards", RegexOptions.IgnoreCase)).Success
                     || (m = Regex.Match(rest, @"^with no digivolution cards", RegexOptions.IgnoreCase)).Success)
            {
                spec.Filters.Add(d => d.EvolutionSources.Count == 0);
            }
            else if ((m = Regex.Match(rest, @"^(?:with |and )?(?<n>\d+) or (?<dir>more|fewer|less) digivolution cards", RegexOptions.IgnoreCase)).Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                bool more = m.Groups["dir"].Value.Equals("more", StringComparison.OrdinalIgnoreCase);
                spec.Filters.Add(more ? d => d.EvolutionSources.Count >= n : d => d.EvolutionSources.Count <= n);
            }
            else if ((m = Regex.Match(rest, $@"^(?:with |and |or )?(?<x>{NameList}) in (?:its|their) names?", RegexOptions.IgnoreCase)).Success)
            {
                var xs = Names(m.Groups["x"].Value);
                spec.Filters.Add(d => xs.Any(x => NameHas(d.TopCard, x)));
            }
            else if ((m = Regex.Match(rest, $@"^(?:with |and |or )?(?:the )?(?<x>{NameList}) traits?|^with (?<x>{NameList}) in any of its traits", RegexOptions.IgnoreCase)).Success)
            {
                var xs = Names(m.Groups["x"].Value);
                spec.Filters.Add(d => xs.Any(x => HasTrait(d.TopCard, x)));
            }
            else if ((m = Regex.Match(rest, $@"^(?:with |and |or )?(?<x>{NameList}) in (?:its|their) texts?", RegexOptions.IgnoreCase)).Success)
            {
                var xs = Names(m.Groups["x"].Value);
                spec.Filters.Add(d => xs.Any(x => TextHas(d.TopCard, x)));
            }
            else if ((m = Regex.Match(rest, @"^(?:with |and )?the (?<which>fewest|most) digivolution cards", RegexOptions.IgnoreCase)).Success)
            {
                bool fewest = m.Groups["which"].Value.Equals("fewest", StringComparison.OrdinalIgnoreCase);
                spec.Selector = list =>
                {
                    var l = list.ToList();
                    if (l.Count == 0) return l;
                    int b = fewest ? l.Min(d => d.EvolutionSources.Count) : l.Max(d => d.EvolutionSources.Count);
                    return l.Where(d => d.EvolutionSources.Count == b);
                };
            }
            else if ((m = Regex.Match(rest, @"^(?:with |and )?DP less than or equal to this Digimon's DP", RegexOptions.IgnoreCase)).Success)
            {
                spec.ContextFilters.Add((c, d) => c.Self != null && d.GetCurrentDP() <= c.Self.GetCurrentDP());
            }
            else if ((m = Regex.Match(rest, @"^(?<neg>with|without|that (?:has|have)|that (?:doesn't|don't) have) ＜(?<k>[A-Za-z .]+?)\s*＞", RegexOptions.IgnoreCase)).Success
                     && KeywordNames.TryGetValue(m.Groups["k"].Value.Trim(), out var kw))
            {
                bool without = m.Groups["neg"].Value.StartsWith("without", StringComparison.OrdinalIgnoreCase) || m.Groups["neg"].Value.Contains("n't");
                spec.Filters.Add(d => (kw == Keyword.Blocker ? d.HasBlocker : CardScripts.HasKeyword(d, kw)) != without);
            }
            else if ((m = Regex.Match(rest, @"^with (?:as much or less|less than or equal to|DP less than or equal to) (?:DP )?(?:as|to) this Digimon(?:'s DP)?", RegexOptions.IgnoreCase)).Success)
            {
                spec.ContextFilters.Add((c, d) => c.Self != null && d.GetCurrentDP() <= c.Self.GetCurrentDP());
            }
            else if ((m = Regex.Match(rest, @"^with (?:a )?lower (?:DP|level) than this Digimon", RegexOptions.IgnoreCase)).Success)
            {
                bool dp = m.Value.Contains("DP", StringComparison.OrdinalIgnoreCase);
                spec.ContextFilters.Add((c, d) => c.Self != null && (dp ? d.GetCurrentDP() < c.Self.GetCurrentDP() : d.TopCard.Level < c.Self.TopCard.Level));
            }
            else
            {
                return false;
            }
            rest = rest[m.Length..].Trim().TrimStart(',').Trim();
        }
        return true;
    }

    private static readonly Dictionary<string, Keyword> KeywordNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Blocker"] = Keyword.Blocker, ["Jamming"] = Keyword.Jamming, ["Piercing"] = Keyword.Piercing, ["Retaliation"] = Keyword.Retaliation,
        ["Reboot"] = Keyword.Reboot, ["Rush"] = Keyword.Rush, ["Raid"] = Keyword.Raid, ["Alliance"] = Keyword.Alliance,
        ["Barrier"] = Keyword.Barrier, ["Evade"] = Keyword.Evade,
    };

    /// <summary>"[A]" · "[A] or [B]" · "[A], [B] or [C]" · "[A] and/or [B]" — 이름 / 특징 목록</summary>
    internal const string NameList = @"\[[^\]]+\](?:(?:,\s*|\s+)(?:or\s+|and/or\s+|and\s+)?\[[^\]]+\])*";

    internal static List<string> Names(string text) =>
        Regex.Matches(text, @"\[([^\]]+)\]").Select(m => m.Groups[1].Value.Trim()).ToList();

    public static bool NameHas(CardData card, string x) => card.Name.Contains(x, StringComparison.OrdinalIgnoreCase);

    public static bool TextHas(CardData card, string x) =>
        NameHas(card, x) || card.EffectText.Contains($"[{x}]", StringComparison.OrdinalIgnoreCase)
        || card.InheritedEffectText.Contains($"[{x}]", StringComparison.OrdinalIgnoreCase);

    public static bool HasTrait(CardData card, string x) =>
        card.Traits.Any(t => t.Equals(x, StringComparison.OrdinalIgnoreCase) || t.Contains(x, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 대상 고르기. harmful = 상대에게 불리한 효과(소멸 · DP − · 레스트 …)면 DP가 높은 것부터, 아니면 자신의 이 디지몬 → DP 높은 순.
    /// preferred = UI가 고른 대상 / 어택 중인 디지몬 (조건에 맞으면 먼저)
    /// </summary>
    public List<CardInstance> Resolve(EffectContext c, CardInstance? preferred)
    {
        if (IsThat && c.LastTargets.Count > 0)
            return c.LastTargets.Where(d => c.Owner.BattleArea.Contains(d) || c.Enemy.BattleArea.Contains(d)).ToList();
        if (IsThis || IsThat)
            return c.Self != null && (c.Owner.BattleArea.Contains(c.Self) || ReferenceEquals(c.Owner.BreedingArea, c.Self)) ? new List<CardInstance> { c.Self } : new List<CardInstance>();

        var zone = Opponent ? c.Enemy : c.Owner;
        IEnumerable<CardInstance> candidates = zone.BattleArea.Where(d => Filters.All(f => f(d)) && ContextFilters.All(f => f(c, d)));
        if (Selector != null)
            candidates = Selector(candidates);
        var list = candidates.ToList();
        if (Count == 0)
            return list;

        var ordered = list
            .OrderByDescending(d => ReferenceEquals(d, preferred))
            .ThenByDescending(d => !Opponent && ReferenceEquals(d, c.Self))
            .ThenByDescending(d => d.GetCurrentDP())
            .ThenByDescending(d => d.TopCard.Level);
        return ordered.Take(Count).ToList();
    }
}
