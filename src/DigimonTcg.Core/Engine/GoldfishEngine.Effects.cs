using System;
using System.Collections.Generic;
using System.Linq;
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
        // 원뿔몬(BT1-034) 진화원: 진화원을 갖지 않은 상대의 디지몬에게는 블록당하지 않는다
        || (attacker.HasSource("BT1-034") && blocker.EvolutionSources.Count == 0);

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
/// 내장 카드(StarterDecks)의 실제 카드 효과 — 공식 한글판 카드 텍스트 기준.
/// 대상 선택이 필요한 효과는 UI가 넘겨준 대상을 쓰고, 없으면 가장 유리한 대상을 자동으로 고른다.
/// (데이터 기반 효과 엔진으로 옮기기 전 단계 — docs/card_effect_engine_plan.md)
/// </summary>
public partial class GoldfishEngine
{
    private static readonly HashSet<string> LoseTwoMemoryOnAttackIds = new() { "ST1-06", "ST2-07", "ST3-07", "ST4-08", "ST5-08", "ST6-08" };

    // =====================================================================
    // 키워드
    // =====================================================================

    private static bool HasRetaliation(CardInstance d) =>
        d.TopCard.CardId is "BT2-074" or "BT2-080" || d.HasSource("BT2-074");

    private static bool HasJamming(CardInstance d) => d.TopCard.CardId == "BT3-031";

    private static bool HasPiercing(CardInstance d) => d.TopCard.CardId == "ST4-13";

    private bool HasReboot(CardInstance d) =>
        d.TopCard.CardId == "BT2-065" || EffectRules.IsActive(d.GrantedRebootUntil, State);

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
        int taiCount = owner.Tamers.Count(t => t.CardId == "ST1-12");     // 신태일: [자신의 턴] 자신의 디지몬 전부 DP +1000
        bool enemyHasNoSourceDigimon = enemy.BattleArea.Any(d => d.EvolutionSources.Count == 0);
        bool shadowWing = EffectRules.IsActive(allSecurityAttackUntil, State);

        foreach (var inst in owner.BattleArea)
        {
            bool ownTurn = inst.IsOwnersTurn;
            inst.HasGrantedBlocker = EffectRules.IsActive(inst.GrantedBlockerUntil, State) || inst.HasSource("ST5-11");

            int dp = 0, sec = 0;
            if (ownTurn)
            {
                if (inst.HasSource("ST1-01") && inst.EvolutionSources.Count >= 4) dp += 1000;   // 코로몬
                if (inst.HasSource("BT2-009") && enemy.Trash.Count >= 5) dp += 1000;            // 길몬
                if (inst.HasSource("ST4-01") && inst.TopCard.Level >= 6) dp += 1000;            // 모티몬
                if (inst.HasSource("ST5-01") && inst.HasBlocker) dp += 1000;                    // 카프리몬
                if (inst.HasSource("ST6-11") && owner.Trash.Count >= 5) dp += 2000;             // 워가루몬(퍼플)
                if (inst.HasSource("BT1-060")) dp += owner.SecurityStack.Count / 3 * 1000;      // 홀리엔젤몬(BT1)
                if (inst.HasSource("ST2-08") && enemyHasNoSourceDigimon) sec += 1;              // 워가루몬
                if (inst.TopCard.CardId == "ST1-11") sec += inst.EvolutionSources.Count / 2;    // 워그레이몬: 진화원 2장마다
                if (shadowWing) sec += 1;                                                        // 섀도 윙 [시큐리티]
            }

            inst.EffectDpBonus = dp;
            inst.EffectSecurityAttackBonus = sec;
            inst.TamerDpBonus = ownTurn ? taiCount * 1000 : 0;
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

    /// <summary>시큐리티 디지몬 DP 보정 (리키 ST3-12 [상대의 턴] +2000 · 스타라이트 익스플로전 · 천국의 문)</summary>
    private int SecurityDigimonDpBonus(PlayerZones owner, bool ownersTurn)
    {
        var buffs = ReferenceEquals(owner, State.Player) ? State.PlayerSecurityDpBuffs : State.OpponentSecurityDpBuffs;
        int bonus = buffs.Where(b => b.Until >= State.TurnSequence).Sum(b => b.Dp);
        if (!ownersTurn)
            bonus += owner.Tamers.Count(t => t.CardId == "ST3-12") * 2000;
        return bonus;
    }

    // =====================================================================
    // 공통 동작
    // =====================================================================

    private PlayerZones OwnerOf(CardInstance inst) =>
        State.DummyOpponent.BattleArea.Contains(inst) || ReferenceEquals(State.DummyOpponent.BreedingArea, inst)
            ? State.DummyOpponent
            : State.Player;

    private PlayerZones EnemyOf(PlayerZones zones) =>
        ReferenceEquals(zones, State.Player) ? State.DummyOpponent : State.Player;

    private string SideName(PlayerZones zones) => ReferenceEquals(zones, State.Player) ? "내" : "상대";

    /// <summary>디지몬을 소멸시킨다 (진화원 포함 트래시) + [소멸 시] 효과</summary>
    private void DeleteFrom(PlayerZones zones, CardInstance instance)
    {
        if (!zones.BattleArea.Remove(instance))
            return;

        var stack = instance.GetEvolutionStackBottomUp();
        zones.Trash.Add(instance.TopCard);
        while (instance.EvolutionSources.Count > 0)
            zones.Trash.Add(instance.EvolutionSources.Pop());

        // [소멸 시] 임프몬(BT2-068): 자신의 덱 위에서 3장 파기 / 퍼그몬(ST6-01) 진화원: 2장 파기
        if (instance.TopCard.CardId == "BT2-068")
            Mill(zones, 3, "임프몬 [소멸 시]");
        if (stack.Any(s => s.CardId == "ST6-01"))
            Mill(zones, 2, "퍼그몬 진화원 [소멸 시]");
    }

    private void DeleteDigimon(CardInstance instance) => DeleteFrom(State.Player, instance);

    private void DeleteOpponentDigimon(CardInstance instance) => DeleteFrom(State.DummyOpponent, instance);

    private void Mill(PlayerZones zones, int count, string reason)
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

    private CardData? DrawFor(PlayerZones zones)
    {
        if (zones.MainDeck.Count == 0)
            return null;
        var card = zones.MainDeck[0];
        zones.MainDeck.RemoveAt(0);
        zones.Hand.Add(card);
        return card;
    }

    /// <summary>≪리커버리 +N (덱)≫: 덱 위에서 N장을 시큐리티 위에 놓는다</summary>
    private int Recovery(PlayerZones zones, int count)
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
    private List<CardData> RevealAndTake(PlayerZones zones, int count, Func<CardData, bool> match, int maxTake)
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

    private void ReturnToHand(PlayerZones zones, CardInstance inst)
    {
        if (!zones.BattleArea.Remove(inst))
            return;
        while (inst.EvolutionSources.Count > 0)
            zones.Trash.Add(inst.EvolutionSources.Pop());
        zones.Hand.Add(inst.TopCard);
    }

    /// <summary>≪퇴화 N≫ — 버린 매수 반환</summary>
    private int DeDigivolve(PlayerZones zones, CardInstance inst, int count)
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
    private bool ApplyDpMinus(CardInstance target, int amount, string source)
    {
        var zones = OwnerOf(target);
        target.TemporaryDpBonus -= amount;
        target.ActiveSkillBuffLabels.Add($"🔻{source} (DP -{amount:N0})");
        Log($"[효과] {source}: {SideName(zones)} [{target.TopCard.Name}] DP -{amount:N0}");

        if (target.TopCard.DP + target.GetTotalBonusDP() > 0)
            return false;

        DeleteFrom(zones, target);
        Log($"[효과] {SideName(zones)} [{target.TopCard.Name}]의 DP가 0이 되어 소멸했습니다.");

        // 플레이어 턴에 상대 디지몬이 DP 0으로 소멸 → 토코몬 / 파닥몬 진화원 [턴에 1회]
        if (ReferenceEquals(zones, State.DummyOpponent))
        {
            foreach (var mine in State.Player.BattleArea)
            {
                if (mine.HasSource("ST3-04") && mine.UsedOncePerTurn.Add("ST3-04"))
                {
                    State.MemoryGauge += 1;
                    Log($"[진화원 효과] 파닥몬: 상대 디지몬이 DP 0으로 소멸 → 메모리 +1");
                }
                if (mine.HasSource("ST3-01") && mine.UsedOncePerTurn.Add("ST3-01"))
                {
                    mine.TemporaryDpBonus += 1000;
                    Log($"[진화원 효과] 토코몬: [{mine.TopCard.Name}] 이 턴 동안 DP +1000");
                }
            }
        }
        return true;
    }

    /// <summary>트래시에서 조건에 맞는 디지몬을 코스트 없이 등장시킨다 ([등장 시] 효과는 발휘하지 않음)</summary>
    private List<CardData> PlayFromTrash(PlayerZones zones, Func<CardData, bool> match, int maxCount)
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

    /// <summary>상대 디지몬 중 효과 대상 고르기: UI가 넘겨준 대상이 조건에 맞으면 그것, 아니면 DP가 가장 높은 것</summary>
    private static CardInstance? PickTarget(IEnumerable<CardInstance> candidates, CardInstance? preferred, Func<CardInstance, bool>? match = null)
    {
        var list = candidates.Where(c => match == null || match(c)).ToList();
        if (preferred != null && list.Contains(preferred))
            return preferred;
        return list.OrderByDescending(c => c.GetCurrentDP()).ThenByDescending(c => c.TopCard.Level).FirstOrDefault();
    }

    // =====================================================================
    // 배틀 (블록 · 디지몬 어택) — ≪길동무≫ 포함
    // =====================================================================

    /// <summary>
    /// 두 디지몬의 배틀: DP가 낮은 쪽이 소멸, 같으면 둘 다 소멸. 혼자 진 쪽이 ≪길동무≫면 상대도 소멸.
    /// </summary>
    private (bool AttackerDeleted, bool DefenderDeleted) ResolveDigimonBattle(CardInstance attacker, CardInstance defender)
    {
        var atkZones = OwnerOf(attacker);
        var defZones = OwnerOf(defender);
        RecalculateFieldBuffs();
        int atkDp = attacker.GetCurrentDP();
        int defDp = defender.GetCurrentDP();

        bool attackerDeleted = atkDp <= defDp;
        bool defenderDeleted = atkDp >= defDp;

        if (attackerDeleted && !defenderDeleted && HasRetaliation(attacker))
        {
            defenderDeleted = true;
            Log($"[≪길동무≫] 배틀에서 진 [{attacker.TopCard.Name}]이(가) [{defender.TopCard.Name}]을(를) 함께 소멸시킵니다!");
        }
        else if (defenderDeleted && !attackerDeleted && HasRetaliation(defender))
        {
            attackerDeleted = true;
            Log($"[≪길동무≫] 배틀에서 진 [{defender.TopCard.Name}]이(가) [{attacker.TopCard.Name}]을(를) 함께 소멸시킵니다!");
        }

        if (defenderDeleted) DeleteFrom(defZones, defender);
        if (attackerDeleted) DeleteFrom(atkZones, attacker);

        // 아트라캅테리몬(ST4-11) 진화원: [자신의 턴] [턴에 1회] 배틀에서 상대 디지몬만 소멸시켰을 때 상대 시큐리티 위에서 1장 파기
        if (defenderDeleted && !attackerDeleted && attacker.HasSource("ST4-11") && attacker.UsedOncePerTurn.Add("ST4-11"))
        {
            var enemy = EnemyOf(atkZones);
            if (enemy.SecurityStack.Count > 0)
            {
                enemy.Trash.Add(enemy.SecurityStack[0]);
                enemy.SecurityStack.RemoveAt(0);
                Log($"[진화원 효과] 아트라캅테리몬: {SideName(enemy)} 시큐리티를 위에서부터 1장 파기!");
            }
        }

        return (attackerDeleted, defenderDeleted);
    }

    // =====================================================================
    // [등장 시] / [진화 시] / [어택 시] — 플레이어
    // =====================================================================

    private string ApplyOnPlayEffects(CardInstance instance)
    {
        var me = State.Player;
        var card = instance.TopCard;
        string summary = string.Empty;

        switch (card.CardId)
        {
            case "BT1-029": // 파피몬: ≪1 드로우≫
            {
                var drawn = DrawFor(me);
                if (drawn != null) summary = $"[등장 시] {drawn.Name} 드로우";
                break;
            }
            case "BT1-010": // 아구몬: 덱 위 5장 중 테이머 1장
            {
                var got = RevealAndTake(me, 5, c => c.Type == CardType.Tamer, 1);
                summary = got.Count > 0 ? $"[등장 시] 테이머 [{got[0].Name}] 패에 추가" : "[등장 시] 덱 위 5장에 테이머가 없습니다";
                break;
            }
            case "BT1-048": // 파닥몬: 덱 위 4장 중 옐로 테이머 전부
            {
                var got = RevealAndTake(me, 4, c => c.Type == CardType.Tamer && c.Color == CardColor.Yellow, 4);
                summary = got.Count > 0 ? $"[등장 시] 옐로 테이머 {got.Count}장 패에 추가" : "[등장 시] 덱 위 4장에 옐로 테이머가 없습니다";
                break;
            }
            case "ST4-03": // 텐타몬: 덱 위 1장이 그린 디지몬이면 패에
            {
                var got = RevealAndTake(me, 1, c => c.Type == CardType.Digimon && c.Color == CardColor.Green, 1);
                summary = got.Count > 0 ? $"[등장 시] [{got[0].Name}] 패에 추가" : "[등장 시] 그린 디지몬이 아니어서 덱 아래로";
                break;
            }
            case "BT1-067": // 팔몬: 덱 위 3장 중 Lv.4 디지몬 1장
            {
                var got = RevealAndTake(me, 3, c => c.Type == CardType.Digimon && c.Level == 4, 1);
                summary = got.Count > 0 ? $"[등장 시] [{got[0].Name}] 패에 추가" : "[등장 시] 덱 위 3장에 Lv.4 디지몬이 없습니다";
                break;
            }
            case "ST6-04": // 드라큐몬: 트래시의 퍼플 옵션(코스트 1 또는 7) 1장 패로
            {
                var opt = me.Trash.LastOrDefault(c => c.Type == CardType.Option && c.Color == CardColor.Purple && c.PlayCost is 1 or 7);
                if (opt != null)
                {
                    me.Trash.Remove(opt);
                    me.Hand.Add(opt);
                    summary = $"[등장 시] 트래시의 [{opt.Name}] 패로 회수";
                }
                break;
            }
            case "BT1-060": // 홀리엔젤몬: ≪리커버리 +1 (덱)≫
            {
                int n = Recovery(me, 1);
                if (n > 0) summary = "[등장 시] ≪리커버리 +1≫ 시큐리티 1장 추가";
                break;
            }
            case "BT2-039": // 홀리드라몬: 시큐리티 3장 이하면 ≪리커버리 +2≫
            {
                if (me.SecurityStack.Count <= 3)
                {
                    int n = Recovery(me, 2);
                    summary = $"[등장 시] ≪리커버리 +2≫ 시큐리티 {n}장 추가";
                }
                break;
            }
            case "BT2-080": // 피에몬: 트래시의 퍼플 Lv.4 이하 디지몬 2장까지 등장
            {
                var played = PlayFromTrash(me, c => c.Color == CardColor.Purple && c.Level <= 4, 2);
                if (played.Count > 0) summary = $"[등장 시] 트래시에서 {string.Join(", ", played.Select(p => p.Name))} 등장";
                break;
            }
        }

        // 크리사리몬(BT2-059) 진화원: 같은 명칭의 다른 자신의 디지몬이 등장했을 때 메모리 +1
        foreach (var other in me.BattleArea.Where(d => !ReferenceEquals(d, instance) && d.HasSource("BT2-059") && d.TopCard.Name == card.Name))
        {
            State.MemoryGauge += 1;
            Log($"[진화원 효과] 크리사리몬: 같은 명칭 [{card.Name}] 등장 → 메모리 +1");
        }

        if (summary.Length > 0)
            Log($"[효과] {card.Name} {summary}");
        RecalculateFieldBuffs();
        return summary;
    }

    private void ApplyWhenDigivolvingEffects(CardInstance target, CardData evoCard)
    {
        var me = State.Player;
        var opp = State.DummyOpponent;
        int seq = State.TurnSequence;

        switch (evoCard.CardId)
        {
            case "ST1-08": // 가루다몬: 이 턴 동안 자신의 디지몬 1마리 DP +3000
                target.TemporaryDpBonus += 3000;
                target.ActiveSkillBuffLabels.Add("🔥가루다몬 [진화 시] (DP +3,000)");
                Log($"[진화 시] 가루다몬: 이 턴 동안 DP +3000 (현재 DP: {target.GetCurrentDP()})");
                break;

            case "ST2-09": // 쥬드몬: 상대 디지몬 1마리의 진화원을 아래에서 2장 파기
            {
                var enemy = opp.BattleArea.Where(d => d.EvolutionSources.Count > 0).OrderByDescending(d => d.TopCard.Level).FirstOrDefault();
                if (enemy != null)
                {
                    var removed = enemy.RemoveBottomSources(2);
                    opp.Trash.AddRange(removed);
                    Log($"[진화 시] 쥬드몬: 상대 [{enemy.TopCard.Name}]의 진화원 {removed.Count}장 파기");
                }
                break;
            }

            case "BT1-025": // 워그레이몬(BT1): 이 턴 동안 ≪시큐리티 어택 +1≫
                target.TemporarySecurityAttackBonus += 1;
                target.ActiveSkillBuffLabels.Add("🔥워그레이몬 [진화 시] (시큐어택 +1)");
                Log("[진화 시] 워그레이몬: 이 턴 동안 ≪시큐리티 어택 +1≫");
                break;

            case "BT2-020": // 듀크몬: 레드 테이머가 있으면 DP 6000 이하 상대 디지몬 1마리 소멸
                if (me.Tamers.Any(t => t.Color == CardColor.Red))
                {
                    var enemy = PickTarget(opp.BattleArea, null, d => d.GetCurrentDP() <= 6000);
                    if (enemy != null)
                    {
                        DeleteOpponentDigimon(enemy);
                        Log($"[진화 시] 듀크몬: 상대 [{enemy.TopCard.Name}] 소멸!");
                    }
                }
                break;

            case "ST3-09": // 엔젤우몬: 시큐리티 3장 이하면 ≪리커버리 +1≫
                if (me.SecurityStack.Count <= 3 && Recovery(me, 1) > 0)
                    Log("[진화 시] 엔젤우몬: ≪리커버리 +1≫ 시큐리티 1장 추가");
                break;

            case "ST4-10": // 릴리몬: 덱 위 5장 중 Lv.6 이상 디지몬 1장
            {
                var got = RevealAndTake(me, 5, c => c.Type == CardType.Digimon && c.Level >= 6, 1);
                Log(got.Count > 0 ? $"[진화 시] 릴리몬: [{got[0].Name}] 패에 추가" : "[진화 시] 릴리몬: 덱 위 5장에 Lv.6 이상 디지몬이 없습니다");
                break;
            }

            case "BT1-074": // 니드몬(BT1): 덱 위 3장 중 Lv.5 이상 디지몬 1장
            {
                var got = RevealAndTake(me, 3, c => c.Type == CardType.Digimon && c.Level >= 5, 1);
                Log(got.Count > 0 ? $"[진화 시] 니드몬: [{got[0].Name}] 패에 추가" : "[진화 시] 니드몬: 덱 위 3장에 Lv.5 이상 디지몬이 없습니다");
                break;
            }

            case "ST4-12": // 로제몬: 다음 상대의 턴 종료 시까지 상대 디지몬 1마리 어택 · 블록 불가
            {
                var enemy = PickTarget(opp.BattleArea, null, d => !d.IsSuspended) ?? PickTarget(opp.BattleArea, null);
                if (enemy != null)
                {
                    enemy.CannotAttackUntil = Math.Max(enemy.CannotAttackUntil, seq + 1);
                    enemy.CannotBlockUntil = Math.Max(enemy.CannotBlockUntil, seq + 1);
                    Log($"[진화 시] 로제몬: 상대 [{enemy.TopCard.Name}]은(는) 다음 상대 턴 종료 시까지 어택 · 블록 불가");
                }
                break;
            }

            case "ST5-09": // 메탈그레이몬(흑): 다음 상대의 턴 종료 시까지 자신의 디지몬 1마리 ≪블로커≫
                target.GrantedBlockerUntil = Math.Max(target.GrantedBlockerUntil, seq + 1);
                Log($"[진화 시] 메탈그레이몬: [{target.TopCard.Name}]이(가) 다음 상대 턴 종료 시까지 ≪블로커≫를 얻습니다");
                break;

            case "ST5-12": // 파워드라몬: 다음 상대의 턴 종료 시까지 자신의 디지몬 2마리까지 ≪재기동≫
            {
                var picks = new List<CardInstance> { target };
                var other = me.BattleArea.Where(d => !ReferenceEquals(d, target)).OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();
                if (other != null) picks.Add(other);
                foreach (var p in picks) p.GrantedRebootUntil = Math.Max(p.GrantedRebootUntil, seq + 1);
                Log($"[진화 시] 파워드라몬: {string.Join(", ", picks.Select(p => p.TopCard.Name))}이(가) ≪재기동≫을 얻습니다");
                break;
            }

            case "ST6-10": // 스컬사탄몬: 트래시의 퍼플 디지몬 1장 패로
            {
                var card = me.Trash.Where(c => c.Type == CardType.Digimon && c.Color == CardColor.Purple).OrderByDescending(c => c.Level).FirstOrDefault();
                if (card != null)
                {
                    me.Trash.Remove(card);
                    me.Hand.Add(card);
                    Log($"[진화 시] 스컬사탄몬: 트래시의 [{card.Name}] 패로 회수");
                }
                break;
            }

            case "BT1-084": // 오메가몬: 상대 디지몬 1마리를 골라 같은 명칭의 상대 디지몬 전부 소멸
            {
                var chosen = PickTarget(opp.BattleArea, null);
                if (chosen != null)
                {
                    var victims = opp.BattleArea.Where(d => d.TopCard.Name == chosen.TopCard.Name).ToList();
                    foreach (var v in victims) DeleteOpponentDigimon(v);
                    Log($"👑 [진화 시] 오메가몬: 상대 [{chosen.TopCard.Name}] 명칭의 디지몬 {victims.Count}마리 전부 소멸!");
                }
                break;
            }

            case "BT3-112": // 오메가몬 Alter-S: 상대 전부 ≪퇴화 1≫ 후 DP 5000 이하 전부 소멸
            {
                foreach (var d in opp.BattleArea.ToList()) DeDigivolve(opp, d, 1);
                RecalculateFieldBuffs();
                var victims = opp.BattleArea.Where(d => d.GetCurrentDP() <= 5000).ToList();
                foreach (var v in victims) DeleteOpponentDigimon(v);
                Log($"👑 [진화 시] 오메가몬 Alter-S: 상대 전부 ≪퇴화 1≫ → DP 5000 이하 {victims.Count}마리 소멸!");
                break;
            }

            case "BT5-086": // 오메가몬(BT5): 이 디지몬을 액티브로 한다
                target.IsSuspended = false;
                Log("👑 [진화 시] 오메가몬: 액티브 상태가 됩니다");
                break;

            case "BT3-031": // 황제드라몬: ≪재밍≫을 가진 자신의 디지몬 전부 액티브
                foreach (var d in me.BattleArea.Where(HasJamming)) d.IsSuspended = false;
                Log("[진화 시] 황제드라몬: ≪재밍≫을 가진 자신의 디지몬 전부 액티브");
                break;
        }

        RecalculateFieldBuffs();
    }

    /// <summary>플레이어 디지몬의 [어택 시] 효과 (자신 + 진화원)</summary>
    private void TriggerWhenAttackingEffects(CardInstance attacker, CardInstance? defender)
    {
        var me = State.Player;
        var opp = State.DummyOpponent;
        var top = attacker.TopCard;

        if (LoseTwoMemoryOnAttackIds.Contains(top.CardId))
        {
            State.MemoryGauge -= 2;
            Log($"[어택 시] {top.Name}: 메모리 -2 (현재 메모리: {State.MemoryGauge})");
        }

        switch (top.CardId)
        {
            case "BT1-021": // 메탈그레이몬(BT1) / 워가루몬(BT1): 메모리 +3, 이 턴 종료 시 -3
            case "BT1-040":
                State.MemoryGauge += 3;
                State.PendingEndOfTurnMemoryLoss += 3;
                Log($"[어택 시] {top.Name}: 메모리 +3 (이 턴 종료 시 -3) → 현재 메모리: {State.MemoryGauge}");
                break;

            case "ST2-11": // 메탈가루몬: [턴에 1회] 이 디지몬을 액티브로
                if (attacker.UsedOncePerTurn.Add("ST2-11"))
                {
                    attacker.IsSuspended = false;
                    Log("❄️ [어택 시] 메탈가루몬: 액티브 상태로 전환 — 이번 턴 한 번 더 어택할 수 있습니다!");
                }
                break;

            case "ST3-11": // 세라피몬: 상대 디지몬 1마리 DP -4000
            {
                var t = defender ?? PickTarget(opp.BattleArea, null, d => d.GetCurrentDP() <= 4000) ?? EffectRules.FindBlocker(State, opp.BattleArea, attacker);
                if (t != null) ApplyDpMinus(t, 4000, "세라피몬 [어택 시]");
                break;
            }

            case "BT2-020": // 듀크몬: 상대 트래시 10장마다 상대 시큐리티 1장 파기
            {
                int n = Math.Min(opp.Trash.Count / 10, opp.SecurityStack.Count);
                for (int i = 0; i < n; i++)
                {
                    opp.Trash.Add(opp.SecurityStack[0]);
                    opp.SecurityStack.RemoveAt(0);
                }
                if (n > 0) Log($"[어택 시] 듀크몬: 상대 시큐리티 {n}장 파기!");
                break;
            }

            case "BT2-039": // 홀리드라몬: 패의 옐로 Lv.3 디지몬 1장을 코스트 없이 등장
            {
                var lv3 = me.Hand.FirstOrDefault(c => c.Type == CardType.Digimon && c.Color == CardColor.Yellow && c.Level == 3);
                if (lv3 != null && me.BattleArea.Count < 8)
                {
                    me.Hand.Remove(lv3);
                    var inst = new CardInstance(lv3, State.CurrentTurn);
                    me.BattleArea.Add(inst);
                    Log($"[어택 시] 홀리드라몬: 패의 [{lv3.Name}]을(를) 코스트 없이 등장");
                    ApplyOnPlayEffects(inst);
                }
                break;
            }

            case "BT3-112": // 오메가몬 Alter-S: 진화원의 Lv.6 디지몬 1장을 패로 → 이 턴 동안 블록당하지 않음 (막을 블로커가 있을 때만)
                if (EffectRules.FindBlocker(State, opp.BattleArea.Where(d => !ReferenceEquals(d, defender)), attacker) != null)
                {
                    var lv6 = attacker.TakeSource(s => s.Type == CardType.Digimon && s.Level == 6);
                    if (lv6 != null)
                    {
                        me.Hand.Add(lv6);
                        attacker.UnblockableUntil = State.TurnSequence;
                        Log($"👑 [어택 시] 오메가몬 Alter-S: 진화원 [{lv6.Name}]을(를) 패로 되돌려 이 턴 동안 블록당하지 않습니다!");
                    }
                }
                break;

            case "BT1-084": // 오메가몬: 진화원의 Lv.6 디지몬 1장을 패로 → 액티브
                if (attacker.UsedOncePerTurn.Add("BT1-084"))
                {
                    var lv6 = attacker.TakeSource(s => s.Type == CardType.Digimon && s.Level == 6);
                    if (lv6 != null)
                    {
                        me.Hand.Add(lv6);
                        attacker.IsSuspended = false;
                        Log($"👑 [어택 시] 오메가몬: 진화원 [{lv6.Name}]을(를) 패로 되돌리고 액티브!");
                    }
                }
                break;
        }

        // ---- 진화원 효과 ----
        if (defender != null)
        {
            if (attacker.HasSource("BT1-001")) { attacker.TemporaryDpBonus += 1000; Log("[진화원 효과] 어니몬: 상대 디지몬에 어택 → 이 턴 DP +1000"); }
            if (attacker.HasSource("ST4-04")) { attacker.TemporaryDpBonus += 2000; Log("[진화원 효과] 팔몬: 상대 디지몬에 어택 → 이 턴 DP +2000"); }
            if (attacker.HasSource("ST4-06")) { attacker.TemporaryDpBonus += 2000; Log("[진화원 효과] 니드몬: 상대 디지몬에 어택 → 이 턴 DP +2000"); }
        }

        if (attacker.HasSource("ST2-03")) // 파피몬: Lv.5 이하 상대 디지몬 1마리의 진화원 아래에서 1장 파기
            TrashBottomSourceOfEnemy(opp, d => d.TopCard.Level <= 5, "파피몬");
        if (attacker.HasSource("ST2-06")) // 가루몬: 상대 디지몬 1마리의 진화원 아래에서 1장 파기
            TrashBottomSourceOfEnemy(opp, _ => true, "가루몬");

        if (attacker.HasSource("BT1-003") && opp.BattleArea.Any(d => d.EvolutionSources.Count == 0) && attacker.UsedOncePerTurn.Add("BT1-003"))
        {
            var drawn = DrawFor(me);
            if (drawn != null) Log($"[진화원 효과] 우파몬: ≪1 드로우≫ [{drawn.Name}]");
        }

        if (attacker.HasSource("ST3-05") && me.SecurityStack.Count >= 4)
        {
            State.MemoryGauge += 1;
            Log("[진화원 효과] 엔젤몬: 시큐리티 4장 이상 → 메모리 +1");
        }

        if (attacker.HasSource("ST3-08"))
        {
            var t = defender ?? PickTarget(opp.BattleArea, null, d => d.GetCurrentDP() <= 1000) ?? EffectRules.FindBlocker(State, opp.BattleArea, attacker);
            if (t != null && opp.BattleArea.Contains(t)) ApplyDpMinus(t, 1000, "홀리엔젤몬 진화원");
        }

        if (attacker.HasSource("BT1-079")) // 릴리몬: ≪블로커≫를 갖지 않은 상대 디지몬 1마리 레스트
        {
            var t = PickTarget(opp.BattleArea, null, d => !d.IsSuspended && !d.HasBlocker);
            if (t != null)
            {
                t.IsSuspended = true;
                Log($"[진화원 효과] 릴리몬: 상대 [{t.TopCard.Name}] 레스트");
            }
        }

        foreach (var id in new[] { "ST6-03", "ST6-06" }) // 파피몬 · 가루몬(퍼플): ≪1 드로우≫ 후 패 1장 파기
        {
            if (!attacker.HasSource(id)) continue;
            var drawn = DrawFor(me);
            var discard = me.Hand.Where(c => c != drawn).OrderBy(c => c.Type == CardType.Digimon ? 0 : 1).ThenBy(c => c.Level).FirstOrDefault() ?? drawn;
            if (discard != null)
            {
                me.Hand.Remove(discard);
                me.Trash.Add(discard);
            }
            Log($"[진화원 효과] ≪1 드로우≫ {(drawn != null ? $"[{drawn.Name}]" : "(덱 없음)")} 후 패의 [{discard?.Name}] 파기");
        }

        RecalculateFieldBuffs();
    }

    private void TrashBottomSourceOfEnemy(PlayerZones enemyZones, Func<CardInstance, bool> match, string source)
    {
        var target = enemyZones.BattleArea.Where(d => d.EvolutionSources.Count > 0 && match(d))
            .OrderByDescending(d => d.TopCard.Level).FirstOrDefault();
        if (target == null) return;
        var removed = target.RemoveBottomSources(1);
        enemyZones.Trash.AddRange(removed);
        Log($"[진화원 효과] {source}: 상대 [{target.TopCard.Name}]의 진화원 [{removed[0].Name}] 파기");
    }

    // =====================================================================
    // 옵션 [메인] — 플레이어
    // =====================================================================

    private string ApplyOptionEffect(CardData optionCard, CardInstance? target)
    {
        var me = State.Player;
        var opp = State.DummyOpponent;
        int seq = State.TurnSequence;
        CardInstance? enemyTarget = target != null && opp.BattleArea.Contains(target) ? target : null;
        CardInstance? allyTarget = target != null && me.BattleArea.Contains(target) ? target : null;
        string msg = string.Empty;

        switch (optionCard.CardId)
        {
            case "ST1-13": // 섀도 윙 / 천국의 문: 이 턴 동안 자신의 디지몬 1마리 DP +3000
            case "ST3-13":
            {
                var ally = allyTarget ?? me.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();
                if (ally != null)
                {
                    ally.TemporaryDpBonus += 3000;
                    ally.ActiveSkillBuffLabels.Add($"✨{optionCard.Name} (DP +3,000)");
                    msg = $"[{ally.TopCard.Name}]의 DP가 이번 턴 +3000 상승했습니다! (현재 DP: {ally.GetCurrentDP()})";
                }
                break;
            }

            case "ST1-14": // 스타라이트 익스플로전: 다음 상대 턴 종료 시까지 자신의 시큐리티 디지몬 전부 DP +7000
                State.PlayerSecurityDpBuffs.Add((7000, seq + 1));
                msg = "다음 상대 턴 종료 시까지 내 시큐리티 디지몬 전부 DP +7000!";
                break;

            case "ST1-15": // 기가 디스트로이어: DP 4000 이하 상대 디지몬 2마리까지 소멸
            {
                var victims = opp.BattleArea.Where(d => d.GetCurrentDP() <= 4000)
                    .OrderByDescending(d => ReferenceEquals(d, enemyTarget)).ThenByDescending(d => d.GetCurrentDP()).Take(2).ToList();
                foreach (var v in victims) DeleteOpponentDigimon(v);
                msg = victims.Count > 0
                    ? $"💥 기가 디스트로이어 발동! 상대 [{string.Join(", ", victims.Select(v => v.TopCard.Name))}] 소멸!"
                    : "DP 4000 이하인 상대 디지몬이 없어 효과가 없습니다.";
                break;
            }

            case "ST1-16": // 가이아 포스: 상대 디지몬 1마리 소멸
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget);
                if (enemy != null)
                {
                    DeleteOpponentDigimon(enemy);
                    msg = $"🔥 가이아 포스 발동! 상대 [{enemy.TopCard.Name}] 소멸!";
                }
                break;
            }

            case "ST2-13": // 해머 스파크: 메모리 +1
                State.MemoryGauge += 1;
                msg = $"⚡ 해머 스파크: 메모리 +1 (현재 메모리: {State.MemoryGauge})";
                break;

            case "ST2-14": // 소로 블루: 진화원 없는 상대 디지몬 1마리, 다음 상대 턴 종료 시까지 어택 · 블록 불가
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget, d => d.EvolutionSources.Count == 0);
                if (enemy != null)
                {
                    enemy.CannotAttackUntil = Math.Max(enemy.CannotAttackUntil, seq + 1);
                    enemy.CannotBlockUntil = Math.Max(enemy.CannotBlockUntil, seq + 1);
                    msg = $"❄️ 소로 블루: 상대 [{enemy.TopCard.Name}]은(는) 다음 상대 턴 종료 시까지 어택 · 블록 불가!";
                }
                else msg = "진화원을 갖지 않은 상대 디지몬이 없어 효과가 없습니다.";
                break;
            }

            case "ST2-15": // 카이저 네일: 자신의 디지몬 진화원의 디지몬 카드 1장을 코스트 없이 등장
            {
                var ally = PickTarget(me.BattleArea, allyTarget, d => d.EvolutionSources.Any(s => s.Type == CardType.Digimon));
                var card = ally?.TakeSource(s => s.Type == CardType.Digimon && s.Level == ally.EvolutionSources.Where(x => x.Type == CardType.Digimon).Max(x => x.Level));
                if (ally != null && card != null)
                {
                    me.BattleArea.Add(new CardInstance(card, State.CurrentTurn));
                    msg = $"❄️ 카이저 네일: [{ally.TopCard.Name}]의 진화원 [{card.Name}(Lv.{card.Level})]이(가) 코스트 없이 등장!";
                }
                break;
            }

            case "ST2-16": // 코큐토스 브레스: 상대 디지몬 1마리를 패로 (진화원은 파기)
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget);
                if (enemy != null)
                {
                    ReturnToHand(opp, enemy);
                    msg = $"🌊 코큐토스 브레스: 상대 [{enemy.TopCard.Name}]을(를) 패로 되돌렸습니다!";
                }
                break;
            }

            case "ST3-16": // 세븐 헤븐즈: 이 턴 동안 상대 디지몬 1마리 DP -10000
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget);
                if (enemy != null)
                {
                    bool deleted = ApplyDpMinus(enemy, 10000, "세븐 헤븐즈");
                    msg = deleted ? $"✨ 세븐 헤븐즈: 상대 [{enemy.TopCard.Name}] DP 0 → 소멸!" : $"✨ 세븐 헤븐즈: 상대 [{enemy.TopCard.Name}] DP -10000";
                }
                break;
            }

            case "ST4-16": // 전기 충격: 레스트 상태 상대 디지몬 1마리를 패로
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget, d => d.IsSuspended);
                if (enemy != null)
                {
                    ReturnToHand(opp, enemy);
                    msg = $"⚡ 전기 충격: 레스트 상태의 상대 [{enemy.TopCard.Name}]을(를) 패로 되돌렸습니다!";
                }
                else msg = "레스트 상태인 상대 디지몬이 없어 효과가 없습니다.";
                break;
            }

            case "ST5-15": // 레이저 아이: 상대 디지몬 2마리까지 ≪퇴화 1≫
            {
                var picks = opp.BattleArea.Where(d => d.EvolutionSources.Count > 0 && d.TopCard.Level > 3)
                    .OrderByDescending(d => ReferenceEquals(d, enemyTarget)).ThenByDescending(d => d.TopCard.Level).Take(2).ToList();
                var names = picks.Select(p => p.TopCard.Name).ToList();
                foreach (var p in picks) DeDigivolve(opp, p, 1);
                msg = picks.Count > 0 ? $"🔻 레이저 아이: 상대 [{string.Join(", ", names)}] ≪퇴화 1≫" : "퇴화시킬 상대 디지몬이 없습니다.";
                break;
            }

            case "ST5-16": // 제노사이드 어택: 등장 코스트 7 이하 상대 디지몬 1마리 소멸
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget, d => d.TopCard.PlayCost <= 7);
                if (enemy != null)
                {
                    DeleteOpponentDigimon(enemy);
                    msg = $"🔥 제노사이드 어택: 상대 [{enemy.TopCard.Name}] 소멸!";
                }
                else msg = "등장 코스트 7 이하인 상대 디지몬이 없습니다.";
                break;
            }

            case "ST6-15": // 데스 클로: 자신의 디지몬 1마리를 소멸시키는 것으로 Lv.4 이하 상대 디지몬 1마리 소멸
            {
                var enemy = PickTarget(opp.BattleArea, enemyTarget, d => d.TopCard.Level <= 4);
                var sacrifice = me.BattleArea.OrderBy(d => d.TopCard.Level).ThenBy(d => d.GetCurrentDP()).FirstOrDefault();
                if (enemy != null && sacrifice != null)
                {
                    DeleteDigimon(sacrifice);
                    DeleteOpponentDigimon(enemy);
                    msg = $"💀 데스 클로: 내 [{sacrifice.TopCard.Name}]을(를) 소멸시켜 상대 [{enemy.TopCard.Name}] 소멸!";
                }
                else msg = "소멸시킬 내 디지몬 또는 Lv.4 이하 상대 디지몬이 없어 효과가 없습니다.";
                break;
            }

            case "ST6-16": // 어둠의 저주: 트래시의 퍼플 Lv.3 1장 + 퍼플 Lv.4 1장 코스트 없이 등장
            {
                var played = PlayFromTrash(me, c => c.Color == CardColor.Purple && c.Level == 3, 1)
                    .Concat(PlayFromTrash(me, c => c.Color == CardColor.Purple && c.Level == 4, 1)).ToList();
                msg = played.Count > 0 ? $"🌑 어둠의 저주: 트래시에서 {string.Join(", ", played.Select(p => p.Name))} 등장!" : "트래시에 등장시킬 퍼플 디지몬이 없습니다.";
                break;
            }
        }

        if (msg.Length > 0)
            Log("[효과] " + msg);
        RecalculateFieldBuffs();
        return msg;
    }

    // =====================================================================
    // 옵션 [시큐리티] — 양쪽 공통 (owner = 시큐리티 주인, attacker = 어택 중인 상대 디지몬)
    // =====================================================================

    private record SecurityOptionOutcome(string Log, string Summary, bool AttackerRemoved, int OwnerMemoryGain, bool ReturnToHand);

    private SecurityOptionOutcome ResolveSecurityOption(CardData option, PlayerZones owner, CardInstance attacker)
    {
        var enemy = EnemyOf(owner);
        string who = SideName(owner);
        int seq = State.TurnSequence;
        bool ownerIsPlayer = ReferenceEquals(owner, State.Player);
        string head = $"[{who} 시큐리티 {option.Name} 발동!]";

        SecurityOptionOutcome Done(string detail, string summary, bool removed = false, int memory = 0, bool toHand = false) =>
            new($"{head} {detail}", $"[시큐리티 {option.Name}] {summary}", removed, memory, toHand);

        switch (option.CardId)
        {
            case "ST1-13": // 섀도 윙: 다음 자신의 턴 종료 시까지 자신의 디지몬 전부 ≪시큐리티 어택 +1≫
                if (ownerIsPlayer) State.PlayerAllSecurityAttackUntil = seq + 1;
                else State.OpponentAllSecurityAttackUntil = seq + 1;
                return Done("다음 자신의 턴 종료 시까지 디지몬 전부 ≪시큐리티 어택 +1≫", "시큐리티 어택 +1 부여");

            case "ST1-14": // 스타라이트 익스플로전: 이 턴 동안 자신의 시큐리티 디지몬 전부 DP +7000
                (ownerIsPlayer ? State.PlayerSecurityDpBuffs : State.OpponentSecurityDpBuffs).Add((7000, seq));
                return Done("이 턴 동안 시큐리티 디지몬 전부 DP +7000", "시큐리티 디지몬 DP +7000");

            case "ST3-13": // 천국의 문: 이 턴 동안 자신의 디지몬과 시큐리티 디지몬 전부 DP +5000, 이 카드는 패로
                foreach (var d in owner.BattleArea) d.TemporaryDpBonus += 5000;
                (ownerIsPlayer ? State.PlayerSecurityDpBuffs : State.OpponentSecurityDpBuffs).Add((5000, seq));
                return Done("이 턴 동안 디지몬 · 시큐리티 디지몬 전부 DP +5000, 이 카드는 패로", "DP +5000 · 패로 회수", toHand: true);

            case "ST1-15": // 기가 디스트로이어
            {
                var victims = enemy.BattleArea.Where(d => d.GetCurrentDP() <= 4000)
                    .OrderByDescending(d => ReferenceEquals(d, attacker)).Take(2).ToList();
                foreach (var v in victims) DeleteFrom(enemy, v);
                return Done($"DP 4000 이하 디지몬 {victims.Count}마리 소멸", $"{victims.Count}마리 소멸", victims.Contains(attacker));
            }

            case "ST1-16": // 가이아 포스: 상대 디지몬 1마리 소멸 → 어택 중인 디지몬
                DeleteFrom(enemy, attacker);
                return Done($"어택하던 [{attacker.TopCard.Name}] 소멸!", $"[{attacker.TopCard.Name}] 소멸", true);

            case "ST5-16": // 제노사이드 어택: 등장 코스트 7 이하
            {
                var t = PickTarget(enemy.BattleArea, attacker, d => d.TopCard.PlayCost <= 7);
                if (t == null) return Done("등장 코스트 7 이하 디지몬이 없어 불발", "불발");
                DeleteFrom(enemy, t);
                return Done($"[{t.TopCard.Name}] 소멸!", $"[{t.TopCard.Name}] 소멸", ReferenceEquals(t, attacker));
            }

            case "ST6-15": // 데스 클로 [시큐리티]: Lv.4 이하 상대 디지몬 1마리 소멸
            {
                var t = PickTarget(enemy.BattleArea, attacker, d => d.TopCard.Level <= 4);
                if (t == null) return Done("Lv.4 이하 디지몬이 없어 불발", "불발");
                DeleteFrom(enemy, t);
                return Done($"[{t.TopCard.Name}] 소멸!", $"[{t.TopCard.Name}] 소멸", ReferenceEquals(t, attacker));
            }

            case "ST3-16": // 세븐 헤븐즈: DP -10000
            {
                bool deleted = ApplyDpMinus(attacker, 10000, "세븐 헤븐즈 [시큐리티]");
                return Done(deleted ? $"[{attacker.TopCard.Name}] DP 0 → 소멸!" : $"[{attacker.TopCard.Name}] DP -10000",
                    deleted ? $"[{attacker.TopCard.Name}] 소멸" : "DP -10000", deleted);
            }

            case "ST2-16": // 코큐토스 브레스: 패로
                ReturnToHand(enemy, attacker);
                return Done($"어택하던 [{attacker.TopCard.Name}]을(를) 패로 되돌렸습니다!", $"[{attacker.TopCard.Name}] 패로", true);

            case "ST4-16": // 전기 충격: 레스트 상태 디지몬을 패로 (어택 중인 디지몬은 레스트 상태)
            {
                var t = PickTarget(enemy.BattleArea, attacker, d => d.IsSuspended);
                if (t == null) return Done("레스트 상태 디지몬이 없어 불발", "불발");
                ReturnToHand(enemy, t);
                return Done($"[{t.TopCard.Name}]을(를) 패로 되돌렸습니다!", $"[{t.TopCard.Name}] 패로", ReferenceEquals(t, attacker));
            }

            case "ST2-13": // 해머 스파크: 메모리 +2
                return Done("메모리 +2", "메모리 +2", memory: 2);

            case "ST2-14": // 소로 블루: 진화원 없는 디지몬, 다음 자신의 턴 종료 시까지 어택 · 블록 불가
            {
                var t = PickTarget(enemy.BattleArea, attacker, d => d.EvolutionSources.Count == 0);
                if (t == null) return Done("진화원을 갖지 않은 디지몬이 없어 불발", "불발");
                t.CannotAttackUntil = Math.Max(t.CannotAttackUntil, seq + 1);
                t.CannotBlockUntil = Math.Max(t.CannotBlockUntil, seq + 1);
                return Done($"[{t.TopCard.Name}]은(는) 다음 자신의 턴 종료 시까지 어택 · 블록 불가", $"[{t.TopCard.Name}] 봉인");
            }

            case "ST5-15": // 레이저 아이: 2마리까지 ≪퇴화 1≫
            {
                var picks = enemy.BattleArea.Where(d => d.EvolutionSources.Count > 0 && d.TopCard.Level > 3)
                    .OrderByDescending(d => ReferenceEquals(d, attacker)).ThenByDescending(d => d.TopCard.Level).Take(2).ToList();
                foreach (var p in picks) DeDigivolve(enemy, p, 1);
                return Done($"디지몬 {picks.Count}마리 ≪퇴화 1≫", $"{picks.Count}마리 퇴화");
            }

            case "ST2-15": // 카이저 네일: 자신의 디지몬 진화원의 디지몬 카드 1장 등장
            {
                var host = owner.BattleArea.FirstOrDefault(d => d.EvolutionSources.Any(s => s.Type == CardType.Digimon));
                var card = host?.TakeSource(s => s.Type == CardType.Digimon);
                if (card == null) return Done("꺼낼 진화원이 없어 불발", "불발");
                owner.BattleArea.Add(new CardInstance(card, State.CurrentTurn) { IsOwnersTurn = false });
                return Done($"진화원의 [{card.Name}]이(가) 코스트 없이 등장!", $"[{card.Name}] 등장");
            }

            case "ST6-16": // 어둠의 저주 [시큐리티]: 트래시의 퍼플 Lv.4 이하 1장 등장
            {
                var played = PlayFromTrash(owner, c => c.Color == CardColor.Purple && c.Level <= 4, 1);
                return played.Count > 0 ? Done($"트래시의 [{played[0].Name}] 등장!", $"[{played[0].Name}] 등장") : Done("트래시에 퍼플 디지몬이 없어 불발", "불발");
            }

            default:
                return Done("[시큐리티] 효과가 없는 카드입니다.", "효과 없음");
        }
    }
}
