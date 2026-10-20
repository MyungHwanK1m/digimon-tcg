using System;
using System.Linq;
using System.Text.RegularExpressions;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Ai;

/// <summary>
/// AI의 한 수 앞 평가 — "이 어택 · 효과를 하면 상대 필드 · 시큐리티가 어떻게 되나"를 상태를 바꾸지 않고 점수로 어림한다.
/// 점수 단위는 대략 "디지몬 한 마리의 가치" (DP 1000 = 1, Lv 1 = 1).
/// 시큐리티는 뒷면이므로 내용을 보지 않고, 어택하는 디지몬의 DP로 시큐리티 디지몬에게 질 확률만 어림한다.
/// </summary>
internal static class AiBoard
{
    /// <summary>필드 디지몬 한 마리의 가치 (잃거나 없앨 때의 점수)</summary>
    public static double Value(CardInstance d) =>
        d.GetCurrentDP() / 1000.0
        + d.TopCard.Level
        + Math.Min(d.EvolutionSources.Count, 5) * 0.6
        + (d.HasBlocker ? 1.5 : 0);

    /// <summary>시큐리티 1장의 가치 — 상대 시큐리티가 적을수록 한 장이 더 크다 (다이렉트 어택에 가까워짐)</summary>
    public static double SecurityCardValue(int remainingSecurity) => 3.0 + Math.Max(0, 5 - remainingSecurity) * 0.8;

    /// <summary>시큐리티 체크 checks장 중 한 번이라도 시큐리티 디지몬 · 옵션에 당해 소멸할 어림 확률</summary>
    public static double SecurityDeathChance(int attackerDp, int checks)
    {
        double perCheck = attackerDp >= 10000 ? 0.08
            : attackerDp >= 8000 ? 0.18
            : attackerDp >= 6000 ? 0.30
            : attackerDp >= 4000 ? 0.45
            : 0.60;
        return 1 - Math.Pow(1 - perCheck, Math.Max(1, checks));
    }

    /// <summary>
    /// 이 어택을 플레이어가 블록할 ≪블로커≫ 어림: 블록해서 어택하는 디지몬을 없앨 수 있을 때(같은 DP 포함)는 막고,
    /// 시큐리티가 0장이면 희생해서라도 막는다고 본다.
    /// </summary>
    public static CardInstance? ExpectedBlocker(GameState state, CardInstance attacker, CardInstance? target)
    {
        var candidates = EffectRules.BlockCandidates(state, state.Player.BattleArea.Where(d => !ReferenceEquals(d, target)), attacker);
        if (candidates.Count == 0)
            return null;
        int atk = attacker.GetCurrentDP();
        var stopper = candidates.Where(b => b.GetCurrentDP() >= atk).OrderBy(Value).FirstOrDefault();
        if (stopper != null)
            return stopper;
        return target == null && state.Player.SecurityStack.Count == 0 ? candidates.OrderBy(Value).First() : null;
    }

    /// <summary>attacker와 defender의 배틀 결과 점수 (AI 쪽 득실: 이기면 상대 가치만큼 +, 지면 내 가치만큼 −, 같으면 둘 다)</summary>
    public static double BattleScore(CardInstance attacker, CardInstance defender, out bool attackerSurvives)
    {
        int a = attacker.GetCurrentDP(), d = defender.GetCurrentDP();
        attackerSurvives = a > d;
        if (a > d) return Value(defender);
        if (a < d) return -Value(attacker);
        return Value(defender) - Value(attacker);
    }

    /// <summary>
    /// 어택 한 번의 어림 점수. 블록 · 시큐리티 체크 · 다음 플레이어 턴에 레스트 상태로 맞을 위험까지 본다.
    /// 다이렉트 어택(시큐리티 0장, 블록 없음)은 승리이므로 매우 큰 점수.
    /// </summary>
    public static double AttackScore(GameState state, CardInstance attacker, CardInstance? target)
    {
        var player = state.Player;
        var blocker = ExpectedBlocker(state, attacker, target);
        double score;
        bool survives;

        if (blocker != null)
            score = BattleScore(attacker, blocker, out survives);
        else if (target != null)
            score = BattleScore(attacker, target, out survives);
        else
        {
            int security = player.SecurityStack.Count;
            int checks = attacker.GetSecurityAttackCount();
            if (checks <= 0)
                return -1;                                   // 시큐리티 체크를 못 하는 어택은 의미 없음
            if (security == 0)
                return 1000;                                 // 다이렉트 어택 = 승리
            int taken = Math.Min(checks, security);
            double gain = Enumerable.Range(0, taken).Sum(i => SecurityCardValue(security - i));
            double death = SecurityDeathChance(attacker.GetCurrentDP(), taken);
            score = gain - death * Value(attacker);
            survives = death < 0.5;
        }

        // 어택한 디지몬은 레스트 상태로 남는다 → 다음 플레이어 턴에 더 센 디지몬에게 공격당할 위험
        if (survives)
        {
            int threat = player.BattleArea.Select(d => d.GetCurrentDP()).DefaultIfEmpty(0).Max();
            if (threat > attacker.GetCurrentDP())
                score -= 0.35 * Value(attacker);
        }
        return score;
    }

    // =====================================================================
    // 효과 문장 점수 (옵션 · 기동 효과) — 한글(내장 카드) · 영어(API 카드)
    // =====================================================================

    /// <summary>효과 문장에서 [메인] 부분만 ([시큐리티] 효과 앞까지)</summary>
    public static string MainPart(string effectText)
    {
        var m = Regex.Match(effectText, @"\[(?:시큐리티|Security)\]|【시큐리티】|Security Effect", RegexOptions.IgnoreCase);
        return m.Success ? effectText[..m.Index] : effectText;
    }

    private static bool IsKorean(string s) => Regex.IsMatch(s, @"[가-힣]");

    /// <summary>효과 문장의 이득 점수 (영어는 AiEvaluator와 같고, 한글은 공식 한글판 표현으로 어림)</summary>
    public static double TextValue(string text)
    {
        if (!IsKorean(text))
            return AiEvaluator.EstimateSentenceValue(text);
        double v = 0;
        bool opponent = text.Contains("상대");
        int N(string pattern) => Regex.Match(text, pattern) is { Success: true } m && int.TryParse(m.Groups[1].Value, out int n) ? n : 1;
        if (text.Contains("소멸시킨다")) v += opponent ? 6 : -4;
        if (Regex.IsMatch(text, @"패로 (?:되|돌)")) v += opponent ? 4.5 : 2;
        if (text.Contains("레스트시킨다") && opponent) v += 2;
        if (text.Contains("어택과 블록을 할 수 없다") || text.Contains("어택할 수 없다")) v += opponent ? 2 : -2;
        if (Regex.IsMatch(text, @"DP를 -(\d+)")) v += (opponent ? 1 : -1) * N(@"DP를 -(\d+)") / 2000.0;
        if (Regex.IsMatch(text, @"DP를 \+(\d+)")) v += N(@"DP를 \+(\d+)") / 3000.0;
        if (Regex.IsMatch(text, @"《(\d+) 드로우》")) v += 1.6 * N(@"《(\d+) 드로우》");
        if (Regex.IsMatch(text, @"메모리를 \+(\d+)")) v += 1.3 * N(@"메모리를 \+(\d+)");
        if (Regex.IsMatch(text, @"메모리를 -(\d+)")) v -= 1.3 * N(@"메모리를 -(\d+)");
        if (Regex.IsMatch(text, @"《퇴화 (\d+)》")) v += 2.5 * N(@"《퇴화 (\d+)》");
        if (text.Contains("진화원") && text.Contains("파기") && opponent) v += 2;
        if (text.Contains("코스트를 지불하지 않고") && text.Contains("등장")) v += 4;
        if (text.Contains("시큐리티") && text.Contains("DP를 +")) v -= 0.5;   // 방어용 버프는 내 턴에 쓰면 이득이 작다
        return v;
    }

    /// <summary>
    /// 효과가 지금 쓸모 있는지 (대상이 있는지): 상대 디지몬을 노리는 효과는 조건(DP N 이하)에 맞는 상대 디지몬이,
    /// 자신의 디지몬을 강화하는 효과는 내 디지몬이 있어야 한다.
    /// </summary>
    public static bool HasUsefulTarget(GameState state, PlayerZones owner, string text)
    {
        var enemy = ReferenceEquals(owner, state.Player) ? state.DummyOpponent : state.Player;
        bool targetsEnemy = Regex.IsMatch(text, @"상대의 디지몬|opponent's Digimon", RegexOptions.IgnoreCase);
        if (targetsEnemy)
        {
            int? cap = Regex.Match(text, @"DP (\d+) 이하|(\d+) DP or less", RegexOptions.IgnoreCase) is { Success: true } m
                ? int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                : null;
            return enemy.BattleArea.Any(d => cap == null || d.GetCurrentDP() <= cap);
        }
        bool targetsOwn = Regex.IsMatch(text, @"자신의 디지몬|your Digimon|1 of your", RegexOptions.IgnoreCase);
        return !targetsOwn || owner.BattleArea.Count > 0;
    }
}
