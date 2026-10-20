using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 효과 문장 파서 (효과 확장 3단계) — API 카드의 영어 효과 절을 카드 스크립트로 바꾼다.
/// 절 = [발동 시점 · 조건 표시]* + 문장들. 문장마다 "Then," · "If ~," · "You may" · 동작을 읽고,
/// 하나라도 읽지 못하면 그 절 전체를 미지원으로 둔다 (조용히 일부만 처리하지 않는다).
/// </summary>
internal static class SentenceParser
{
    private enum Qualifier { None, YourTurn, OpponentsTurn, AllTurns }

    private static readonly Dictionary<string, EffectTrigger> EventTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["On Play"] = EffectTrigger.OnPlay,
        ["When Digivolving"] = EffectTrigger.WhenDigivolving,
        ["When Attacking"] = EffectTrigger.WhenAttacking,
        ["On Deletion"] = EffectTrigger.OnDeletion,
        ["Main"] = EffectTrigger.Main,
        ["Security"] = EffectTrigger.Security,
        ["Start of Your Turn"] = EffectTrigger.StartOfYourTurn,
        ["End of Attack"] = EffectTrigger.EndOfAttack,
        ["Start of Your Main Phase"] = EffectTrigger.StartOfYourMainPhase,
        ["End of Your Turn"] = EffectTrigger.EndOfYourTurn,
        ["End of Opponent's Turn"] = EffectTrigger.EndOfOpponentsTurn,
        ["When Moving"] = EffectTrigger.WhenMoving,
    };

    private static readonly Dictionary<EffectTrigger, string> Labels = new()
    {
        [EffectTrigger.OnPlay] = "등장 시", [EffectTrigger.WhenDigivolving] = "진화 시", [EffectTrigger.WhenAttacking] = "어택 시",
        [EffectTrigger.OnDeletion] = "소멸 시", [EffectTrigger.Main] = "메인", [EffectTrigger.Security] = "시큐리티",
        [EffectTrigger.StartOfYourTurn] = "자신의 턴 개시 시", [EffectTrigger.EndOfAttack] = "어택 종료 시",
        [EffectTrigger.StartOfYourMainPhase] = "자신의 메인 페이즈 개시 시", [EffectTrigger.EndOfYourTurn] = "자신의 턴 종료 시",
        [EffectTrigger.DeletedOpponentInBattle] = "배틀에서 소멸시켰을 때", [EffectTrigger.AttackBlocked] = "블록당했을 때",
        [EffectTrigger.EndOfOpponentsTurn] = "상대의 턴 종료 시",
    };

    private static readonly Dictionary<string, Keyword> GrantableKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Blocker"] = Keyword.Blocker, ["Jamming"] = Keyword.Jamming, ["Piercing"] = Keyword.Piercing, ["Retaliation"] = Keyword.Retaliation,
        ["Reboot"] = Keyword.Reboot, ["Rush"] = Keyword.Rush, ["Raid"] = Keyword.Raid, ["Alliance"] = Keyword.Alliance,
        ["Barrier"] = Keyword.Barrier, ["Evade"] = Keyword.Evade,
    };

    /// <summary>파싱한 절을 스크립트에 등록하는 일 (전부 읽은 뒤에만 등록한다)</summary>
    private sealed class Pending
    {
        public readonly List<Action<CardScriptDefinition>> Registrations = new();
    }

    // =====================================================================
    // 진입점
    // =====================================================================

    /// <summary>절 하나를 읽어 def에 등록한다. 성공하면 null, 실패하면 미지원 이유. def가 null이면 읽기만 한다 (지원 상태 판단)</summary>
    public static string? TryParse(string rawClause, bool inherited, CardData card, CardScriptDefinition? def)
    {
        string clause = Normalize(rawClause);
        if (clause.Length == 0)
            return null;

        // ACE ≪오버플로우 -N≫: 이 카드가 배틀 에어리어 · 진화원에서 다른 곳으로 가면 메모리 -N
        var overflow = Regex.Match(clause, @"^(?:Ace )?Overflow ＜-(?<n>\d+)＞$", RegexOptions.IgnoreCase);
        if (overflow.Success)
        {
            int n = int.Parse(overflow.Groups["n"].Value);
            def?.Overflow(n);
            return null;
        }
        // 코스트 감소: "When this card would be played, if you have ~, reduce the play cost by 4."
        var reduce = Regex.Match(clause, @"^(?:\[[^\]]+\]\s*)*When (?:this card would be played|you would play this card(?: from your hand)?|you would use this card), (?:if (?<cond>[^,]+), )?reduce (?:the|its) (?:play |use |memory )?cost by (?<n>\d+)\.?$", RegexOptions.IgnoreCase);
        if (reduce.Success)
        {
            int n = int.Parse(reduce.Groups["n"].Value);
            Func<PlayerZones, bool> zc = _ => true;
            if (reduce.Groups["cond"].Success)
            {
                var parsedCond = ParseZoneCondition(reduce.Groups["cond"].Value);
                if (parsedCond == null) return $"미지원 조건 (코스트 감소: {reduce.Groups["cond"].Value})";
                zc = parsedCond;
            }
            def?.ReducePlayCost(z => zc(z) ? n : 0);
            return null;
        }
        // 옵션 사용 조건: "While you have a Tamer, you can ignore this card's color requirements."
        var ignore = Regex.Match(clause, @"^While (?<cond>.+?), you can ignore this card's color requirements\.?$", RegexOptions.IgnoreCase);
        if (ignore.Success)
        {
            var zoneCond = ParseZoneCondition(ignore.Groups["cond"].Value);
            if (zoneCond == null) return $"미지원 조건 (색 조건 무시: {ignore.Groups["cond"].Value})";
            def?.IgnoreColorRequirementWhen(zoneCond);
            return null;
        }

        var prefix = Regex.Match(clause, @"^((?:\s*\[[^\]]+\])*)\s*(.*)$");
        var labels = Regex.Matches(prefix.Groups[1].Value, @"\[([^\]]+)\]").Select(m => m.Groups[1].Value.Trim()).ToList();
        string body = prefix.Groups[2].Value.Trim();

        var triggers = new List<EffectTrigger>();
        var qualifier = Qualifier.None;
        bool oncePerTurn = false;
        foreach (var label in labels)
        {
            if (EventTriggers.TryGetValue(label, out var t)) triggers.Add(t);
            else if (label.Equals("End of All Turns", StringComparison.OrdinalIgnoreCase)) { triggers.Add(EffectTrigger.EndOfYourTurn); triggers.Add(EffectTrigger.EndOfOpponentsTurn); }
            else if (label.Equals("Your Turn", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.YourTurn;
            else if (label.Equals("Opponent's Turn", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.OpponentsTurn;
            else if (label.Equals("All Turns", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.AllTurns;
            else if (label.Equals("Once Per Turn", StringComparison.OrdinalIgnoreCase)) oncePerTurn = true;
            else return $"미지원 시점 [{label}]";
        }

        // "When this Digimon ~, ~" — 문장 안의 발동 시점
        Func<EffectContext, bool>? eventCondition = null;
        var when = Regex.Match(body, @"^When (?<ev>[^,]+), (?<rest>.+)$", RegexOptions.IgnoreCase);
        if (triggers.Count == 0 && when.Success)
        {
            var ev = ParseEventPhrase(when.Groups["ev"].Value);
            if (ev == null) return "미지원 발동 조건 (When ~)";
            triggers.AddRange(ev.Value.Triggers);
            eventCondition = ev.Value.Condition;
            body = when.Groups["rest"].Value;
        }
        else if (when.Success && ParseEventPhrase(when.Groups["ev"].Value) is { } same && same.Triggers.All(triggers.Contains))
        {
            // "[When Attacking] When this Digimon attacks a player, ~" — 같은 시점에 조건만 더한다
            eventCondition = same.Condition;
            body = when.Groups["rest"].Value;
        }

        var pending = new Pending();
        string? error = triggers.Count == 0
            ? ParseStatic(body, inherited, card, qualifier, pending)
            : ParseTriggered(body, inherited, card, triggers, qualifier, oncePerTurn, pending, eventCondition);
        if (error != null)
            return error;
        if (def != null)
            foreach (var r in pending.Registrations) r(def);
        return null;
    }

    /// <summary>
    /// 고르는 효과: "[Main] Activate 1 of the effects below:" + "・~" 줄들. 선택지마다 문장을 읽고,
    /// 처리할 때 플레이어에게 앞에서부터 물어 고르게 한다 (AI · 테스트는 첫 번째).
    /// </summary>
    public static string? TryParseChoice(string rawHeader, IReadOnlyList<string> rawOptions, bool inherited, CardData card, CardScriptDefinition? def)
    {
        string header = Normalize(rawHeader);
        var prefix = Regex.Match(header, @"^((?:\s*\[[^\]]+\])*)\s*(.*)$");
        var labels = Regex.Matches(prefix.Groups[1].Value, @"\[([^\]]+)\]").Select(m => m.Groups[1].Value.Trim()).ToList();
        var triggers = new List<EffectTrigger>();
        bool oncePerTurn = false;
        var qualifier = Qualifier.None;
        foreach (var label in labels)
        {
            if (EventTriggers.TryGetValue(label, out var t)) triggers.Add(t);
            else if (label.Equals("Once Per Turn", StringComparison.OrdinalIgnoreCase)) oncePerTurn = true;
            else if (label.Equals("Your Turn", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.YourTurn;
            else if (label.Equals("Opponent's Turn", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.OpponentsTurn;
            else if (label.Equals("All Turns", StringComparison.OrdinalIgnoreCase)) qualifier = Qualifier.AllTurns;
            else return $"미지원 시점 [{label}]";
        }
        if (triggers.Count == 0) return "미지원: 시점 없는 고르는 효과";
        var body = Regex.Match(prefix.Groups[2].Value, @"^(?:If (?<cond>[^,]+), )?(?:you may )?(?:By (?<cost>[^,]+), )?(?:you may )?activate (?:1|one) of the (?:effects below|following effects)[:.]?$", RegexOptions.IgnoreCase);
        if (!body.Success) return "문장 파서(3단계) 대상: 고르는 효과";
        Func<EffectContext, bool>? cond = null;
        if (body.Groups["cond"].Success && (cond = ParseEventCondition(body.Groups["cond"].Value)) == null)
            return $"미지원 조건 (If {body.Groups["cond"].Value})";
        if (body.Groups["cost"].Success)
        {
            var cost = ParseCost(body.Groups["cost"].Value, card);
            if (cost == null) return $"미지원 비용 (By {body.Groups["cost"].Value})";
            var (canPay, pay, recommend, describe) = cost.Value;
            var baseCond = cond;
            cond = c =>
            {
                if (baseCond != null && !baseCond(c)) return false;
                if (!canPay(c) || !c.Engine.AskYesNo(c.Owner, c.Self, $"[{c.Card.Name}] {describe} 효과를 발휘할까요?", recommend(c))) return false;
                pay(c);
                return true;
            };
        }

        var options = new List<Step[]>();
        foreach (var raw in rawOptions)
        {
            var steps = new List<Step>();
            foreach (var sentence in MergeRevealSentences(SplitSentences(Normalize(raw))))
            {
                var step = ParseStep(sentence, card, triggers);
                if (step.Error != null) return step.Error;
                if (step.Step != null) steps.Add(step.Step);
            }
            options.Add(steps.ToArray());
        }

        var optionTexts = rawOptions.Select(o => Normalize(o).TrimStart('・').Trim()).ToArray();
        // AI 추천: 문장 점수(AiEvaluator)의 합이 가장 큰 선택지 (같으면 앞의 것)
        var values = optionTexts.Select(t => SplitSentences(t).Sum(AiEvaluator.EstimateSentenceValue)).ToArray();
        int recommended = Array.IndexOf(values, values.Max());
        Action<EffectContext> resolve = c =>
        {
            if (cond != null && !cond(c)) return;
            int pick = c.Engine.ChooseOption(c.Owner, $"[{c.Card.Name}] 발휘할 효과를 1개 고르세요.", optionTexts, recommended);
            foreach (var s in options[pick])
                RunStep(s, c);
        };
        Func<EffectContext, bool>? when = qualifier switch
        {
            Qualifier.YourTurn => c => c.Engine.IsTurnOf(c.Owner),
            Qualifier.OpponentsTurn => c => !c.Engine.IsTurnOf(c.Owner),
            _ => null,
        };
        if (triggers.Contains(EffectTrigger.Main) && (card.Type != CardType.Option || inherited))
            return "미지원: 테이머 · 디지몬의 [메인] 효과";
        foreach (var t in triggers)
        {
            var tr = t;
            if (def == null) continue;
            if (tr == EffectTrigger.Main) def.Main(TargetKind.None, resolve);
            else if (inherited) def.Inherited(tr, resolve, when, oncePerTurn);
            else def.On(tr, resolve, when, oncePerTurn);
        }
        return null;
    }

    /// <summary>설명문 괄호 · 라벨("Security Effect", "Inherited Effect", "・") · 특수 공백 정리 (키워드 ＜＞ 안의 괄호는 보존)</summary>
    private static string Normalize(string clause)
    {
        string s = clause.Replace(" ", " ").Replace("’", "'").Replace("‘", "'").Trim();
        s = Regex.Replace(s, @"^(?:Security Effect|Inherited Effect|・)\s*", "", RegexOptions.IgnoreCase);
        // ＜Recovery +1 (Deck)＞ 같은 키워드 안 괄호를 잠시 바꿔 두고, 설명문 괄호를 지운다
        s = Regex.Replace(s, @"[＜<]([^＞>]*)[＞>]", m => "＜" + m.Groups[1].Value.Replace("(", "⟮").Replace(")", "⟯") + "＞");
        s = Regex.Replace(s, @"\s*\((?:[^()]|\([^()]*\))*\)", "");
        s = s.Replace("⟮", "(").Replace("⟯", ")");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        s = Regex.Replace(s, @"\s+([.,])", "$1");
        return s;
    }

    /// <summary>"When ~," 문장 안의 발동 시점 → 엔진 시점 + 추가 조건 (예: "your other Digimon" = 자신이 아닌 디지몬)</summary>
    private static (List<EffectTrigger> Triggers, Func<EffectContext, bool>? Condition)? ParseEventPhrase(string ev)
    {
        ev = ev.Trim();
        Match m;
        (List<EffectTrigger>, Func<EffectContext, bool>?) One(EffectTrigger t, Func<EffectContext, bool>? cond = null) => (new List<EffectTrigger> { t }, cond);

        if (Regex.IsMatch(ev, @"^this Digimon deletes (?:an|1 of your|your) opponent's Digimon in battle(?: and survives)?$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.DeletedOpponentInBattle);
        if (Regex.IsMatch(ev, @"^this Digimon (?:is|becomes|gets) blocked$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.AttackBlocked);
        if (Regex.IsMatch(ev, @"^this Digimon attacks$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.WhenAttacking);
        if (Regex.IsMatch(ev, @"^this Digimon attacks (?:a player|your opponent|the opponent)$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.WhenAttacking, c => c.Other == null);
        if (Regex.IsMatch(ev, @"^this Digimon attacks (?:a|an opponent's|your opponent's) Digimon$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.WhenAttacking, c => c.Other != null);
        if (Regex.IsMatch(ev, @"^this Digimon wins a battle$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.WonBattle);
        if (Regex.IsMatch(ev, @"^(?:an|one of your|any of your) opponent's Digimon (?:becomes suspended|suspends|is suspended)$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.OpponentDigimonSuspended);
        if ((m = Regex.Match(ev, $@"^(?:one of |any of )?your (?<other>other )?(?:(?<names>{TargetSpec.NameList}) trait )?Digimon(?: with (?<inname>{TargetSpec.NameList}) in (?:its|their) names?)? (?<what>attacks?|suspends?|becomes suspended|become suspended)$", RegexOptions.IgnoreCase)).Success)
        {
            bool other = m.Groups["other"].Success;
            var traits = m.Groups["names"].Success ? TargetSpec.Names(m.Groups["names"].Value) : null;
            var names = m.Groups["inname"].Success ? TargetSpec.Names(m.Groups["inname"].Value) : null;
            var trig = m.Groups["what"].Value.StartsWith("attack", StringComparison.OrdinalIgnoreCase) ? EffectTrigger.AllyDigimonAttacks : EffectTrigger.AllyDigimonSuspended;
            return One(trig, c => c.Other != null
                && (!other || !ReferenceEquals(c.Other, c.Self))
                && (traits == null || traits.Any(x => TargetSpec.HasTrait(c.Other.TopCard, x)))
                && (names == null || names.Any(x => TargetSpec.NameHas(c.Other.TopCard, x))));
        }
        if (Regex.IsMatch(ev, @"^this Digimon (?:suspends|becomes suspended|is suspended)$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.ThisDigimonSuspends);
        if (Regex.IsMatch(ev, @"^(?:an|one of your|any of your) opponent's Digimon attacks?$|^your opponent's Digimon attacks?$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.OpponentDigimonAttacks);
        if (Regex.IsMatch(ev, @"^(?:an|one of your|any of your) opponent's Digimon (?:is|are) deleted$|^your opponent's Digimon (?:is|are) deleted$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.OpponentDigimonDeleted);
        if (Regex.IsMatch(ev, @"^(?:one of |any of )?your other Digimon (?:is|are) deleted$", RegexOptions.IgnoreCase))
            return One(EffectTrigger.AllyDigimonDeleted);

        // "your (other) ([X] trait) Digimon are played (or digivolve)"
        if ((m = Regex.Match(ev, $@"^(?:one of |any of )?your (?<other>other )?(?:(?<names>{TargetSpec.NameList}) trait )?Digimon(?: with (?<inname>{TargetSpec.NameList}) in (?:its|their) names?)? (?:is |are )?(?<what>played or digivolves?|played|digivolves?|digivolved)$", RegexOptions.IgnoreCase)).Success)
        {
            bool other = m.Groups["other"].Success;
            var traits = m.Groups["names"].Success ? TargetSpec.Names(m.Groups["names"].Value) : null;
            var names = m.Groups["inname"].Success ? TargetSpec.Names(m.Groups["inname"].Value) : null;
            string what = m.Groups["what"].Value.ToLowerInvariant();
            var triggers = new List<EffectTrigger>();
            if (what.Contains("played")) triggers.Add(EffectTrigger.AllyDigimonPlayed);
            if (what.Contains("digivolv")) triggers.Add(EffectTrigger.AllyDigimonDigivolved);
            Func<EffectContext, bool> cond = c => c.Other != null
                && (!other || !ReferenceEquals(c.Other, c.Self))
                && (traits == null || traits.Any(x => TargetSpec.HasTrait(c.Other.TopCard, x)))
                && (names == null || names.Any(x => TargetSpec.NameHas(c.Other.TopCard, x)));
            return (triggers, cond);
        }
        return null;
    }

    private static List<string> SplitSentences(string body) =>
        Regex.Split(Regex.Replace(body, @"＞\s+Then\b", "＞. Then"), @"(?<=[.!])\s+(?=[A-Z＜<\[])")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    // =====================================================================
    // 지속 효과: "[Your Turn] This Digimon gets +1000 DP." · "While ~, this Digimon gains ＜Blocker＞."
    // =====================================================================

    private static string? ParseStatic(string body, bool inherited, CardData card, Qualifier qualifier, Pending pending)
    {
        var sentences = SplitSentences(body);
        if (sentences.Count != 1)
            return "문장 파서(3단계) 대상: 여러 문장의 지속 효과";
        string s = sentences[0].TrimEnd('.');

        Func<StaticContext, bool> turn = qualifier switch
        {
            Qualifier.YourTurn => c => c.OwnersTurn,
            Qualifier.OpponentsTurn => c => !c.OwnersTurn,
            _ => _ => true,
        };
        Func<StaticContext, bool> cond = turn;
        var w = Regex.Match(s, @"^While (?<cond>.+?), (?<rest>(?:this Digimon|it|all of your Digimon|your Digimon) .+)$", RegexOptions.IgnoreCase);
        if (w.Success)
        {
            var parsed = ParseStaticCondition(w.Groups["cond"].Value);
            if (parsed == null) return $"미지원 조건 (While {w.Groups["cond"].Value})";
            cond = c => turn(c) && parsed(c);
            s = w.Groups["rest"].Value;
        }

        var source = inherited ? EffectSource.Inherited : EffectSource.Main;
        Match m;
        if ((m = Regex.Match(s, @"^(?:this Digimon|it) gets? (?<sign>[+-])(?<n>\d+) DP$", RegexOptions.IgnoreCase)).Success)
        {
            int dp = int.Parse(m.Groups["n"].Value) * (m.Groups["sign"].Value == "-" ? -1 : 1);
            var c2 = cond;
            pending.Registrations.Add(d => d.Static(StaticStat.Dp, source, c => c2(c) ? dp : 0));
            return null;
        }
        if ((m = Regex.Match(s, @"^(?:all of your Digimon|your Digimon) gets? (?<sign>[+-])(?<n>\d+) DP$", RegexOptions.IgnoreCase)).Success && card.Type == CardType.Tamer && !inherited)
        {
            int dp = int.Parse(m.Groups["n"].Value) * (m.Groups["sign"].Value == "-" ? -1 : 1);
            var c2 = cond;
            pending.Registrations.Add(d => d.Static(StaticStat.AllyDigimonDp, EffectSource.Main, c => c2(c) ? dp : 0));
            return null;
        }
        if ((m = Regex.Match(s, @"^(?:this Digimon|it) gains? (?<kws>.+)$", RegexOptions.IgnoreCase)).Success)
        {
            var grants = ParseKeywordList(m.Groups["kws"].Value);
            if (grants == null) return "미지원 키워드 부여";
            var c2 = cond;
            foreach (var (k, sa) in grants)
            {
                if (k is { } kw)
                    pending.Registrations.Add(d => d.Static(StaticStat.Keyword, source, c => c2(c) ? 1 : 0, kw));
                else
                    pending.Registrations.Add(d => d.Static(StaticStat.SecurityAttack, source, c => c2(c) ? sa : 0));
            }
            return null;
        }
        return $"문장 파서(3단계) 대상: {s}";
    }

    /// <summary>＜Blocker＞, ＜Rush＞ and ＜Security A. +1＞ → (키워드 또는 null, 시큐리티 어택 수)</summary>
    private static List<(Keyword? Keyword, int SecurityAttack)>? ParseKeywordList(string text)
    {
        text = text.Trim().TrimEnd('.');
        var result = new List<(Keyword?, int)>();
        string rest = text;
        while (rest.Length > 0)
        {
            var m = Regex.Match(rest, @"^(?:,\s*|and\s+|,\s*and\s+)?＜(?<name>[^＞0-9+\-−]+?)\s*(?<num>[+\-−]\s*\d+)?\s*＞\s*", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            string name = m.Groups["name"].Value.Trim();
            if (GrantableKeywords.TryGetValue(name, out var k)) result.Add((k, 0));
            else if (name.StartsWith("Security A", StringComparison.OrdinalIgnoreCase))
                result.Add((null, int.Parse(m.Groups["num"].Value.Replace(" ", "").Replace("−", "-"))));
            else return null;
            rest = rest[m.Length..].Trim();
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>필드 상태 조건 (옵션 색 조건 무시용) — 자신의 배틀 에어리어 · 테이머 기준</summary>
    private static Func<PlayerZones, bool>? ParseZoneCondition(string cond)
    {
        cond = cond.Trim();
        Match m;
        if (Regex.IsMatch(cond, @"^you have (?:a|any) Tamers?$", RegexOptions.IgnoreCase)) return z => z.Tamers.Count > 0;
        if (Regex.IsMatch(cond, @"^you have no face-up security cards$", RegexOptions.IgnoreCase)) return _ => true;   // 앞면 시큐리티는 아직 없다
        if ((m = Regex.Match(cond, @"^you have (?:a |an )?(?:\[(?<x>[^\]]+)\] trait (?<kind>Digimon or Tamer|Digimon|Tamer)|(?<kind>Digimon or Tamer|Digimon|Tamer) with the \[(?<x>[^\]]+)\] trait)(?: on the field| in play)?$", RegexOptions.IgnoreCase)).Success)
        {
            string x = m.Groups["x"].Value; string kind = m.Groups["kind"].Value.ToLowerInvariant();
            return z => (kind != "tamer" && z.BattleArea.Any(d => TargetSpec.HasTrait(d.TopCard, x)))
                        || (kind != "digimon" && z.Tamers.Any(t => TargetSpec.HasTrait(t, x)));
        }
        if ((m = Regex.Match(cond, @"^you (?:have )?(?<neg>don't have|do not have|have) \[(?<x>[^\]]+)\] in (?:the|your) battle area$", RegexOptions.IgnoreCase)).Success)
        {
            string x = m.Groups["x"].Value; bool neg = !m.Groups["neg"].Value.Equals("have", StringComparison.OrdinalIgnoreCase);
            return z => z.BattleArea.Any(d => TargetSpec.NameHas(d.TopCard, x)) != neg;
        }
        if ((m = Regex.Match(cond, $@"^(?:you have|there are) (?<n>\d+) or more (?:cards )?(?:with (?<x>{TargetSpec.NameList}) in (?:its|their) (?<where>texts?|names?) |(?:(?<t>{TargetSpec.NameList}) trait )?cards )?in your trash$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            var xs = m.Groups["x"].Success ? TargetSpec.Names(m.Groups["x"].Value) : null;
            var ts = m.Groups["t"].Success ? TargetSpec.Names(m.Groups["t"].Value) : null;
            bool inName = m.Groups["where"].Value.StartsWith("name", StringComparison.OrdinalIgnoreCase);
            return z => z.Trash.Count(c => (xs == null || xs.Any(x => inName ? TargetSpec.NameHas(c, x) : TargetSpec.TextHas(c, x)))
                                           && (ts == null || ts.Any(x => TargetSpec.HasTrait(c, x)))) >= n;
        }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>more|fewer|less) security cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return z => more ? z.SecurityStack.Count >= n : z.SecurityStack.Count <= n; }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or more Tamers$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return z => z.Tamers.Count >= n; }
        if ((m = Regex.Match(cond, @"^you have (?:a |an )?(?<kind>Digimon|Tamer) with \[(?<x>[^\]]+)\] in its name(?: on the field| in play)?$", RegexOptions.IgnoreCase)).Success)
        {
            string x = m.Groups["x"].Value; bool tamer = m.Groups["kind"].Value.Equals("Tamer", StringComparison.OrdinalIgnoreCase);
            return z => tamer ? z.Tamers.Any(t => TargetSpec.NameHas(t, x)) : z.BattleArea.Any(d => TargetSpec.NameHas(d.TopCard, x));
        }
        return null;
    }

    /// <summary>"this Digimon has the [A] or [B] trait" · "has [X] in its name / text" · "[A] or [B] is in this Digimon's digivolution cards"</summary>
    private static Func<CardInstance?, bool>? ParseDigimonCondition(string cond)
    {
        cond = cond.Trim();
        Match m;
        if ((m = Regex.Match(cond, $@"^(?:this Digimon|it) has (?:the )?(?<x>{TargetSpec.NameList}) traits?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return d => d != null && xs.Any(x => TargetSpec.HasTrait(d.TopCard, x)); }
        if ((m = Regex.Match(cond, $@"^(?:this Digimon|it) has (?<x>{TargetSpec.NameList}) in its names?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return d => d != null && xs.Any(x => TargetSpec.NameHas(d.TopCard, x)); }
        if ((m = Regex.Match(cond, $@"^(?:this Digimon|it) has (?<x>{TargetSpec.NameList}) in its texts?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return d => d != null && xs.Any(x => TargetSpec.TextHas(d.TopCard, x)); }
        if ((m = Regex.Match(cond, $@"^(?:a card with )?(?<x>{TargetSpec.NameList})(?: in its name)? (?:is|are) in this Digimon's digivolution cards$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return d => d != null && d.EvolutionSources.Any(s => xs.Any(x => TargetSpec.NameHas(s, x))); }
        if ((m = Regex.Match(cond, $@"^this Digimon's digivolution cards include (?:a card with )?(?<x>{TargetSpec.NameList})(?: in its name)?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return d => d != null && d.EvolutionSources.Any(s => xs.Any(x => TargetSpec.NameHas(s, x))); }
        if ((m = Regex.Match(cond, @"^(?:this Digimon|it) has (?<n>\d+) or (?<dir>more|fewer|less) digivolution cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return d => d != null && (more ? d.EvolutionSources.Count >= n : d.EvolutionSources.Count <= n); }
        if (Regex.IsMatch(cond, @"^(?:this Digimon|it) has no digivolution cards$", RegexOptions.IgnoreCase)) return d => d != null && d.EvolutionSources.Count == 0;
        if (Regex.IsMatch(cond, @"^(?:this Digimon|it) is suspended$", RegexOptions.IgnoreCase)) return d => d?.IsSuspended == true;
        if (Regex.IsMatch(cond, @"^(?:this Digimon|it) is unsuspended$", RegexOptions.IgnoreCase)) return d => d?.IsSuspended == false;
        return null;
    }

    private static Func<StaticContext, bool>? ParseStaticCondition(string cond)
    {
        cond = cond.Trim();
        if (ParseDigimonCondition(cond) is { } dc) return c => dc(c.Digimon);
        Match m;
        if (Regex.IsMatch(cond, @"^(?:this Digimon|it) is suspended$", RegexOptions.IgnoreCase)) return c => c.Digimon?.IsSuspended == true;
        if (Regex.IsMatch(cond, @"^(?:this Digimon|it) is unsuspended$", RegexOptions.IgnoreCase)) return c => c.Digimon?.IsSuspended == false;
        if ((m = Regex.Match(cond, @"^(?:this Digimon|it) has (?<n>\d+) or more digivolution cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => (c.Digimon?.EvolutionSources.Count ?? 0) >= n; }
        if ((m = Regex.Match(cond, @"^(?:this Digimon|it) has \[(?<x>[^\]]+)\] in its name$", RegexOptions.IgnoreCase)).Success)
        { string x = m.Groups["x"].Value; return c => c.Digimon != null && TargetSpec.NameHas(c.Digimon.TopCard, x); }
        if ((m = Regex.Match(cond, @"^(?:this Digimon|it) has the \[(?<x>[^\]]+)\] trait$", RegexOptions.IgnoreCase)).Success)
        { string x = m.Groups["x"].Value; return c => c.Digimon != null && TargetSpec.HasTrait(c.Digimon.TopCard, x); }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>fewer|less|more) security cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Owner.SecurityStack.Count >= n : c.Owner.SecurityStack.Count <= n; }
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or (?<dir>fewer|less|more) security cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Enemy.SecurityStack.Count >= n : c.Enemy.SecurityStack.Count <= n; }
        if (Regex.IsMatch(cond, @"^you have a Tamer$", RegexOptions.IgnoreCase)) return c => c.Owner.Tamers.Count > 0;
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or more Tamers$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Owner.Tamers.Count >= n; }
        if ((m = Regex.Match(cond, @"^(?:there are|you have) (?<n>\d+) or more cards in your trash$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Owner.Trash.Count >= n; }
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or more cards in (?:their|your opponent's) trash$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Enemy.Trash.Count >= n; }
        return null;
    }

    // =====================================================================
    // 발동 효과: [시점] 문장들
    // =====================================================================

    /// <summary>문장 하나: 조건 · 동작 · "~할 수 있다"(Optional — 발휘할지 묻는다) · 원문(물을 때 보여 줌)</summary>
    private sealed record Step(Func<EffectContext, bool>? Condition, Action<EffectContext> Act, bool Optional = false, string Text = "");

    /// <summary>문장을 처리한다: 조건 확인 → "~할 수 있다"면 효과 주인에게 묻기(추천 = AI 평가) → 동작</summary>
    private static void RunStep(Step s, EffectContext c)
    {
        if (s.Condition != null && !s.Condition(c)) return;
        if (s.Optional && !c.Engine.AskYesNo(c.Owner, c.Self, $"[{c.Card.Name}] 이 효과를 발휘할까요?\n{s.Text}", AiEvaluator.EstimateSentenceValue(s.Text) >= 0)) return;
        s.Act(c);
    }

    private static string? ParseTriggered(string body, bool inherited, CardData card, List<EffectTrigger> triggers, Qualifier qualifier, bool oncePerTurn, Pending pending, Func<EffectContext, bool>? eventCondition = null)
    {
        foreach (var t in triggers)
        {
            // 엔진이 이 시점을 처리하는 카드 종류인지
            if (t == EffectTrigger.Main && (card.Type != CardType.Option || inherited)) return "미지원: 테이머 · 디지몬의 [메인] 효과";
            if (t == EffectTrigger.Security && card.Type == CardType.Tamer && !IsTamerPlayRule(body)) return "미지원: 테이머의 [시큐리티] 효과";
            if (t == EffectTrigger.OnPlay && card.Type == CardType.Option) return "미지원: 옵션의 [등장 시]";
        }

        var sentences = MergeRevealSentences(SplitSentences(body));
        var steps = new List<Step>();
        foreach (var raw in sentences)
        {
            var step = ParseStep(raw, card, triggers);
            if (step.Error != null) return step.Error;
            if (step.Step != null) steps.Add(step.Step);
        }
        if (steps.Count == 0)
            return null;   // 규칙으로 처리되는 문장뿐 (테이머 [시큐리티] 등장)

        Func<EffectContext, bool> turnOk = qualifier switch
        {
            Qualifier.YourTurn => c => c.Engine.IsTurnOf(c.Owner),
            Qualifier.OpponentsTurn => c => !c.Engine.IsTurnOf(c.Owner),
            _ => _ => true,
        };
        var stepList = steps.ToArray();
        foreach (var trigger in triggers)
        {
            var tr = trigger;
            Action<EffectContext> resolve = c =>
            {
                foreach (var s in stepList)
                    RunStep(s, c);
                if (tr == EffectTrigger.Security && c.Summary.Length == 0)
                    c.Summary = c.Message.Length > 0 ? Shorten(c.Message) : "효과 발휘";
            };
            Func<EffectContext, bool>? when = qualifier == Qualifier.None || qualifier == Qualifier.AllTurns ? null : turnOk;
            if (eventCondition != null)
            {
                var turnWhen = when;
                when = c => (turnWhen == null || turnWhen(c)) && eventCondition(c);
            }
            if (tr == EffectTrigger.Main)
                pending.Registrations.Add(d => d.Main(TargetKind.None, resolve));
            else if (inherited)
                pending.Registrations.Add(d => d.Inherited(tr, resolve, when, oncePerTurn));
            else
                pending.Registrations.Add(d => d.On(tr, resolve, when, oncePerTurn));
        }
        return null;
    }

    private static string Shorten(string s) => s.Length <= 40 ? s : s[..40] + "…";

    private static bool IsTamerPlayRule(string body) =>
        Regex.IsMatch(body.Trim().TrimEnd('.'), @"^(?:Play this card without paying (?:the|its) (?:memory )?cost|Place this card in (?:the|your) battle area)$", RegexOptions.IgnoreCase);

    /// <summary>"Reveal the top N ~. Add ~ among them to the hand. Return the rest ~." 를 한 문장으로 합친다</summary>
    private static List<string> MergeRevealSentences(List<string> sentences)
    {
        var result = new List<string>();
        for (int i = 0; i < sentences.Count; i++)
        {
            if (Regex.IsMatch(sentences[i], @"^(?:Then, )?(?:You may )?Reveal the top \d+ cards? of your deck\.?$", RegexOptions.IgnoreCase)
                && i + 2 < sentences.Count
                && Regex.IsMatch(sentences[i + 1], @"^Add .+ among them to (?:the|your) hand\.?$", RegexOptions.IgnoreCase))
            {
                result.Add($"{sentences[i]} {sentences[i + 1]} {sentences[i + 2]}");
                i += 2;
            }
            else result.Add(sentences[i]);
        }
        return result;
    }

    private static (Step? Step, string? Error) ParseStep(string sentence, CardData card, List<EffectTrigger> triggers)
    {
        string s = sentence.Trim();
        s = Regex.Replace(s, @"^Then,?\s+", "", RegexOptions.IgnoreCase);

        Func<EffectContext, bool>? cond = null;
        var ifm = Regex.Match(s, @"^If (?<c>[^,]+), (?<rest>.+)$", RegexOptions.IgnoreCase);
        if (ifm.Success)
        {
            cond = ParseEventCondition(ifm.Groups["c"].Value);
            if (cond == null) return (null, $"미지원 조건 (If {ifm.Groups["c"].Value})");
            s = ifm.Groups["rest"].Value;
        }
        bool optional = Regex.IsMatch(s, @"^you may\s+", RegexOptions.IgnoreCase);   // "~할 수 있다": 발휘할지 효과 주인이 정한다
        s = Regex.Replace(s, @"^you may\s+", "", RegexOptions.IgnoreCase);
        s = s.Trim();

        // 비용: "By trashing 1 card in your hand, ~" — 낼 수 있을 때만, 낼지는 효과 주인이 정한다 (플레이어는 물어봄)
        var by = Regex.Match(s, @"^By (?<cost>[^,]+), (?<rest>.+)$", RegexOptions.IgnoreCase);
        if (by.Success)
        {
            var cost = ParseCost(by.Groups["cost"].Value, card);
            if (cost == null) return (null, $"미지원 비용 (By {by.Groups["cost"].Value})");
            var inner = ParseStep(by.Groups["rest"].Value, card, triggers);
            if (inner.Error != null || inner.Step == null) return (null, inner.Error ?? "미지원 비용 뒤 문장");
            var (canPay, pay, recommend, describe) = cost.Value;
            var innerStep = inner.Step;
            Action<EffectContext> paidAct = c =>
            {
                if (!canPay(c)) return;
                if (!c.Engine.AskYesNo(c.Owner, c.Self, $"[{c.Card.Name}] {describe} 효과를 발휘할까요?", recommend(c))) return;
                pay(c);
                RunStep(innerStep, c);
            };
            return (new Step(cond, paidAct), null);
        }
        if (s.Length > 0) s = char.ToUpperInvariant(s[0]) + s[1..];

        bool security = triggers.Contains(EffectTrigger.Security);
        if (security && card.Type == CardType.Tamer && IsTamerPlayRule(s))
            return (null, null);   // 테이머 [시큐리티] 등장 = 엔진 규칙

        var act = ParseAction(s.TrimEnd('.'), card, triggers);
        if (act.Error != null) return (null, act.Error);
        return (new Step(cond, act.Act!, optional, sentence.Trim()), null);
    }

    /// <summary>비용 구절 → (낼 수 있는지, 내기, AI 추천, 설명)</summary>
    private static (Func<EffectContext, bool> CanPay, Action<EffectContext> Pay, Func<EffectContext, bool> Recommend, string Describe)? ParseCost(string cost, CardData card)
    {
        cost = cost.Trim();
        Match m;
        if (Regex.IsMatch(cost, @"^suspending this Digimon$", RegexOptions.IgnoreCase))
            return (c => c.Self != null && !c.Self.IsSuspended && c.Owner.BattleArea.Contains(c.Self), c => c.Engine.Suspend(c.Self!), _ => true, "이 디지몬을 레스트시켜");
        if (Regex.IsMatch(cost, @"^suspending this Tamer$", RegexOptions.IgnoreCase) && card.Type == CardType.Tamer)
            return (c => c.Owner.HasActiveTamer(c.Card), c => c.Owner.SuspendedTamers.Add(c.Card), _ => true, "이 테이머를 레스트시켜");
        if ((m = Regex.Match(cost, @"^suspending (?<n>\d+) of your other Digimon$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            IEnumerable<CardInstance> Others(EffectContext c) => c.Owner.BattleArea.Where(d => !d.IsSuspended && !ReferenceEquals(d, c.Self)).OrderBy(d => d.HasBlocker ? 1 : 0).ThenBy(d => d.GetCurrentDP());
            return (c => Others(c).Count() >= n, c => { foreach (var d in Others(c).Take(n).ToList()) c.Engine.Suspend(d); }, _ => true, $"다른 디지몬 {n}마리를 레스트시켜");
        }
        if (Regex.IsMatch(cost, @"^trashing (?:the top card of your security stack|your top security card)$", RegexOptions.IgnoreCase))
            return (c => c.Owner.SecurityStack.Count > 0,
                    c => { c.Owner.Trash.Add(c.Owner.SecurityStack[0]); c.Owner.SecurityStack.RemoveAt(0); },
                    c => c.Owner.SecurityStack.Count >= 3, "시큐리티 위 1장을 파기하여");
        if ((m = Regex.Match(cost, @"^trashing (?<n>\d+) cards? (?:in|from) your hand$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            return (c => c.Owner.Hand.Count >= n, c => c.Engine.DiscardFromHand(c.Owner, n), c => c.Owner.Hand.Count >= n + 2, $"패 {n}장을 버려");
        }
        // "trashing 1 card with the [X] trait in your hand" · "trashing 1 [X] trait card from your hand"
        if ((m = Regex.Match(cost, @"^trashing (?<pick>.+?) (?:in|from) your hand$", RegexOptions.IgnoreCase)).Success
            && (ParseCardPick(m.Groups["pick"].Value) ?? ParseNamePick(m.Groups["pick"].Value)) is { } pick)
        {
            var (n, match) = pick;
            return (c => c.Owner.Hand.Count(match) >= n,
                    c =>
                    {
                        foreach (var x in c.Owner.Hand.Where(match).OrderBy(x => x.Level).Take(n).ToList())
                        {
                            c.Owner.Hand.Remove(x);
                            c.Owner.Trash.Add(x);
                        }
                    },
                    c => c.Owner.Hand.Count >= n + 2, $"패에서 조건에 맞는 카드 {n}장을 버려");
        }
        if ((m = Regex.Match(cost, @"^deleting (?<n>\d+) of your (?<other>other )?Digimon$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value); bool other = m.Groups["other"].Success;
            IEnumerable<CardInstance> Victims(EffectContext c) => c.Owner.BattleArea.Where(d => !other || !ReferenceEquals(d, c.Self)).OrderBy(d => d.TopCard.Level).ThenBy(d => d.GetCurrentDP());
            return (c => Victims(c).Count() >= n, c => { foreach (var d in Victims(c).Take(n).ToList()) c.DeleteOwn(d); },
                    c => Victims(c).Take(n).All(d => d.TopCard.Level <= 3), $"자신의 디지몬 {n}마리를 소멸시켜");
        }
        if (Regex.IsMatch(cost, @"^deleting this Digimon$", RegexOptions.IgnoreCase))
            return (c => c.Self != null && c.Owner.BattleArea.Contains(c.Self), c => c.DeleteOwn(c.Self!), _ => false, "이 디지몬을 소멸시켜");
        if (Regex.IsMatch(cost, @"^returning this Tamer to the bottom of the deck$", RegexOptions.IgnoreCase) && card.Type == CardType.Tamer)
            return (c => c.Owner.Tamers.Contains(c.Card),
                    c => { c.Owner.Tamers.Remove(c.Card); c.Owner.SuspendedTamers.Remove(c.Card); c.Owner.MainDeck.Add(c.Card); },
                    _ => true, "이 테이머를 덱 아래로 되돌려");
        if ((m = Regex.Match(cost, @"^paying (?<n>\d+) (?:memory|cost)$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            return (c => true, c => c.AddMemory(-n), c => c.OwnerMemory - n >= 0, $"메모리 {n}을(를) 지불하여");
        }
        return null;
    }

    private static Func<EffectContext, bool>? ParseEventCondition(string cond)
    {
        cond = cond.Trim();
        if (ParseDigimonCondition(cond) is { } dc) return c => dc(c.Self);
        Match m;
        if ((m = Regex.Match(cond, @"^your hand has (?<n>\d+) or (?<dir>fewer|less|more) cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Owner.Hand.Count >= n : c.Owner.Hand.Count <= n; }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>less|fewer|more) memory$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.OwnerMemory >= n : c.OwnerMemory <= n; }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>fewer|less|more) security cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Owner.SecurityStack.Count >= n : c.Owner.SecurityStack.Count <= n; }
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or (?<dir>fewer|less|more) security cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Enemy.SecurityStack.Count >= n : c.Enemy.SecurityStack.Count <= n; }
        if (Regex.IsMatch(cond, @"^your opponent has (?:a|any) Digimon$", RegexOptions.IgnoreCase)) return c => c.Enemy.BattleArea.Count > 0;
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or more Digimon$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Enemy.BattleArea.Count >= n; }
        if (Regex.IsMatch(cond, @"^you have a Tamer$", RegexOptions.IgnoreCase)) return c => c.Owner.Tamers.Count > 0;
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or more Tamers$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Owner.Tamers.Count >= n; }
        if ((m = Regex.Match(cond, @"^you have (?:a|an) (?<kind>Digimon|Tamer) with \[(?<x>[^\]]+)\] in its name$", RegexOptions.IgnoreCase)).Success)
        {
            string x = m.Groups["x"].Value; bool tamer = m.Groups["kind"].Value.Equals("Tamer", StringComparison.OrdinalIgnoreCase);
            return c => tamer ? c.Owner.Tamers.Any(t => TargetSpec.NameHas(t, x)) : c.Owner.BattleArea.Any(d => TargetSpec.NameHas(d.TopCard, x));
        }
        if ((m = Regex.Match(cond, @"^you have (?:a|an) (?:\[(?<x>[^\]]+)\] trait (?<kind>Digimon|Tamer)|(?<kind>Digimon|Tamer) with the \[(?<x>[^\]]+)\] trait)$", RegexOptions.IgnoreCase)).Success)
        {
            string x = m.Groups["x"].Value; bool tamer = m.Groups["kind"].Value.Equals("Tamer", StringComparison.OrdinalIgnoreCase);
            return c => tamer ? c.Owner.Tamers.Any(t => TargetSpec.HasTrait(t, x)) : c.Owner.BattleArea.Any(d => TargetSpec.HasTrait(d.TopCard, x));
        }
        if ((m = Regex.Match(cond, @"^(?:this Digimon|it) has (?<n>\d+) or more digivolution cards$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => (c.Self?.EvolutionSources.Count ?? 0) >= n; }
        if ((m = Regex.Match(cond, @"^(?:there are|you have) (?<n>\d+) or more cards in your trash$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Owner.Trash.Count >= n; }
        if (Regex.IsMatch(cond, @"^it's your turn$", RegexOptions.IgnoreCase)) return c => c.Engine.IsTurnOf(c.Owner);
        if (Regex.IsMatch(cond, @"^you have a Tamer in play$", RegexOptions.IgnoreCase)) return c => c.Owner.Tamers.Count > 0;
        // DNA 진화 · 디지크로스는 아직 엔진에 없으므로 "~했다면"은 언제나 거짓 (특수 메커닉 — 7단계)
        if (Regex.IsMatch(cond, @"^(?:DNA digivolving|DigiXrosing|it DNA digivolved|this Digimon DNA digivolved|DigiXrosing with \d+ or more cards)$", RegexOptions.IgnoreCase)) return _ => false;
        if ((m = Regex.Match(cond, $@"^you have (?:a|an) (?<color>{Colors}) (?<kind>Tamer|Digimon)(?: in play)?$", RegexOptions.IgnoreCase)).Success)
        {
            var color = Enum.Parse<CardColor>(m.Groups["color"].Value, ignoreCase: true); bool tamer = m.Groups["kind"].Value.Equals("Tamer", StringComparison.OrdinalIgnoreCase);
            return c => tamer ? c.Owner.Tamers.Any(t => t.AllColors.Contains(color)) : c.Owner.BattleArea.Any(d => d.TopCard.AllColors.Contains(color));
        }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) security cards?$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); return c => c.Owner.SecurityStack.Count == n; }
        if (Regex.IsMatch(cond, @"^you (?:don't|do not) have (?:a|any) Digimon$", RegexOptions.IgnoreCase)) return c => c.Owner.BattleArea.Count == 0;
        if (Regex.IsMatch(cond, @"^your opponent (?:doesn't|does not) have (?:a|any) Digimon$", RegexOptions.IgnoreCase)) return c => c.Enemy.BattleArea.Count == 0;
        if ((m = Regex.Match(cond, $@"^(?:any of them|it|that Digimon) (?:have|has) (?:the )?(?<x>{TargetSpec.NameList}) traits?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return c => c.Other != null && xs.Any(x => TargetSpec.HasTrait(c.Other.TopCard, x)); }
        if ((m = Regex.Match(cond, $@"^(?:any of them|it|that Digimon) (?:have|has) (?<x>{TargetSpec.NameList}) in (?:its|their) names?$", RegexOptions.IgnoreCase)).Success)
        { var xs = TargetSpec.Names(m.Groups["x"].Value); return c => c.Other != null && xs.Any(x => TargetSpec.NameHas(c.Other.TopCard, x)); }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>fewer|less|more) Tamers$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Owner.Tamers.Count >= n : c.Owner.Tamers.Count <= n; }
        if ((m = Regex.Match(cond, @"^you have (?<n>\d+) or (?<dir>fewer|less|more) cards in your hand$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Owner.Hand.Count >= n : c.Owner.Hand.Count <= n; }
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or (?<dir>fewer|less|more) cards in (?:their|your opponent's) hand$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? c.Enemy.Hand.Count >= n : c.Enemy.Hand.Count <= n; }
        if ((m = Regex.Match(cond, @"^your opponent has (?<n>\d+) or (?<dir>fewer|less|more) memory$", RegexOptions.IgnoreCase)).Success)
        { int n = int.Parse(m.Groups["n"].Value); bool more = m.Groups["dir"].Value == "more"; return c => more ? -c.OwnerMemory >= n : -c.OwnerMemory <= n; }
        return null;
    }

    // =====================================================================
    // 동작
    // =====================================================================

    private enum Duration { None, ThisTurn, OpponentsTurnEnd, OpponentsNextTurnEnd, YourTurnEnd, ThisAttack }

    private static Duration ParseDuration(string text, out string rest)
    {
        rest = text;
        var pairs = new (string Pattern, Duration D)[]
        {
            (@"\s*for the turn$", Duration.ThisTurn),
            (@"\s*for the attack$", Duration.ThisAttack),
            (@"\s*until (?:their|your opponent's|the opponent's) turn ends$", Duration.OpponentsTurnEnd),
            (@"\s*until your turn ends$", Duration.YourTurnEnd),
            (@"\s*until the end of (?:your opponent's|their|the opponent's) next turn$", Duration.OpponentsNextTurnEnd),
            (@"\s*until the end of (?:your opponent's|their|the opponent's) turn$", Duration.OpponentsTurnEnd),
            (@"\s*until the end of your turn$", Duration.YourTurnEnd),
        };
        foreach (var (p, d) in pairs)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (m.Success) { rest = text[..m.Index]; return d; }
        }
        // "For the turn, ~" / "Until the end of your opponent's turn, ~" 앞에 붙는 형태
        var lead = Regex.Match(text, @"^(?<d>for the turn|until the end of (?:your opponent's|their) (?:next )?turn|until the end of your turn), (?<rest>.+)$", RegexOptions.IgnoreCase);
        if (lead.Success)
        {
            rest = lead.Groups["rest"].Value;
            string d = lead.Groups["d"].Value.ToLowerInvariant();
            return d == "for the turn" ? Duration.ThisTurn : d.Contains("next") ? Duration.OpponentsNextTurnEnd : d.EndsWith("your turn") && !d.Contains("opponent") && !d.Contains("their") ? Duration.YourTurnEnd : Duration.OpponentsTurnEnd;
        }
        return Duration.None;
    }

    /// <summary>기간 → TurnSequence 기준 끝나는 턴 (효과 주인의 턴인지에 따라)</summary>
    private static int Until(EffectContext c, Duration d)
    {
        bool mine = c.Engine.IsTurnOf(c.Owner);
        int seq = c.TurnSequence;
        return d switch
        {
            Duration.ThisTurn => seq,
            Duration.OpponentsTurnEnd => mine ? seq + 1 : seq,
            Duration.OpponentsNextTurnEnd => mine ? seq + 1 : seq + 2,
            Duration.YourTurnEnd => mine ? seq : seq + 1,
            _ => seq,
        };
    }

    private static (Action<EffectContext>? Act, string? Error) ParseAction(string s, CardData card, List<EffectTrigger> triggers)
    {
        bool security = triggers.Contains(EffectTrigger.Security);
        string label = string.Join(" · ", triggers.Select(t => Labels.GetValueOrDefault(t, t.ToString())));
        Match m;

        // ---- 이 카드 자체 ([시큐리티]) ----
        if (Regex.IsMatch(s, @"^Add this card to (?:the|your) hand$", RegexOptions.IgnoreCase) && security && card.Type == CardType.Option)
            return (c => { c.ReturnToHand = true; Emit(c, label, "이 카드를 패에 넣는다"); }, null);
        if (Regex.IsMatch(s, @"^Activate this card's \[Main\] effects?$", RegexOptions.IgnoreCase) && security && card.Type == CardType.Option)
            return (c => ActivateMainEffects(c, label), null);

        // ---- 동작 키워드 ----
        if ((m = Regex.Match(s, @"^＜Draw (?<n>\d+)＞(?: and trash (?<t>\d+) cards? (?:in|from) your hand)?$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value); int trash = m.Groups["t"].Success ? int.Parse(m.Groups["t"].Value) : 0;
            return (c =>
            {
                int drawn = 0;
                for (int i = 0; i < n; i++) if (c.Draw() != null) drawn++;
                string extra = trash > 0 ? $", 패 {c.Engine.DiscardFromHand(c.Owner, trash)}장 파기" : "";
                Emit(c, label, $"≪{n} 드로우≫ ({drawn}장){extra}");
            }, null);
        }
        if ((m = Regex.Match(s, @"^＜Recovery \+(?<n>\d+)\s*(?:\(Deck\))?\s*＞$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            return (c => Emit(c, label, $"≪리커버리 +{n}≫ 시큐리티 {c.Recovery(n)}장 추가"), null);
        }
        if ((m = Regex.Match(s, @"^＜De-Digivolve (?<n>\d+)＞ (?<t>.+)$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null || !target.Opponent) return (null, $"미지원 대상 (퇴화): {m.Groups["t"].Value}");
            return (c =>
            {
                var parts = new List<string>();
                foreach (var t in Pick(target, c, security).Where(d => d.EvolutionSources.Count > 0 && d.TopCard.Level > 3))
                {
                    string before = t.TopCard.Name;
                    c.DeDigivolveEnemy(t, n);
                    parts.Add($"[{before}] → [{t.TopCard.Name}]");
                }
                Emit(c, label, parts.Count > 0 ? $"≪퇴화 {n}≫ {string.Join(", ", parts)}" : "≪퇴화≫할 대상이 없습니다");
            }, null);
        }

        // ---- 메모리 ----
        if ((m = Regex.Match(s, @"^(?<v>Gain|Lose) (?<n>\d+) memory$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value) * (m.Groups["v"].Value.Equals("Lose", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
            return (c => { c.AddMemory(n); Emit(c, label, $"메모리 {(n > 0 ? "+" : "")}{n} (현재 {c.OwnerMemory})"); }, null);
        }
        if ((m = Regex.Match(s, @"^Set (?:it|your memory) to (?<n>\d+)$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            return (c => { c.AddMemory(n - c.OwnerMemory); Emit(c, label, $"메모리를 {n}로 한다"); }, null);
        }

        // ---- 시큐리티 · 덱 · 패 ----
        if ((m = Regex.Match(s, @"^Trash (?:the top (?:card|(?<n>\d+) cards) of your opponent's security stack|your opponent's top (?:security card|(?<n>\d+) security cards)|their top (?:security card|(?<n>\d+) security cards))$", RegexOptions.IgnoreCase)).Success)
        {
            int n = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 1;
            return (c =>
            {
                int k = 0;
                while (k < n && c.Enemy.SecurityStack.Count > 0) { c.Enemy.Trash.Add(c.Enemy.SecurityStack[0]); c.Enemy.SecurityStack.RemoveAt(0); k++; }
                Emit(c, label, $"상대 시큐리티 위에서 {k}장 파기");
            }, null);
        }
        if ((m = Regex.Match(s, @"^Trash the top (?:card|(?<n>\d+) cards) of your deck$", RegexOptions.IgnoreCase)).Success)
        {
            int n = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 1;
            return (c => c.Mill(n, $"{c.Card.Name} [{label}]"), null);
        }
        if ((m = Regex.Match(s, @"^Trash (?<n>\d+) cards? (?:in|from) your hand$", RegexOptions.IgnoreCase)).Success)
        {
            int n = int.Parse(m.Groups["n"].Value);
            return (c => Emit(c, label, $"패 {c.Engine.DiscardFromHand(c.Owner, n)}장 파기"), null);
        }
        if ((m = Regex.Match(s, @"^Reveal the top (?<n>\d+) cards? of your deck\.? Add (?<adds>.+?) among them to (?:the|your) hand\.? (?<rest>.+?)\.?$", RegexOptions.IgnoreCase)).Success)
            return ParseReveal(m, label);

        if ((m = Regex.Match(s, @"^Add your (?<pos>top|bottom) security card to (?:the|your) hand$", RegexOptions.IgnoreCase)).Success)
        {
            bool top = m.Groups["pos"].Value.Equals("top", StringComparison.OrdinalIgnoreCase);
            return (c =>
            {
                var sec = c.Owner.SecurityStack;
                if (sec.Count == 0) { Emit(c, label, "시큐리티가 없습니다"); return; }
                int i = top ? 0 : sec.Count - 1;
                var card = sec[i];
                sec.RemoveAt(i);
                c.Owner.Hand.Add(card);
                Emit(c, label, $"시큐리티 {(top ? "맨 위" : "맨 아래")} 1장을 패에 넣는다");
            }, null);
        }
        if ((m = Regex.Match(s, @"^(?:Return|Add) (?<pick>.+?) from your trash to (?:the|your) hand$", RegexOptions.IgnoreCase)).Success)
        {
            var pick = ParseCardPick(m.Groups["pick"].Value) ?? ParseNamePick(m.Groups["pick"].Value);
            if (pick == null) return (null, $"미지원 회수 조건: {m.Groups["pick"].Value}");
            var (count, match) = pick.Value;
            return (c =>
            {
                var cards = c.Owner.Trash.Where(match).OrderByDescending(x => x.Level).ThenByDescending(x => x.PlayCost).Take(count).ToList();
                foreach (var card in cards) { c.Owner.Trash.Remove(card); c.Owner.Hand.Add(card); }
                Emit(c, label, cards.Count > 0 ? $"트래시의 [{string.Join(", ", cards.Select(x => x.Name))}] 패로 회수" : "회수할 카드가 없습니다");
            }, null);
        }
        if ((m = Regex.Match(s, @"^Play (?<pick>.+?) from (?<from>your hand or (?:from your )?trash|your hand or from your trash|your trash or hand|your hand|your trash|this Digimon's digivolution cards) without paying (?:the|its) (?:memory )?cost$", RegexOptions.IgnoreCase)).Success)
        {
            var pick = ParseCardPick(m.Groups["pick"].Value) ?? ParseNamePick(m.Groups["pick"].Value);
            if (pick == null) return (null, $"미지원 등장 조건: {m.Groups["pick"].Value}");
            var (count, match) = pick.Value;
            string from = m.Groups["from"].Value.ToLowerInvariant();
            bool fromHand = from.Contains("hand"), fromTrash = from.Contains("trash"), fromSources = from.Contains("digivolution");
            return (c =>
            {
                var played = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    var pool = new List<(CardData Card, List<CardData>? Zone)>();
                    if (fromHand) pool.AddRange(c.Owner.Hand.Where(x => x.Type is CardType.Digimon or CardType.Tamer && match(x)).Select(x => (x, (List<CardData>?)c.Owner.Hand)));
                    if (fromTrash) pool.AddRange(c.Owner.Trash.Where(x => x.Type is CardType.Digimon or CardType.Tamer && match(x)).Select(x => (x, (List<CardData>?)c.Owner.Trash)));
                    if (fromSources && c.Self != null) pool.AddRange(c.Self.EvolutionSources.Where(x => x.Type == CardType.Digimon && match(x)).Select(x => (x, (List<CardData>?)null)));
                    var best = pool.OrderByDescending(p => p.Card.Level).ThenByDescending(p => p.Card.PlayCost).FirstOrDefault();
                    if (best.Card == null || c.Owner.BattleArea.Count >= 12) break;
                    if (best.Zone != null) best.Zone.Remove(best.Card);
                    else if (c.Self?.TakeSource(x => ReferenceEquals(x, best.Card)) == null) break;
                    c.Engine.PlayWithoutCost(c.Owner, best.Card);
                    played.Add(best.Card.Name);
                }
                Emit(c, label, played.Count > 0 ? $"[{string.Join(", ", played)}] 코스트 없이 등장" : "등장시킬 카드가 없습니다");
            }, null);
        }
        if ((m = Regex.Match(s, @"^Trash the (?<pos>top|bottom) (?:(?<n>\d+) )?digivolution cards? of (?<t>.+)$", RegexOptions.IgnoreCase)).Success)
        {
            bool top = m.Groups["pos"].Value.Equals("top", StringComparison.OrdinalIgnoreCase);
            int n = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 1;
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (진화원 파기): {m.Groups["t"].Value}");
            return (c =>
            {
                var parts = new List<string>();
                foreach (var d in Pick(target, c, security).Where(d => d.EvolutionSources.Count > 0))
                {
                    var zone = c.Engine.OwnerOf(d);
                    var removed = top ? c.Engine.TrashTopSources(zone, d, n) : d.RemoveBottomSources(n);
                    if (!top) zone.Trash.AddRange(removed);
                    parts.Add($"[{d.TopCard.Name}] {removed.Count}장");
                }
                Emit(c, label, parts.Count > 0 ? $"진화원 파기: {string.Join(", ", parts)}" : "진화원을 파기할 대상이 없습니다");
            }, null);
        }
        if (security && card.Type == CardType.Digimon && Regex.IsMatch(s, @"^At the end of the battle, play this card without paying (?:the|its) cost$", RegexOptions.IgnoreCase))
            return (c => { c.PlayAfterBattle = true; Emit(c, label, "배틀이 끝나면 이 카드를 코스트 없이 등장"); }, null);

        // ---- 대상을 가진 동작 ----
        if ((m = Regex.Match(s, @"^Delete (?<t>.+)$", RegexOptions.IgnoreCase)).Success)
        {
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (소멸): {m.Groups["t"].Value}");
            return (c =>
            {
                var victims = Pick(target, c, security);
                var names = new List<string>();
                foreach (var v in victims)
                {
                    bool gone = c.Enemy.BattleArea.Contains(v) ? c.DeleteEnemy(v) : c.Engine.DeleteFrom(c.Owner, v);
                    if (gone) names.Add(v.TopCard.Name);
                }
                Emit(c, label, names.Count > 0 ? $"[{string.Join(", ", names)}] 소멸" : "소멸시킬 대상이 없습니다", attackerRemoved: victims.Contains(c.Other!) && names.Count > 0);
            }, null);
        }
        if ((m = Regex.Match(s, @"^(?<v>Suspend|Unsuspend) (?<t>.+)$", RegexOptions.IgnoreCase)).Success)
        {
            bool suspend = m.Groups["v"].Value.Equals("Suspend", StringComparison.OrdinalIgnoreCase);
            string tt = Regex.Replace(m.Groups["t"].Value, @"Digimon or Tamers", "Digimon", RegexOptions.IgnoreCase);
            var target = TargetSpec.Parse(tt);
            if (target == null) return (null, $"미지원 대상 ({(suspend ? "레스트" : "액티브")})");
            return (c =>
            {
                var list = Pick(target, c, security).Where(d => d.IsSuspended != suspend).ToList();
                if (target.Count > 0) list = list.Take(target.Count).ToList();
                foreach (var d in list)
                {
                    if (suspend) c.Engine.Suspend(d);
                    else d.IsSuspended = false;
                }
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] {(suspend ? "레스트" : "액티브")}" : "대상이 없습니다");
            }, null);
        }
        if ((m = Regex.Match(s, @"^Return (?<t>.+?) to (?:the|its owner's|their owners'?|the owner's|your) hands?$", RegexOptions.IgnoreCase)).Success)
        {
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (패로): {m.Groups["t"].Value}");
            return (c =>
            {
                var list = Pick(target, c, security);
                foreach (var d in list) c.Engine.ReturnToHand(c.Engine.OwnerOf(d), d);
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] 패로" : "대상이 없습니다", attackerRemoved: list.Contains(c.Other!));
            }, null);
        }
        if ((m = Regex.Match(s, @"^(?:Place|Return) (?<t>.+?) (?:at|on|to) the bottom of (?:its owner's|their owners'?|the owner's|the|your opponent's) decks?$", RegexOptions.IgnoreCase)).Success)
        {
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (덱 아래로): {m.Groups["t"].Value}");
            return (c =>
            {
                var list = Pick(target, c, security);
                foreach (var d in list) c.Engine.BottomDeck(c.Engine.OwnerOf(d), d);
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] 덱 아래로" : "대상이 없습니다", attackerRemoved: list.Contains(c.Other!));
            }, null);
        }

        // "Give 1 of your opponent's Digimon ＜Security A. -1＞ until their turn ends" = "~ gains ＜…＞"
        var give = Regex.Match(s, @"^Give (?<t>.+?) (?<kws>＜.+)$", RegexOptions.IgnoreCase);
        if (give.Success)
            s = $"{give.Groups["t"].Value} gains {give.Groups["kws"].Value}";
        // "This Digimon unsuspends" = "Unsuspend this Digimon"
        var unsus = Regex.Match(s, @"^(?<t>.+?) unsuspends?$", RegexOptions.IgnoreCase);
        if (unsus.Success && !s.StartsWith("Unsuspend", StringComparison.OrdinalIgnoreCase))
            return ParseAction($"Unsuspend {unsus.Groups["t"].Value}", card, triggers);
        // "Change the attack target to this Digimon"
        if (Regex.IsMatch(s, @"^Change the attack target to this Digimon$", RegexOptions.IgnoreCase))
            return (c =>
            {
                if (c.Self != null && c.Engine.RedirectAttack(c.Owner, c.Self))
                    Emit(c, label, $"어택 대상을 [{c.Self.TopCard.Name}]으로 바꿨습니다");
            }, null);

        var dur = ParseDuration(s, out string core);

        // "~ gains ＜Blocker＞ and gets +2000 DP" · "~ get -5000 DP and gain ＜Security A. -1＞"
        var combo = Regex.Match(core, @"^(?<t>.+?) (?:gains? (?<kws>＜[^.]+?＞)\s*,?\s*and gets? (?<sign>[+-])(?<n>\d+) DP|gets? (?<sign>[+-])(?<n>\d+) DP\s*,?\s*and gains? (?<kws>＜[^.]+?＞))$", RegexOptions.IgnoreCase);
        if (combo.Success)
        {
            var target = TargetSpec.Parse(combo.Groups["t"].Value);
            var grants = ParseKeywordList(combo.Groups["kws"].Value);
            if (target == null || grants == null) return (null, "미지원 키워드 부여 + DP");
            int dp = int.Parse(combo.Groups["n"].Value) * (combo.Groups["sign"].Value == "-" ? -1 : 1);
            var d2 = dur == Duration.None ? Duration.ThisTurn : dur;
            return (c =>
            {
                int until = Until(c, d2);
                var list = Pick(target, c, security);
                foreach (var d in list)
                {
                    foreach (var (k, sa) in grants)
                    {
                        if (k is { } kw) d.TimedKeywords.Add((kw, until));
                        else d.TimedSecurityAttackBuffs.Add((sa, until));
                    }
                    c.Engine.ApplyDpChange(d, dp, until, $"{c.Card.Name} [{label}]");
                }
                c.RecalculateFieldBuffs();
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] DP {(dp > 0 ? "+" : "")}{dp:N0} · 키워드 획득" : "대상이 없습니다");
            }, null);
        }
        if ((m = Regex.Match(core, @"^(?<t>.+?) gets? (?<sign>[+-])(?<n>\d+) DP$", RegexOptions.IgnoreCase)).Success)
        {
            if (dur == Duration.None) dur = Duration.ThisTurn;   // 발동 효과의 기간 없는 DP 변화 = 이 턴 동안
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (DP): {m.Groups["t"].Value}");
            int dp = int.Parse(m.Groups["n"].Value) * (m.Groups["sign"].Value == "-" ? -1 : 1);
            return (c =>
            {
                var list = Pick(target, c, security);
                bool removed = false;
                foreach (var d in list)
                {
                    if (dur == Duration.ThisAttack) { d.AttackDpBonus += dp; continue; }
                    if (c.Engine.ApplyDpChange(d, dp, Until(c, dur), $"{c.Card.Name} [{label}]") && ReferenceEquals(d, c.Other)) removed = true;
                }
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] DP {(dp > 0 ? "+" : "")}{dp:N0}" : "대상이 없습니다", attackerRemoved: removed);
            }, null);
        }
        if ((m = Regex.Match(core, @"^(?<t>.+?) gains? (?<kws>＜.+)$", RegexOptions.IgnoreCase)).Success)
        {
            if (dur == Duration.None) dur = Duration.ThisTurn;
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            var grants = ParseKeywordList(m.Groups["kws"].Value);
            if (target == null || grants == null) return (null, "미지원 키워드 부여");
            return (c =>
            {
                int until = Until(c, dur);
                var list = Pick(target, c, security);
                foreach (var d in list)
                    foreach (var (k, sa) in grants)
                    {
                        if (k is { } kw) d.TimedKeywords.Add((kw, until));
                        else if (dur == Duration.ThisAttack) d.AttackSecurityAttackBonus += sa;
                        else d.TimedSecurityAttackBuffs.Add((sa, until));
                    }
                c.RecalculateFieldBuffs();
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] 키워드 획득" : "대상이 없습니다");
            }, null);
        }
        if ((m = Regex.Match(core, @"^(?<t>.+?) can't (?<what>attack or block|attack and block|attack|block)$", RegexOptions.IgnoreCase)).Success)
        {
            if (dur == Duration.None) return (null, "미지원: 기간 없는 어택 · 블록 불가");
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            if (target == null) return (null, $"미지원 대상 (어택 · 블록 불가): {m.Groups["t"].Value}");
            string what = m.Groups["what"].Value.ToLowerInvariant();
            return (c =>
            {
                int until = Until(c, dur);
                var list = Pick(target, c, security);
                foreach (var d in list)
                {
                    if (what.Contains("attack")) d.CannotAttackUntil = Math.Max(d.CannotAttackUntil, until);
                    if (what.Contains("block")) d.CannotBlockUntil = Math.Max(d.CannotBlockUntil, until);
                }
                Emit(c, label, list.Count > 0 ? $"[{string.Join(", ", list.Select(d => d.TopCard.Name))}] {(what.Contains("block") && what.Contains("attack") ? "어택 · 블록" : what.Contains("block") ? "블록" : "어택")} 불가" : "대상이 없습니다");
            }, null);
        }

        // "＜Draw 1＞ and gain 1 memory" — 두 동작이 and로 이어진 문장
        foreach (Match and in Regex.Matches(s, @"\s+and\s+", RegexOptions.IgnoreCase))
        {
            string left = s[..and.Index].Trim(), right = s[(and.Index + and.Length)..].Trim();
            if (left.Length == 0 || right.Length == 0) continue;
            right = char.ToUpperInvariant(right[0]) + right[1..];
            var a = ParseAction(left, card, triggers);
            if (a.Error != null) continue;
            var b = ParseAction(right, card, triggers);
            if (b.Error != null) continue;
            var (actA, actB) = (a.Act!, b.Act!);
            return (c => { actA(c); actB(c); }, null);
        }

        // "1 of your Digimon may digivolve into [Greymon] in the hand without paying the cost"
        if ((m = Regex.Match(s, @"^(?<t>this Digimon|\d+ of your Digimon(?: with [^,]+?)?) (?:may )?digivolves? into (?<pick>.+?) in (?:the|your) hand(?: for (?:a digivolution cost of (?<cost>\d+)|its digivolution cost))? ?(?<free>without paying (?:the|its) (?:memory |digivolution )?cost|with the digivolution cost reduced by (?<red>\d+))?$", RegexOptions.IgnoreCase)).Success)
        {
            var target = TargetSpec.Parse(m.Groups["t"].Value);
            var pick = ParseCardPick("1 " + m.Groups["pick"].Value) ?? ParseCardPick(m.Groups["pick"].Value) ?? ParseNamePick("1 " + m.Groups["pick"].Value);
            if (target == null || pick == null) return (null, $"미지원 진화 대상: {s}");
            var match = pick.Value.Match;
            bool free = m.Groups["free"].Value.StartsWith("without", StringComparison.OrdinalIgnoreCase);
            int reduce = m.Groups["red"].Success ? int.Parse(m.Groups["red"].Value) : 0;
            int? fixedCost = m.Groups["cost"].Success ? int.Parse(m.Groups["cost"].Value) : null;
            return (c =>
            {
                foreach (var d in Pick(target, c, security).Where(x => c.Owner.BattleArea.Contains(x)))
                {
                    var evo = c.Owner.Hand.Where(x => match(x) && ActionValidator.ValidateEvolution(d, x).CanExecute)
                        .OrderByDescending(x => x.Level).ThenByDescending(x => x.DP).FirstOrDefault();
                    if (evo == null) continue;
                    int cost = free ? 0 : fixedCost ?? Math.Max(0, evo.EvoCost - reduce);
                    c.Engine.EvolveByEffect(c.Owner, d, evo, cost);
                    Emit(c, label, $"[{d.TopCard.Name}]으로 진화 (코스트 {cost})");
                    return;
                }
                Emit(c, label, "진화할 수 있는 카드가 없습니다");
            }, null);
        }

        return (null, $"문장 파서(3단계) 대상: {s}");
    }

    /// <summary>대상을 고르고 앞 문장 대상("that Digimon")으로 기억한다</summary>
    private static List<CardInstance> Pick(TargetSpec target, EffectContext c, bool security)
    {
        var list = target.Resolve(c, Preferred(c, security));
        c.LastTargets = list;
        return list;
    }

    /// <summary>대상 고르기에서 먼저 볼 디지몬: [시큐리티]면 어택 중인 디지몬, 그 밖엔 UI가 고른 대상 → 어택 대상</summary>
    private static CardInstance? Preferred(EffectContext c, bool security) => security ? c.Other : c.ChosenTarget ?? c.Other;

    /// <summary>효과 결과 기록: [메인] · [시큐리티]는 결과 메시지로, 그 밖엔 로그로</summary>
    private static void Emit(EffectContext c, string label, string text, bool attackerRemoved = false)
    {
        if (attackerRemoved) c.AttackerRemoved = true;
        if (label.Contains("시큐리티") || label == "메인")
            c.Message = c.Message.Length == 0 ? text : $"{c.Message} / {text}";
        else
            c.Log($"[{label}] {c.Card.Name}: {text}");
    }

    private static void ActivateMainEffects(EffectContext c, string label)
    {
        var script = CardScripts.For(c.Card);
        foreach (var e in script?.Effects.Where(e => e.Trigger == EffectTrigger.Main) ?? Enumerable.Empty<CardEffect>())
            e.Resolve(c);
        if (c.Summary.Length == 0) c.Summary = "[메인] 효과 발휘";
    }

    // ---- 덱 위 공개 ----

    private static (Action<EffectContext>? Act, string? Error) ParseReveal(Match m, string label)
    {
        int n = int.Parse(m.Groups["n"].Value);
        var picks = new List<(int Count, Func<CardData, bool> Match)>();
        foreach (var part in Regex.Split(m.Groups["adds"].Value, @"\s+and\s+(?=(?:up to )?\d+ )", RegexOptions.IgnoreCase))
        {
            var pick = ParseCardPick(part) ?? ParseNamePick(part);
            if (pick == null) return (null, $"미지원 공개 조건 ({part})");
            picks.Add(pick.Value);
        }
        string rest = m.Groups["rest"].Value.TrimEnd('.');
        bool trashRest;
        if (Regex.IsMatch(rest, @"^(?:Return|Place) the (?:rest|remaining cards) (?:to|at|on) (?:the )?(?:top or |either the top or )?(?:bottom|top) of (?:the |your )?deck(?: in any order)?$|^Place the (?:rest|remaining cards|cards) (?:on|at) (?:the )?(?:top|bottom) of (?:the |your )?deck(?: in any order)?$", RegexOptions.IgnoreCase)) trashRest = false;
        else if (Regex.IsMatch(rest, @"^Trash the (?:rest|remaining cards)$", RegexOptions.IgnoreCase)) trashRest = true;
        else return (null, $"미지원 공개 후 처리: {rest}");

        return (c =>
        {
            var deck = c.Owner.MainDeck;
            var revealed = deck.Take(n).ToList();
            deck.RemoveRange(0, revealed.Count);
            var taken = new List<CardData>();
            foreach (var (count, match) in picks)
            {
                foreach (var card in revealed.Where(x => !taken.Contains(x) && match(x)).OrderByDescending(x => x.Level).ThenByDescending(x => x.DP).Take(count).ToList())
                    taken.Add(card);
            }
            foreach (var t in taken) { revealed.Remove(t); c.Owner.Hand.Add(t); }
            if (trashRest) c.Owner.Trash.AddRange(revealed); else deck.AddRange(revealed);
            Emit(c, label, taken.Count > 0 ? $"덱 위 {n}장 공개 → [{string.Join(", ", taken.Select(t => t.Name))}] 패에 추가" : $"덱 위 {n}장 공개 → 조건에 맞는 카드 없음");
        }, null);
    }

    /// <summary>"1 [Agumon]" · "1 [Agumon] or [Gabumon]" · "up to 2 [Greymon]" — 카드 이름</summary>
    private static (int Count, Func<CardData, bool> Match)? ParseNamePick(string part)
    {
        var m = Regex.Match(part.Trim(), @"^(?:up to )?(?<n>\d+|a|an) (?<names>\[[^\]]+\](?:,? (?:or )?\[[^\]]+\])*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        int count = int.TryParse(m.Groups["n"].Value, out int k) ? k : 1;
        var names = Regex.Matches(m.Groups["names"].Value, @"\[([^\]]+)\]").Select(x => x.Groups[1].Value).ToList();
        return (count, c => names.Any(n => c.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || TargetSpec.NameHas(c, n)));
    }

    private const string Colors = "red|blue|yellow|green|black|purple|white";

    /// <summary>
    /// 카드 고르기 조건: "1 card with [Greymon] in its name" · "up to 2 Tamer cards" · "1 [Royal Knight] trait Digimon card" ·
    /// "1 green or blue level 4 or lower Digimon card" · "1 X card or 1 Y card"(둘 중 하나) …
    /// </summary>
    private static (int Count, Func<CardData, bool> Match)? ParseCardPick(string part)
    {
        // "1 A card or 1 B card" — 어느 한쪽 조건
        var alternatives = Regex.Split(part.Trim(), @"\s+or\s+(?=(?:up to )?\d+ )", RegexOptions.IgnoreCase);
        if (alternatives.Length > 1)
        {
            var parsed = alternatives.Select(ParseSingleCardPick).ToList();
            if (parsed.Any(p => p == null)) return null;
            return (parsed.Max(p => p!.Value.Count), c => parsed.Any(p => p!.Value.Match(c)));
        }
        return ParseSingleCardPick(part);
    }

    private static (int Count, Func<CardData, bool> Match)? ParseSingleCardPick(string part)
    {
        var m = Regex.Match(part.Trim(),
            $@"^(?:up to )?(?<n>\d+|a|an) (?<adj>(?:(?:{TargetSpec.NameList}(?: traits?)?|level \d+(?: or (?:lower|higher))?|play costs? \d+ or (?:lower|higher|less|more)|(?:{Colors})(?:(?:,| or| and/or|/) (?:{Colors}))*)\s+)*)(?<kind>Digimon|Tamer|Option)?\s*(?:cards?)?(?<filter>.*)$",
            RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        int count = int.TryParse(m.Groups["n"].Value, out int k) ? k : 1;
        var filters = new List<Func<CardData, bool>>();
        string kind = m.Groups["kind"].Value.ToLowerInvariant();
        if (kind == "digimon") filters.Add(c => c.Type == CardType.Digimon);
        else if (kind == "tamer") filters.Add(c => c.Type == CardType.Tamer);
        else if (kind == "option") filters.Add(c => c.Type == CardType.Option);

        string adj = m.Groups["adj"].Value;
        foreach (Match a in Regex.Matches(adj, $@"(?<names>{TargetSpec.NameList})(?<trait> traits?)?|level (?<lv>\d+)(?: or (?<dir>lower|higher))?|play costs? (?<pc>\d+) or (?<pdir>lower|higher|less|more)|(?<colors>(?:{Colors})(?:(?:,| or| and/or|/) (?:{Colors}))*)", RegexOptions.IgnoreCase))
        {
            if (a.Groups["names"].Success)
            {
                var xs = TargetSpec.Names(a.Groups["names"].Value);
                filters.Add(a.Groups["trait"].Success ? c => xs.Any(x => TargetSpec.HasTrait(c, x)) : c => xs.Any(x => TargetSpec.NameHas(c, x) || TargetSpec.HasTrait(c, x)));
            }
            else if (a.Groups["pc"].Success)
            {
                int pc = int.Parse(a.Groups["pc"].Value); bool low = a.Groups["pdir"].Value is "lower" or "less";
                filters.Add(c => low ? c.PlayCost <= pc : c.PlayCost >= pc);
            }
            else if (a.Groups["pc"].Success)
            {
                int pc = int.Parse(a.Groups["pc"].Value); bool low = a.Groups["pdir"].Value is "lower" or "less";
                filters.Add(c => low ? c.PlayCost <= pc : c.PlayCost >= pc);
            }
            else if (a.Groups["lv"].Success)
            {
                int lv = int.Parse(a.Groups["lv"].Value); string dir = a.Groups["dir"].Value.ToLowerInvariant();
                filters.Add(dir == "lower" ? c => c.Level <= lv : dir == "higher" ? c => c.Level >= lv : c => c.Level == lv);
            }
            else if (a.Groups["colors"].Success)
            {
                var colors = Regex.Matches(a.Groups["colors"].Value, Colors, RegexOptions.IgnoreCase).Select(x => Enum.Parse<CardColor>(x.Value, ignoreCase: true)).ToList();
                filters.Add(c => c.AllColors.Any(colors.Contains));
            }
        }

        string rest = m.Groups["filter"].Value.Trim();
        while (rest.Length > 0)
        {
            Match f;
            if ((f = Regex.Match(rest, $@"^(?:with |and |or |,\s*)?(?<x>{TargetSpec.NameList}) in (?:its|their) names?", RegexOptions.IgnoreCase)).Success)
            { var xs = TargetSpec.Names(f.Groups["x"].Value); filters.Add(c => xs.Any(x => TargetSpec.NameHas(c, x))); }
            else if ((f = Regex.Match(rest, $@"^(?:with |and |or |,\s*)?(?:the )?(?<x>{TargetSpec.NameList}) traits?|^(?:with |,\s*)?(?<x>{TargetSpec.NameList}) in any of its traits", RegexOptions.IgnoreCase)).Success)
            { var xs = TargetSpec.Names(f.Groups["x"].Value); filters.Add(c => xs.Any(x => TargetSpec.HasTrait(c, x))); }
            else if ((f = Regex.Match(rest, $@"^(?:with |and |or |,\s*)?(?<x>{TargetSpec.NameList}) in (?:its|their) texts?", RegexOptions.IgnoreCase)).Success)
            { var xs = TargetSpec.Names(f.Groups["x"].Value); filters.Add(c => xs.Any(x => TargetSpec.TextHas(c, x))); }
            else if ((f = Regex.Match(rest, @"^(?:with |and |,\s*)?(?:a )?play costs? of (?<n>\d+) or (?<dir>less|more)", RegexOptions.IgnoreCase)).Success)
            { int v = int.Parse(f.Groups["n"].Value); bool less = f.Groups["dir"].Value == "less"; filters.Add(c => less ? c.PlayCost <= v : c.PlayCost >= v); }
            else if ((f = Regex.Match(rest, @"^(?:with |and |,\s*)?(?<n>\d+) DP or (?<dir>less|more)", RegexOptions.IgnoreCase)).Success)
            { int v = int.Parse(f.Groups["n"].Value); bool less = f.Groups["dir"].Value == "less"; filters.Add(c => less ? c.DP <= v : c.DP >= v); }
            else if ((f = Regex.Match(rest, @"^(?:with |and |,\s*)?(?:a )?level (?:of )?(?<n>\d+)(?: or (?<dir>lower|higher|less|more))?", RegexOptions.IgnoreCase)).Success)
            {
                int v = int.Parse(f.Groups["n"].Value); string dir = f.Groups["dir"].Value.ToLowerInvariant();
                filters.Add(dir is "lower" or "less" ? c => c.Level <= v : dir is "higher" or "more" ? c => c.Level >= v : c => c.Level == v);
            }
            else if ((f = Regex.Match(rest, $@"^(?:,\s*)?other than (?<x>{TargetSpec.NameList})", RegexOptions.IgnoreCase)).Success)
            { var xs = TargetSpec.Names(f.Groups["x"].Value); filters.Add(c => !xs.Any(x => c.Name.Equals(x, StringComparison.OrdinalIgnoreCase))); }
            else if ((f = Regex.Match(rest, $@"^(?<x>{TargetSpec.NameList})", RegexOptions.IgnoreCase)).Success)
            { var xs = TargetSpec.Names(f.Groups["x"].Value); filters.Add(c => xs.Any(x => TargetSpec.NameHas(c, x) || TargetSpec.HasTrait(c, x))); }
            else if ((f = Regex.Match(rest, @"^with (?:an? )?inherited effects?", RegexOptions.IgnoreCase)).Success)
            { filters.Add(c => c.InheritedEffectText.Trim().Length > 0); }
            else if ((f = Regex.Match(rest, @"^with (?:a )?(?:memory |play )?cost of (?<n>d+)$", RegexOptions.IgnoreCase)).Success)
            { int v = int.Parse(f.Groups["n"].Value); filters.Add(c => c.PlayCost == v); }
            else if ((f = Regex.Match(rest, @"^suspended", RegexOptions.IgnoreCase)).Success)
            { /* "suspended" 등장 — 레스트 상태로 등장 (지금은 액티브로 등장) */ }
            else return null;
            rest = rest[f.Length..].Trim().TrimStart(',').Trim();
        }
        return (count, c => filters.All(f => f(c)));
    }
}
