using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Engine.Ai;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine;

/// <summary>
/// 턴 종료 시 어택 (효과 엔진 7단계): ≪볼텍스≫ · ≪에그제큐트≫.
/// - [자신의 턴 종료 시] 효과(액티브로 되돌리는 효과 등)를 처리한 뒤, 액티브 상태인 ≪볼텍스≫ · ≪에그제큐트≫ 디지몬이 1번씩 어택할 수 있다
/// - ≪볼텍스≫: 상대의 디지몬(레스트 — 보통 어택 대상)에게만, 등장한 턴에도
/// - ≪에그제큐트≫: 시큐리티 · 상대의 디지몬(액티브도). 그 어택이 끝나면 이 디지몬을 소멸 (어택 종료 시 — EndAttack)
/// 플레이어는 어택할지 · 대상을 고르고(추천 = 배틀에서 이기는 대상이 있을 때), AI는 정책(IAiPolicy.ChooseEndOfTurnAttack)이 고른다.
/// </summary>
public partial class GoldfishEngine
{
    // 이번 턴 종료 단계에서 이미 턴 종료 시 어택을 한 디지몬
    private readonly HashSet<CardInstance> _endOfTurnAttacked = new(ReferenceEqualityComparer.Instance);

    // ≪에그제큐트≫로 어택 중인 디지몬 — 어택 종료 시 소멸
    private CardInstance? _executingAttacker;

    /// <summary>이 디지몬이 지금 ≪에그제큐트≫로 어택 중인지 ("If attacking by ＜Execute＞")</summary>
    internal bool IsExecuteAttack(CardInstance? d) => d != null && ReferenceEquals(_executingAttacker, d);

    private bool CanEndOfTurnAttack(PlayerZones owner, CardInstance d) =>
        owner.BattleArea.Contains(d) && !d.IsSuspended && !_endOfTurnAttacked.Contains(d) && !EffectRules.IsAttackLocked(State, d)
        && (CardScripts.HasKeyword(d, Keyword.Vortex) || CardScripts.HasKeyword(d, Keyword.Execute));

    /// <summary>턴 종료 시 어택의 대상 후보: ≪볼텍스≫ = 어택할 수 있는(레스트) 상대 디지몬, ≪에그제큐트≫ = 액티브도</summary>
    private List<CardInstance> EndOfTurnTargets(PlayerZones owner, bool execute) =>
        EnemyOf(owner).BattleArea.Where(t => execute ? !CardScripts.HasKeyword(t, Keyword.CannotBeAttacked) : EffectRules.CanBeAttackTarget(t)).ToList();

    /// <summary>배틀에서 이기는(상대만 소멸하는) 대상 중 가장 좋은 것 (없으면 null)</summary>
    private static CardInstance? BestWinningTarget(CardInstance attacker, IEnumerable<CardInstance> targets) =>
        targets.Where(t => attacker.GetCurrentDP() > t.GetCurrentDP())
            .OrderByDescending(t => t.TopCard.Level).ThenByDescending(t => t.GetCurrentDP()).FirstOrDefault();

    /// <summary>플레이어 턴 종료: ≪볼텍스≫ · ≪에그제큐트≫ 디지몬이 차례로 어택할지 고른다</summary>
    private void RunPlayerEndOfTurnAttacks()
    {
        _endOfTurnAttacked.Clear();
        var owner = State.Player;
        foreach (var attacker in owner.BattleArea.ToList())
        {
            if (State.IsGameOver) return;
            if (!CanEndOfTurnAttack(owner, attacker)) continue;
            bool execute = CardScripts.HasKeyword(attacker, Keyword.Execute) && !CardScripts.HasKeyword(attacker, Keyword.Vortex)
                           || (CardScripts.HasKeyword(attacker, Keyword.Execute) && EndOfTurnTargets(owner, execute: false).Count == 0);
            var targets = EndOfTurnTargets(owner, execute);
            if (!execute && targets.Count == 0) continue;   // ≪볼텍스≫는 상대 디지몬에게만
            var best = BestWinningTarget(attacker, targets);
            string kw = execute ? "≪에그제큐트≫" : "≪볼텍스≫";
            if (!AskYesNo(owner, attacker, $"{kw} [{attacker.TopCard.Name}]으로 턴 종료 시 어택할까요?{(execute ? " (어택이 끝나면 이 디지몬은 소멸합니다)" : "")}", recommended: best != null))
                continue;
            CardInstance? defender = null;
            if (targets.Count > 0)
            {
                var ranked = AiEvaluator.Rank(targets, best);
                defender = ChooseTargets(owner, ranked, 1, $"{kw} [{attacker.TopCard.Name}]의 어택 대상을 고르세요.{(execute ? " (고르지 않으면 시큐리티)" : "")}",
                    optional: execute, preselected: best).FirstOrDefault();
            }
            if (!execute && defender == null) continue;
            _endOfTurnAttacked.Add(attacker);
            if (execute) _executingAttacker = attacker;
            Log($"[{kw}] [{attacker.TopCard.Name}] 턴 종료 시 어택!");
            if (defender != null) PlayerAttackOnDigimonCore(attacker, defender);
            else PlayerSecurityAttackCore(attacker);
        }
    }

    /// <summary>AI 턴 종료: 다음 턴 종료 시 어택 (정책이 고른 것 — Target null = 시큐리티). 없으면 null</summary>
    private (CardInstance Attacker, CardInstance? Target, bool Execute)? NextAiEndOfTurnAttack()
    {
        var owner = State.DummyOpponent;
        foreach (var attacker in owner.BattleArea.ToList())
        {
            if (!CanEndOfTurnAttack(owner, attacker)) continue;
            // 정책(IAiPolicy.ChooseEndOfTurnAttack)에 묻는다 — ≪볼텍스≫로 먼저, 고르지 않으면 ≪에그제큐트≫로
            EndOfTurnAttack? choice = null;
            bool execute = false;
            if (CardScripts.HasKeyword(attacker, Keyword.Vortex) && EndOfTurnTargets(owner, execute: false) is { Count: > 0 } vortexTargets)
                choice = AiPolicy.ChooseEndOfTurnAttack(this, attacker, vortexTargets, canAttackSecurity: false);
            if (choice is { Target: null }) choice = null;   // ≪볼텍스≫는 상대 디지몬에게만
            if (choice == null && CardScripts.HasKeyword(attacker, Keyword.Execute))
            {
                choice = AiPolicy.ChooseEndOfTurnAttack(this, attacker, EndOfTurnTargets(owner, execute: true), canAttackSecurity: true);
                execute = choice != null;
            }
            if (choice is not { } c) continue;
            var targets = EndOfTurnTargets(owner, execute);
            if (c.Target != null && !targets.Contains(c.Target)) continue;   // 후보에 없는 대상은 무시
            _endOfTurnAttacked.Add(attacker);
            if (execute) _executingAttacker = attacker;
            return (attacker, c.Target, execute);
        }
        return null;
    }

    /// <summary>어택 종료 시: ≪에그제큐트≫로 어택한 디지몬을 소멸</summary>
    private void FinishExecuteAttack(CardInstance attacker)
    {
        if (!ReferenceEquals(_executingAttacker, attacker)) return;
        _executingAttacker = null;
        var owner = OwnerOf(attacker);
        if (!owner.BattleArea.Contains(attacker) || State.IsGameOver) return;
        EffectLog(owner, $"[≪에그제큐트≫] 어택이 끝나 [{attacker.TopCard.Name}]을(를) 소멸시킵니다.");
        DeleteFrom(owner, attacker, DeletionCause.Effect);
    }
}
