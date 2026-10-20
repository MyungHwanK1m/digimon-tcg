using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine;

/// <summary>
/// 블록 가능 여부처럼 엔진과 ActionValidator(공격 미리보기)가 함께 쓰는 판정
/// </summary>
public static class EffectRules
{
    /// <summary>"~ 턴 종료 시까지" 기간 효과가 아직 유효한지 (0 = 효과 없음)</summary>
    public static bool IsActive(int until, GameState state) => until > 0 && until >= state.TurnSequence;

    public static bool CanBlock(GameState state, CardInstance blocker) =>
        blocker.HasBlocker && !blocker.IsSuspended && !IsActive(blocker.CannotBlockUntil, state);

    /// <summary>attacker가 blocker에게 블록당하지 않는지</summary>
    public static bool IsUnblockableBy(GameState state, CardInstance attacker, CardInstance blocker) =>
        IsActive(attacker.UnblockableUntil, state)
        || (CardScripts.HasKeyword(attacker, Keyword.UnblockableBySourceless) && blocker.EvolutionSources.Count == 0);

    /// <summary>attacker의 어택을 막을 블로커 (가장 DP가 높은 블로커가 막는다)</summary>
    public static CardInstance? FindBlocker(GameState state, IEnumerable<CardInstance> defenders, CardInstance attacker) =>
        BlockCandidates(state, defenders, attacker).FirstOrDefault();

    /// <summary>attacker의 어택을 블록할 수 있는 블로커 전부 (DP 높은 순) — 이 중 어느 것으로 막을지(막지 않을지)는 방어 측이 고른다</summary>
    public static List<CardInstance> BlockCandidates(GameState state, IEnumerable<CardInstance> defenders, CardInstance attacker) =>
        defenders
            .Where(d => CanBlock(state, d) && !IsUnblockableBy(state, attacker, d))
            .OrderByDescending(d => d.GetCurrentDP())
            .ToList();

    /// <summary>
    /// 상대(AI · 연습 더미)가 내 어택을 블록할 블로커. null = 블록하지 않음.
    /// 연습 모드의 더미 수비대는 블로커 돌파 훈련용이므로 항상 막는다. 실전 AI는 공식 룰대로 막을지 고른다:
    /// 이기는 블로커가 있으면 (그중 DP가 가장 낮은 것으로) 막고, 다이렉트 어택은 희생해서라도 막고,
    /// 시큐리티가 1장 남았을 때는 상대 쪽이 더 높은 레벨일 때만 동귀어진으로 교환한다. 그 밖에는 블로커를 아낀다.
    /// </summary>
    public static CardInstance? ChooseOpponentBlocker(GameState state, CardInstance attacker, CardInstance? attackTarget)
    {
        var candidates = BlockCandidates(state, state.DummyOpponent.BattleArea.Where(d => !ReferenceEquals(d, attackTarget)), attacker);
        if (candidates.Count == 0)
            return null;
        if (state.CurrentStage != PracticeStage.Stage3_AiBattle)
            return candidates[0];

        int atkDp = attacker.GetCurrentDP();
        var winner = candidates.Where(b => b.GetCurrentDP() > atkDp).OrderBy(b => b.GetCurrentDP()).FirstOrDefault();
        if (winner != null)
            return winner;

        if (attackTarget == null)
        {
            int security = state.DummyOpponent.SecurityStack.Count;
            if (security == 0)
                return candidates.OrderBy(b => b.GetCurrentDP()).First();

            if (security <= 1)
            {
                var trade = candidates.FirstOrDefault(b => b.GetCurrentDP() == atkDp && b.TopCard.Level < attacker.TopCard.Level);
                if (trade != null)
                    return trade;
            }
        }
        return null;
    }

    /// <summary>
    /// 시큐리티 체크로 오픈된 카드에 [시큐리티] 효과가 있는지 (공식 룰 13-1-8-2: 시큐리티 체크로 발동하는 효과가 있을 때만 처리하고,
    /// 없으면 13-1-8-4에 따라 그냥 트래시). 테이머의 [시큐리티] 효과는 "코스트 없이 등장".
    /// </summary>
    public static bool HasSecurityEffect(CardData card) =>
        card.EffectText.Contains("[시큐리티]") || card.EffectText.Contains("[Security]", StringComparison.OrdinalIgnoreCase);

    public static bool IsAttackLocked(GameState state, CardInstance digimon) =>
        IsActive(digimon.CannotAttackUntil, state);
}

/// <summary>
/// 카드 효과 처리 — 효과 내용은 카드 스크립트(Engine/Effects)에 있고, 여기서는 시점마다 해당 효과를 찾아 처리한다.
/// 스크립트가 쓰는 공용 동작(드로우 · 소멸 · 리커버리 · 메모리 …)도 여기에 있다.
/// </summary>
public partial class GoldfishEngine
{
    // =====================================================================
    // 효과 발동
    // =====================================================================

    /// <summary>
    /// trigger 시점의 효과를 처리한다: mainCard의 메인 효과 → self의 진화원 효과(아래에서 위 순서, 같은 카드는 1번).
    /// 진화원 효과는 처리 직전에 그 진화원이 아직 남아 있을 때만 발휘한다 (앞의 효과로 진화원이 빠질 수 있음).
    /// sources를 넘기면 그 목록을 진화원으로 쓴다 ([소멸 시]: 이미 트래시로 간 진화원).
    /// </summary>
    internal EffectContext RunEffects(
        EffectTrigger trigger,
        PlayerZones owner,
        CardInstance? self,
        CardData? mainCard,
        CardInstance? other = null,
        CardInstance? chosenTarget = null,
        bool includeInherited = true,
        IReadOnlyList<CardData>? sources = null)
    {
        var ctx = new EffectContext(this, owner, self, other, chosenTarget);

        if (mainCard != null && CardScripts.For(mainCard) is { } main)
        {
            foreach (var effect in main.Effects.Where(e => e.Trigger == trigger && e.Source == EffectSource.Main).ToList())
                Resolve(ctx, mainCard, effect);
        }

        if (includeInherited && self != null)
        {
            var sourceCards = (sources ?? self.GetEvolutionStackBottomUp()).GroupBy(s => s.CardId).Select(g => g.First()).ToList();
            foreach (var source in sourceCards)
            {
                if (CardScripts.For(source) is not { } script)
                    continue;
                foreach (var effect in script.Effects.Where(e => e.Trigger == trigger && e.Source == EffectSource.Inherited).ToList())
                {
                    if (sources == null && !self.HasSource(source.CardId))
                        break;
                    Resolve(ctx, source, effect);
                }
            }
        }

        return ctx;
    }

    private static void Resolve(EffectContext ctx, CardData card, CardEffect effect)
    {
        ctx.Card = card;
        if (effect.Condition != null && !effect.Condition(ctx))
            return;
        if (effect.OncePerTurn && ctx.Self != null && !ctx.Self.UsedOncePerTurn.Add(card.CardId))
            return;
        effect.Resolve(ctx);
    }

    // =====================================================================
    // 키워드
    // =====================================================================

    private static bool HasRetaliation(CardInstance d) => CardScripts.HasKeyword(d, Keyword.Retaliation);

    private static bool HasJamming(CardInstance d) => CardScripts.HasKeyword(d, Keyword.Jamming);

    private static bool HasPiercing(CardInstance d) => CardScripts.HasKeyword(d, Keyword.Piercing);

    private bool HasReboot(CardInstance d) =>
        CardScripts.HasKeyword(d, Keyword.Reboot) || EffectRules.IsActive(d.GrantedRebootUntil, State);

    // =====================================================================
    // 지속 효과 (테이머 · 진화원 조건부 DP / 시큐리티 어택 · 얻은 블로커)
    // =====================================================================

    /// <summary>
    /// 필드의 지속 효과 수치를 다시 계산한다 (양쪽 모두). [자신의 턴] 효과는 주인의 턴에만 적용된다.
    /// </summary>
    public void RecalculateFieldBuffs()
    {
        ApplyContinuousEffects(State.Player, State.DummyOpponent, State.PlayerAllSecurityAttackUntil);
        ApplyContinuousEffects(State.DummyOpponent, State.Player, State.OpponentAllSecurityAttackUntil);
    }

    private void ApplyContinuousEffects(PlayerZones owner, PlayerZones enemy, int allSecurityAttackUntil)
    {
        bool shadowWing = EffectRules.IsActive(allSecurityAttackUntil, State);

        foreach (var inst in owner.BattleArea)
        {
            bool ownTurn = inst.IsOwnersTurn;
            inst.HasGrantedBlocker = EffectRules.IsActive(inst.GrantedBlockerUntil, State) || CardScripts.HasKeyword(inst, Keyword.Blocker);

            var ctx = new StaticContext { State = State, Owner = owner, Enemy = enemy, Digimon = inst, OwnersTurn = ownTurn };
            int dp = 0, sec = 0;
            foreach (var effect in StaticEffectsOf(inst))
            {
                if (effect.Stat == StaticStat.Dp) dp += effect.Value(ctx);
                else if (effect.Stat == StaticStat.SecurityAttack) sec += effect.Value(ctx);
            }
            if (ownTurn && shadowWing) sec += 1;   // 섀도 윙 [시큐리티]

            inst.EffectDpBonus = dp;
            inst.EffectSecurityAttackBonus = sec;
            inst.TamerDpBonus = TamerStatics(owner, StaticStat.AllyDigimonDp).Sum(e => e.Value(ctx));
            inst.TamerSecurityAttackBonus = 0;
        }

        if (owner.BreedingArea != null)
        {
            owner.BreedingArea.EffectDpBonus = 0;
            owner.BreedingArea.EffectSecurityAttackBonus = 0;
            owner.BreedingArea.TamerDpBonus = 0;
            owner.BreedingArea.TamerSecurityAttackBonus = 0;
            owner.BreedingArea.HasGrantedBlocker = false;
        }
    }

    /// <summary>디지몬 자신에게 적용되는 지속 효과: 맨 위 카드의 메인 효과 + 진화원 효과(같은 카드는 1번)</summary>
    private static IEnumerable<StaticEffect> StaticEffectsOf(CardInstance inst)
    {
        if (CardScripts.For(inst.TopCard) is { } top)
        {
            foreach (var e in top.Statics.Where(e => e.Source == EffectSource.Main))
                yield return e;
        }
        foreach (var source in inst.EvolutionSources.GroupBy(s => s.CardId).Select(g => g.First()))
        {
            if (CardScripts.For(source) is not { } script) continue;
            foreach (var e in script.Statics.Where(e => e.Source == EffectSource.Inherited))
                yield return e;
        }
    }

    /// <summary>배틀하고 있는 동안만 더하는 DP (opponentHasSources = 배틀 상대가 진화원을 가졌는지, 시큐리티 디지몬이면 false)</summary>
    private int BattleDpBonus(CardInstance digimon, bool opponentHasSources)
    {
        var owner = OwnerOf(digimon);
        var ctx = new StaticContext
        {
            State = State, Owner = owner, Enemy = EnemyOf(owner), Digimon = digimon,
            OwnersTurn = digimon.IsOwnersTurn, BattleOpponentHasSources = opponentHasSources,
        };
        return StaticEffectsOf(digimon).Where(e => e.Stat == StaticStat.DpInBattle).Sum(e => e.Value(ctx));
    }

    /// <summary>필드의 테이머 효과 (같은 테이머가 여러 장이면 장마다)</summary>
    private static IEnumerable<StaticEffect> TamerStatics(PlayerZones owner, StaticStat stat) =>
        owner.Tamers.SelectMany(t => CardScripts.For(t)?.Statics.Where(e => e.Stat == stat && e.Source == EffectSource.Main) ?? Enumerable.Empty<StaticEffect>());

    private bool _playerTurnActive = true;

    /// <summary>누구의 턴인지 표시한다 ([자신의 턴] 진화원 DP는 주인의 턴에만 적용)</summary>
    private void SetTurnOwner(bool playerTurn)
    {
        _playerTurnActive = playerTurn;
        foreach (var d in AllInstances(State.Player)) d.IsOwnersTurn = playerTurn;
        foreach (var d in AllInstances(State.DummyOpponent)) d.IsOwnersTurn = !playerTurn;
        RecalculateFieldBuffs();
    }

    /// <summary>"이 턴 동안" 효과와 [턴에 1회] 사용 기록을 지운다 (각 턴 시작 시)</summary>
    private static void ResetTurnScopedState(CardInstance d)
    {
        d.TemporaryDpBonus = 0;
        d.TemporarySecurityAttackBonus = 0;
        d.HasUsedOncePerTurnEffect = false;
        d.UsedOncePerTurn.Clear();
        d.ActiveSkillBuffLabels.Clear();
    }

    private void PruneExpiredBuffs()
    {
        State.PlayerSecurityDpBuffs.RemoveAll(b => b.Until < State.TurnSequence);
        State.OpponentSecurityDpBuffs.RemoveAll(b => b.Until < State.TurnSequence);
    }

    private static IEnumerable<CardInstance> AllInstances(PlayerZones zones) =>
        zones.BreedingArea != null ? zones.BattleArea.Append(zones.BreedingArea) : zones.BattleArea;

    /// <summary>시큐리티 디지몬 DP 보정 (스타라이트 익스플로전 · 천국의 문 등 기간 효과 + 리키 같은 테이머 지속 효과)</summary>
    private int SecurityDigimonDpBonus(PlayerZones owner, bool ownersTurn)
    {
        int bonus = SecurityDpBuffsOf(owner).Where(b => b.Until >= State.TurnSequence).Sum(b => b.Dp);
        var ctx = new StaticContext { State = State, Owner = owner, Enemy = EnemyOf(owner), OwnersTurn = ownersTurn };
        return bonus + TamerStatics(owner, StaticStat.SecurityDigimonDp).Sum(e => e.Value(ctx));
    }

    internal List<(int Dp, int Until)> SecurityDpBuffsOf(PlayerZones owner) =>
        ReferenceEquals(owner, State.Player) ? State.PlayerSecurityDpBuffs : State.OpponentSecurityDpBuffs;

    /// <summary>자신의 디지몬 전부 ≪시큐리티 어택 +1≫ (until 턴 종료 시까지)</summary>
    internal void SetAllSecurityAttackUntil(PlayerZones owner, int until)
    {
        if (ReferenceEquals(owner, State.Player)) State.PlayerAllSecurityAttackUntil = until;
        else State.OpponentAllSecurityAttackUntil = until;
    }

    // =====================================================================
    // 메모리 — 효과 주인 기준 (AI 턴에는 AI 메모리 = AiTurnContext.Memory)
    // =====================================================================

    /// <summary>side 기준 메모리 (플레이어면 +가 플레이어 쪽, AI면 +가 AI 쪽)</summary>
    internal int MemoryOf(PlayerZones side)
    {
        bool player = ReferenceEquals(side, State.Player);
        if (_aiTurn != null)
            return player ? -_aiTurn.Memory : _aiTurn.Memory;
        return player ? State.MemoryGauge : -State.MemoryGauge;
    }

    /// <summary>side의 메모리 +amount (상대 쪽에서 보면 -amount)</summary>
    internal void AddMemory(PlayerZones side, int amount)
    {
        bool player = ReferenceEquals(side, State.Player);
        if (_aiTurn != null)
            _aiTurn.Memory += player ? -amount : amount;
        else
            State.MemoryGauge += player ? amount : -amount;
    }

    /// <summary>[이 턴 종료 시] 메모리 -amount — 턴 플레이어의 효과만 의미가 있다</summary>
    internal void AddEndOfTurnMemoryLoss(PlayerZones side, int amount)
    {
        bool player = ReferenceEquals(side, State.Player);
        if (_aiTurn != null)
        {
            if (!player) _aiTurn.EndOfTurnMemoryLoss += amount;
        }
        else if (player)
        {
            State.PendingEndOfTurnMemoryLoss += amount;
        }
    }

    // =====================================================================
    // 공통 동작
    // =====================================================================

    internal PlayerZones OwnerOf(CardInstance inst) =>
        State.DummyOpponent.BattleArea.Contains(inst) || ReferenceEquals(State.DummyOpponent.BreedingArea, inst)
            ? State.DummyOpponent
            : State.Player;

    internal PlayerZones EnemyOf(PlayerZones zones) =>
        ReferenceEquals(zones, State.Player) ? State.DummyOpponent : State.Player;

    internal string SideName(PlayerZones zones) => ReferenceEquals(zones, State.Player) ? "내" : "상대";

    /// <summary>소멸 원인 — ≪방벽≫은 배틀, ≪회피≫는 배틀 · 효과에서만 소멸을 막는다</summary>
    internal enum DeletionCause { Battle, Effect, ZeroDp, Cost }

    /// <summary>
    /// 디지몬을 소멸시킨다 (진화원 포함 트래시) + [소멸 시] 효과. ≪방벽≫ · ≪회피≫로 막으면 false.
    /// DP 0이 되어 소멸하는 것(룰 처리)과 코스트로 소멸시키는 것은 막을 수 없다.
    /// </summary>
    internal bool DeleteFrom(PlayerZones zones, CardInstance instance, DeletionCause cause = DeletionCause.Effect)
    {
        if (!zones.BattleArea.Contains(instance))
            return false;
        if (TryPreventDeletion(zones, instance, cause))
            return false;
        RemoveDeleted(zones, instance);
        return true;
    }

    /// <summary>≪방벽≫ · ≪회피≫ — 소멸을 막았으면 true</summary>
    private bool TryPreventDeletion(PlayerZones zones, CardInstance inst, DeletionCause cause)
    {
        if (cause is DeletionCause.ZeroDp or DeletionCause.Cost)
            return false;
        string name = inst.TopCard.Name;

        if (cause == DeletionCause.Battle && CardScripts.HasKeyword(inst, Keyword.Barrier) && zones.SecurityStack.Count > 0
            && AskYesNo(zones, inst, $"≪방벽≫ [{name}]이(가) 배틀에서 소멸합니다. 시큐리티를 위에서 1장 파기하고 소멸을 막을까요?", recommended: zones.SecurityStack.Count >= 2))
        {
            zones.Trash.Add(zones.SecurityStack[0]);
            zones.SecurityStack.RemoveAt(0);
            EffectLog(zones, $"[≪방벽≫] [{name}]: 시큐리티를 위에서 1장 파기하고 소멸하지 않습니다. (남은 시큐리티 {zones.SecurityStack.Count}장)");
            return true;
        }

        if (CardScripts.HasKeyword(inst, Keyword.Evade) && !inst.IsSuspended
            && AskYesNo(zones, inst, $"≪회피≫ [{name}]이(가) 소멸합니다. 레스트시키고 소멸을 막을까요?", recommended: true))
        {
            inst.IsSuspended = true;
            EffectLog(zones, $"[≪회피≫] [{name}]: 레스트 상태가 되어 소멸하지 않습니다.");
            return true;
        }
        return false;
    }

    /// <summary>소멸 처리 (막을 수 있는지는 이미 정해졌을 때)</summary>
    private void RemoveDeleted(PlayerZones zones, CardInstance instance)
    {
        if (!zones.BattleArea.Remove(instance))
            return;

        var stack = instance.GetEvolutionStackBottomUp();
        zones.Trash.Add(instance.TopCard);
        while (instance.EvolutionSources.Count > 0)
            zones.Trash.Add(instance.EvolutionSources.Pop());

        RunEffects(EffectTrigger.OnDeletion, zones, instance, instance.TopCard, sources: stack);
    }

    // =====================================================================
    // 플레이어에게 묻는 선택 (AI · 테스트는 추천 답)
    // =====================================================================

    /// <summary>플레이어 쪽 선택을 물을 곳 (UI가 등록). null이면 추천 답을 쓴다</summary>
    public IChoiceProvider? PlayerChoices { get; set; }

    internal bool AskYesNo(PlayerZones owner, CardInstance? subject, string question, bool recommended) =>
        ReferenceEquals(owner, State.Player) && PlayerChoices != null
            ? PlayerChoices.Confirm(State, subject, question, recommended)
            : recommended;

    internal CardInstance? AskDigimon(PlayerZones owner, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional)
    {
        if (!ReferenceEquals(owner, State.Player) || PlayerChoices == null)
            return recommended;
        var picked = PlayerChoices.ChooseDigimon(State, question, options, recommended, optional);
        return picked != null && options.Contains(picked) ? picked : optional ? null : recommended;
    }

    // =====================================================================
    // 어택 키워드 — ≪연계≫ · ≪돌진≫ · ≪속공≫ · ≪진격≫
    // =====================================================================

    /// <summary>이 턴에 어택할 수 있는 상태인지 (액티브 · 등장한 턴이 아니거나 ≪속공≫ · 어택 불가 효과 없음)</summary>
    internal bool CanAttackThisTurn(CardInstance d) =>
        !d.IsSuspended
        && (d.TurnPlayed < State.CurrentTurn || CardScripts.HasKeyword(d, Keyword.Rush))
        && !EffectRules.IsAttackLocked(State, d);

    /// <summary>
    /// "어택했을 때" 키워드 처리 ([어택 시] 효과 다음): ≪연계≫ → ≪돌진≫.
    /// 반환: ≪돌진≫으로 바꾼 어택 대상 (바꾸지 않았으면 null). target = 원래 어택 대상 (시큐리티 어택이면 null)
    /// </summary>
    private CardInstance? ApplyAttackKeywords(CardInstance attacker, CardInstance? target)
    {
        var owner = OwnerOf(attacker);
        var enemy = EnemyOf(owner);
        if (!owner.BattleArea.Contains(attacker))
            return null;

        if (CardScripts.HasKeyword(attacker, Keyword.Alliance))
        {
            var options = owner.BattleArea.Where(d => !ReferenceEquals(d, attacker) && !d.IsSuspended).ToList();
            if (options.Count > 0)
            {
                // 추천: 이 턴에 어택하지 못하고 ≪블로커≫도 아닌 디지몬 중 DP가 가장 높은 것
                var recommended = options.Where(d => !d.HasBlocker && !CanAttackThisTurn(d)).OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();
                var ally = AskDigimon(owner, $"≪연계≫ [{attacker.TopCard.Name}]: 레스트시켜 DP를 더할 다른 디지몬을 고르세요 (고르지 않아도 됩니다).", options, recommended, optional: true);
                if (ally != null)
                {
                    ally.IsSuspended = true;
                    int dp = ally.GetCurrentDP();
                    attacker.AttackDpBonus += dp;
                    attacker.AttackSecurityAttackBonus += 1;
                    EffectLog(owner, $"[≪연계≫] [{ally.TopCard.Name}]을(를) 레스트시켜 [{attacker.TopCard.Name}]이(가) 이 어택 동안 DP +{dp:N0} · ≪시큐리티 어택 +1≫");
                }
            }
        }

        if (CardScripts.HasKeyword(attacker, Keyword.Raid))
        {
            var best = enemy.BattleArea.Where(d => !d.IsSuspended).OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();
            if (best != null && !ReferenceEquals(best, target))
            {
                // 추천: 이기는 배틀일 때만 — 디지몬을 어택 중이었거나, 시큐리티 어택이면 상대 ≪블로커≫ / Lv.5 이상을 잡을 때 (시큐리티 0장이면 다이렉트 어택 우선)
                bool wins = attacker.GetCurrentDP() > best.GetCurrentDP();
                bool recommended = wins && (target != null || (enemy.SecurityStack.Count > 0 && (best.HasBlocker || best.TopCard.Level >= 5)));
                if (AskYesNo(owner, attacker, $"≪돌진≫ [{attacker.TopCard.Name}]: 어택 대상을 상대 [{best.TopCard.Name}(DP {best.GetCurrentDP():N0})]로 바꿀까요?", recommended))
                {
                    EffectLog(owner, $"[≪돌진≫] [{attacker.TopCard.Name}]의 어택 대상을 액티브 상태의 [{best.TopCard.Name}(DP {best.GetCurrentDP():N0})]로 바꿉니다!");
                    return best;
                }
            }
        }
        return null;
    }

    /// <summary>어택이 끝났을 때: "이 어택 동안" 효과(≪연계≫)를 지우고 [어택 종료 시] 효과</summary>
    private void EndAttack(CardInstance attacker)
    {
        attacker.AttackDpBonus = 0;
        attacker.AttackSecurityAttackBonus = 0;
        RecalculateFieldBuffs();
        var owner = OwnerOf(attacker);
        if (owner.BattleArea.Contains(attacker) && !State.IsGameOver)
        {
            RunEffects(EffectTrigger.EndOfAttack, owner, attacker, attacker.TopCard);
            RecalculateFieldBuffs();
        }
    }

    /// <summary>[자신의 턴 개시 시] 효과 — 자신의 디지몬(메인 · 진화원)과 테이머</summary>
    private void TriggerStartOfTurn(PlayerZones owner)
    {
        foreach (var d in owner.BattleArea.ToList())
            RunEffects(EffectTrigger.StartOfYourTurn, owner, d, d.TopCard);
        foreach (var tamer in owner.Tamers.ToList())
            RunEffects(EffectTrigger.StartOfYourTurn, owner, null, tamer, includeInherited: false);
        RecalculateFieldBuffs();
    }

    /// <summary>
    /// 시큐리티 체크로 오픈된 디지몬 카드의 [시큐리티] 효과 (공식 룰: 효과를 발휘한 뒤 배틀). 반환: 결과 메시지 (효과 없으면 빈 문자열)
    /// </summary>
    private string ResolveSecurityDigimonEffect(CardData securityCard, PlayerZones owner, CardInstance attacker)
    {
        if (!(CardScripts.For(securityCard)?.Effects.Any(e => e.Trigger == EffectTrigger.Security) ?? false))
            return string.Empty;
        var ctx = RunEffects(EffectTrigger.Security, owner, null, securityCard, other: attacker, includeInherited: false);
        RecalculateFieldBuffs();
        string msg = $"[{SideName(owner)} 시큐리티 디지몬 {securityCard.Name}의 [시큐리티] 효과] {ctx.Message}";
        Log(msg);
        return msg;
    }

    /// <summary>
    /// ≪진격≫: 상대 쪽 메모리가 1 이상이면 이 디지몬으로 어택할 수 있다.
    /// 플레이어는 턴이 끝나지 않고 이 디지몬의 어택 1번만 할 수 있는 상태(State.BlitzAttacker)가 되고, AI는 곧바로 어택한다.
    /// </summary>
    internal bool TryGrantBlitz(PlayerZones owner, CardInstance digimon)
    {
        if (MemoryOf(owner) > -1 || !owner.BattleArea.Contains(digimon) || !CanAttackThisTurn(digimon))
            return false;
        if (ReferenceEquals(owner, State.Player))
            State.BlitzAttacker = digimon;
        else if (_aiTurn != null)
            _aiTurn.BlitzAttacker = digimon;
        else
            return false;
        return true;
    }

    private bool DeleteDigimon(CardInstance instance) => DeleteFrom(State.Player, instance, DeletionCause.Battle);

    private bool DeleteOpponentDigimon(CardInstance instance) => DeleteFrom(State.DummyOpponent, instance, DeletionCause.Battle);

    internal void Mill(PlayerZones zones, int count, string reason)
    {
        int n = 0;
        while (n < count && zones.MainDeck.Count > 0)
        {
            zones.Trash.Add(zones.MainDeck[0]);
            zones.MainDeck.RemoveAt(0);
            n++;
        }
        if (n > 0)
            Log($"[효과] {reason}: {SideName(zones)} 덱 위에서 {n}장 파기");
    }

    internal CardData? DrawFor(PlayerZones zones)
    {
        if (zones.MainDeck.Count == 0)
            return null;
        var card = zones.MainDeck[0];
        zones.MainDeck.RemoveAt(0);
        zones.Hand.Add(card);
        return card;
    }

    /// <summary>≪리커버리 +N (덱)≫: 덱 위에서 N장을 시큐리티 위에 놓는다</summary>
    internal int Recovery(PlayerZones zones, int count)
    {
        int n = 0;
        while (n < count && zones.MainDeck.Count > 0)
        {
            zones.SecurityStack.Insert(0, zones.MainDeck[0]);
            zones.MainDeck.RemoveAt(0);
            n++;
        }
        return n;
    }

    /// <summary>덱 위에서 count장을 오픈해 조건에 맞는 카드를 최대 maxTake장 패에 넣고, 나머지는 덱 아래로 되돌린다</summary>
    internal List<CardData> RevealAndTake(PlayerZones zones, int count, Func<CardData, bool> match, int maxTake)
    {
        var revealed = zones.MainDeck.Take(count).ToList();
        zones.MainDeck.RemoveRange(0, revealed.Count);
        var taken = revealed.Where(match).OrderByDescending(c => c.Level).ThenByDescending(c => c.DP).Take(maxTake).ToList();
        foreach (var c in taken)
        {
            revealed.Remove(c);
            zones.Hand.Add(c);
        }
        zones.MainDeck.AddRange(revealed);
        return taken;
    }

    internal void ReturnToHand(PlayerZones zones, CardInstance inst)
    {
        if (!zones.BattleArea.Remove(inst))
            return;
        while (inst.EvolutionSources.Count > 0)
            zones.Trash.Add(inst.EvolutionSources.Pop());
        zones.Hand.Add(inst.TopCard);
    }

    /// <summary>≪퇴화 N≫ — 버린 매수 반환</summary>
    internal int DeDigivolve(PlayerZones zones, CardInstance inst, int count)
    {
        int n = 0;
        while (n < count)
        {
            var removed = inst.DeDigivolveOnce();
            if (removed == null) break;
            zones.Trash.Add(removed);
            n++;
        }
        return n;
    }

    /// <summary>DP 마이너스 — DP가 0 이하가 되면 소멸 (반환: 소멸 여부)</summary>
    internal bool ApplyDpMinus(CardInstance target, int amount, string source)
    {
        var zones = OwnerOf(target);
        target.TemporaryDpBonus -= amount;
        target.ActiveSkillBuffLabels.Add($"🔻{source} (DP -{amount:N0})");
        Log($"[효과] {source}: {SideName(zones)} [{target.TopCard.Name}] DP -{amount:N0}");

        if (target.TopCard.DP + target.GetTotalBonusDP() > 0)
            return false;

        DeleteFrom(zones, target, DeletionCause.ZeroDp);
        Log($"[효과] {SideName(zones)} [{target.TopCard.Name}]의 DP가 0이 되어 소멸했습니다.");

        // 상대 디지몬이 DP 0으로 소멸했을 때 (토코몬 · 파닥몬 진화원 등)
        var other = EnemyOf(zones);
        foreach (var d in other.BattleArea.ToList())
            RunEffects(EffectTrigger.OpponentDigimonDeletedByZeroDp, other, d, null);
        return true;
    }

    /// <summary>트래시에서 조건에 맞는 디지몬을 코스트 없이 등장시킨다 ([등장 시] 효과는 발휘하지 않음)</summary>
    internal List<CardData> PlayFromTrash(PlayerZones zones, Func<CardData, bool> match, int maxCount)
    {
        var picks = zones.Trash.Where(c => c.Type == CardType.Digimon && match(c))
            .OrderByDescending(c => c.Level).ThenByDescending(c => c.DP)
            .Take(maxCount).ToList();
        foreach (var c in picks)
        {
            zones.Trash.Remove(c);
            zones.BattleArea.Add(new CardInstance(c, State.CurrentTurn) { IsOwnersTurn = ReferenceEquals(zones, State.Player) == _playerTurnActive });
        }
        return picks;
    }

    /// <summary>디지몬 중 효과 대상 고르기: UI가 넘겨준 대상이 조건에 맞으면 그것, 아니면 DP가 가장 높은 것</summary>
    internal static CardInstance? PickTarget(IEnumerable<CardInstance> candidates, CardInstance? preferred, Func<CardInstance, bool>? match = null)
    {
        var list = candidates.Where(c => match == null || match(c)).ToList();
        if (preferred != null && list.Contains(preferred))
            return preferred;
        return list.OrderByDescending(c => c.GetCurrentDP()).ThenByDescending(c => c.TopCard.Level).FirstOrDefault();
    }

    /// <summary>상대 디지몬 1마리(진화원이 있고 조건에 맞는 것 중 레벨이 가장 높은 것)의 진화원을 아래에서 1장 파기</summary>
    internal void TrashBottomSourceOfEnemy(PlayerZones enemyZones, Func<CardInstance, bool> match, string source)
    {
        var target = enemyZones.BattleArea.Where(d => d.EvolutionSources.Count > 0 && match(d))
            .OrderByDescending(d => d.TopCard.Level).FirstOrDefault();
        if (target == null) return;
        var removed = target.RemoveBottomSources(1);
        enemyZones.Trash.AddRange(removed);
        Log($"[진화원 효과] {source}: 상대 [{target.TopCard.Name}]의 진화원 [{removed[0].Name}] 파기");
    }

    /// <summary>카드 효과 로그 — AI 쪽 효과는 🤖를 붙여 구분한다 (효과 문장의 "상대"는 효과 주인 기준)</summary>
    internal void EffectLog(PlayerZones owner, string message) =>
        Log(ReferenceEquals(owner, State.DummyOpponent) ? "🤖 " + message : message);

    /// <summary>상대 턴 리플레이에 장면을 남긴다 (상대 턴 중 내 효과가 발휘됐을 때)</summary>
    internal void AddReplayEvent(string title, string detail) =>
        AddOpponentEvent(OpponentActionKind.TurnEnd, title, detail);

    // =====================================================================
    // 배틀 (블록 · 디지몬 어택) — ≪길동무≫ 포함
    // =====================================================================

    /// <summary>
    /// 두 디지몬의 배틀: DP가 낮은 쪽이 소멸, 같으면 둘 다 소멸. 혼자 진 쪽이 ≪길동무≫면 상대도 소멸.
    /// 반환하는 DP는 배틀 중에만 더하는 DP(뿔몬 진화원 등)까지 포함한 값.
    /// </summary>
    private (bool AttackerDeleted, bool DefenderDeleted, int AttackerDp, int DefenderDp) ResolveDigimonBattle(CardInstance attacker, CardInstance defender)
    {
        var atkZones = OwnerOf(attacker);
        var defZones = OwnerOf(defender);
        RecalculateFieldBuffs();
        int atkDp = attacker.GetCurrentDP() + BattleDpBonus(attacker, defender.EvolutionSources.Count > 0);
        int defDp = defender.GetCurrentDP() + BattleDpBonus(defender, attacker.EvolutionSources.Count > 0);

        bool attackerLoses = atkDp <= defDp;
        bool defenderLoses = atkDp >= defDp;

        // 진 쪽이 ≪방벽≫ · ≪회피≫로 소멸을 막는지 먼저 정한다 (막으면 ≪길동무≫도 발동하지 않는다)
        bool attackerDeleted = attackerLoses && !TryPreventDeletion(atkZones, attacker, DeletionCause.Battle);
        bool defenderDeleted = defenderLoses && !TryPreventDeletion(defZones, defender, DeletionCause.Battle);

        if (attackerDeleted && !defenderLoses && HasRetaliation(attacker))
        {
            Log($"[≪길동무≫] 배틀에서 진 [{attacker.TopCard.Name}]이(가) [{defender.TopCard.Name}]을(를) 함께 소멸시킵니다!");
            defenderDeleted = !TryPreventDeletion(defZones, defender, DeletionCause.Effect);
        }
        else if (defenderDeleted && !attackerLoses && HasRetaliation(defender))
        {
            Log($"[≪길동무≫] 배틀에서 진 [{defender.TopCard.Name}]이(가) [{attacker.TopCard.Name}]을(를) 함께 소멸시킵니다!");
            attackerDeleted = !TryPreventDeletion(atkZones, attacker, DeletionCause.Effect);
        }

        if (defenderDeleted) RemoveDeleted(defZones, defender);
        if (attackerDeleted) RemoveDeleted(atkZones, attacker);

        if (defenderDeleted && !attackerDeleted)
            RunEffects(EffectTrigger.DeletedOpponentInBattle, atkZones, attacker, null);

        return (attackerDeleted, defenderDeleted, atkDp, defDp);
    }

    // =====================================================================
    // 시점별 효과 처리
    // =====================================================================

    /// <summary>[등장 시] 효과 + 같은 명칭 등장에 반응하는 진화원 효과. 반환: 결과 요약</summary>
    internal string ApplyOnPlayEffects(PlayerZones owner, CardInstance instance)
    {
        var card = instance.TopCard;
        string summary = RunEffects(EffectTrigger.OnPlay, owner, instance, card, includeInherited: false).Message;

        foreach (var other in owner.BattleArea.Where(d => !ReferenceEquals(d, instance)).ToList())
            RunEffects(EffectTrigger.AllySameNamePlayed, owner, other, null, other: instance);

        if (summary.Length > 0)
            EffectLog(owner, $"[효과] {card.Name} {summary}");
        RecalculateFieldBuffs();
        return summary;
    }

    /// <summary>[진화 시] 효과 (진화한 카드의 메인 효과)</summary>
    private void ApplyWhenDigivolvingEffects(PlayerZones owner, CardInstance target, CardData evoCard)
    {
        RunEffects(EffectTrigger.WhenDigivolving, owner, target, evoCard, includeInherited: false);
        RecalculateFieldBuffs();
    }

    /// <summary>[어택 시] 효과 (자신 + 진화원). defender = 어택 대상 디지몬 (시큐리티 어택이면 null)</summary>
    private void TriggerWhenAttackingEffects(CardInstance attacker, CardInstance? defender)
    {
        RunEffects(EffectTrigger.WhenAttacking, OwnerOf(attacker), attacker, attacker.TopCard, other: defender);
        RecalculateFieldBuffs();
    }

    /// <summary>내 디지몬의 어택이 블록당했을 때 (메탈그레이몬 ST1-09 진화원 등)</summary>
    private void OnAttackBlocked(CardInstance attacker) =>
        RunEffects(EffectTrigger.AttackBlocked, OwnerOf(attacker), attacker, null);

    /// <summary>옵션 [메인] 효과. target = UI가 고른 대상. 반환: 결과 메시지</summary>
    private string ApplyOptionEffect(PlayerZones owner, CardData optionCard, CardInstance? target)
    {
        string msg = RunEffects(EffectTrigger.Main, owner, null, optionCard, chosenTarget: target, includeInherited: false).Message;
        if (msg.Length > 0)
            Log("[효과] " + msg);
        RecalculateFieldBuffs();
        return msg;
    }

    /// <summary>[상대의 턴 종료 시] 이 턴에 상대가 어택하지 않았을 때 (owner = 턴을 마친 쪽의 상대)</summary>
    private void TriggerOpponentTurnEndWithoutAttack(PlayerZones owner)
    {
        foreach (var d in owner.BattleArea.ToList())
            RunEffects(EffectTrigger.OpponentTurnEndWithoutAttack, owner, d, null);
    }

    // =====================================================================
    // 옵션 [시큐리티] — 양쪽 공통 (owner = 시큐리티 주인, attacker = 어택 중인 상대 디지몬)
    // =====================================================================

    private record SecurityOptionOutcome(string Log, string Summary, bool AttackerRemoved, bool ReturnToHand);

    private SecurityOptionOutcome ResolveSecurityOption(CardData option, PlayerZones owner, CardInstance attacker)
    {
        string head = $"[{SideName(owner)} 시큐리티 {option.Name} 발동!]";
        bool hasEffect = CardScripts.For(option)?.Effects.Any(e => e.Trigger == EffectTrigger.Security) ?? false;
        if (!hasEffect)
            return new($"{head} [시큐리티] 효과가 없는 카드입니다.", $"[시큐리티 {option.Name}] 효과 없음", false, false);

        var ctx = RunEffects(EffectTrigger.Security, owner, null, option, other: attacker, includeInherited: false);
        return new($"{head} {ctx.Message}", $"[시큐리티 {option.Name}] {ctx.Summary}", ctx.AttackerRemoved, ctx.ReturnToHand);
    }

    /// <summary>
    /// [자신의 턴 개시 시] 테이머 효과로 메모리를 보정한다 (플레이어 · AI 공통, 카드 스크립트의 StartOfTurnMemory).
    /// </summary>
    private static (int Memory, string? Note) ApplyStartOfTurnTamerMemory(int handedOver, IReadOnlyCollection<CardData> tamers, PlayerZones enemy)
    {
        int memory = handedOver;
        var notes = new List<string>();

        foreach (var group in tamers.GroupBy(t => t.CardId))
        {
            var effect = CardScripts.For(group.First())?.StartOfTurnMemory;
            if (effect == null || !effect.Condition(enemy))
                continue;
            int count = group.Count();
            int gain = effect.Amount * count;
            string name = group.First().Name;
            memory += gain;
            notes.Add($"{name} 효과 +{gain} ({effect.Reason})" + (count > 1 ? $" ({name} {count}장)" : ""));
        }

        return (memory, notes.Count > 0 ? string.Join(", ", notes) : null);
    }
}
