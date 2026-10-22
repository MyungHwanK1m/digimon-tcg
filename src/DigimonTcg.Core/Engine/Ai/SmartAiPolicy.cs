using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Ai;

/// <summary>
/// 한 수 앞을 보는 AI (AiBoard의 어림 점수로 후보를 비교해 가장 좋은 것만, 이득이 있을 때만 한다):
/// - 어택: 어택하는 디지몬 × 대상(시큐리티 · 상대 레스트 디지몬)마다 블록 · 배틀 · 시큐리티 위험 · 반격 위험을 보고 고른다.
///   이득이 없으면 어택하지 않는다 (다이렉트 어택은 언제나 우선).
/// - 진화: 오르는 DP · 이번 턴 어택 가능 여부 · [진화 시] 효과 · 코스트를 견준다.
/// - 옵션: [메인] 효과가 지금 쓸모 있을 때(대상이 있을 때) 점수가 코스트보다 크면 쓴다 — 블로커 제거 같은 효과는 어택 전에.
/// - [메인] 기동: 효과 점수가 양수이고 대상이 있을 때.
/// - 등장: 디지몬 가치 · 진화 이어 갈 수 있는지 · ≪블로커≫ 필요 · [등장 시] 효과 − 넘겨줄 메모리.
/// </summary>
public sealed class SmartAiPolicy : IAiPolicy
{
    private static readonly LegacyAiPolicy Legacy = new();

    public SmartAiTuning Tuning { get; }

    public SmartAiPolicy(SmartAiTuning? tuning = null)
    {
        Tuning = tuning ?? new SmartAiTuning();
    }

    public string Name => "개선 AI (한 수 앞 평가)";
    public int MaxMemoryHandOver => 3;

    public AiEvolution? ChooseEvolution(GoldfishEngine engine, int memory, bool beforeAttacks)
    {
        if (!Tuning.SmartEvolution)
            return Legacy.ChooseEvolution(engine, memory, beforeAttacks);
        var ai = engine.State.DummyOpponent;
        int limit = beforeAttacks ? memory : memory + MaxMemoryHandOver;
        AiEvolution? best = null;
        double bestScore = 0;
        foreach (var field in LegacyAiPolicy.EvolutionBases(ai))
        {
            bool inBattle = ai.BattleArea.Contains(field);
            foreach (var card in ai.Hand.Distinct())
            {
                if (!ActionValidator.ValidateEvolution(field, card).CanExecute)
                    continue;
                int cost = engine.PreviewEvoCost(ai, field, card);   // 진화 코스트 감소 반영 (엔진이 실제로 받는 값)
                if (cost > limit)
                    continue;
                double score = 1.5                                          // Lv이 오르고 진화 보너스로 1장 드로우
                    + (card.DP - field.TopCard.DP) / 1000.0 * 0.8
                    + (inBattle && beforeAttacks && engine.CanAttackThisTurn(field) ? 1.5 : 0)   // 진화한 그대로 더 세게 어택
                    + (inBattle ? Math.Max(0, AiBoard.TextValue(AiBoard.MainPart(card.EffectText))) * 0.5 : 0)
                    - cost * 0.35
                    - Math.Max(0, cost - memory) * 0.4;                    // 상대에게 넘겨줄 메모리
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new AiEvolution(field, card);
                }
            }
        }
        // 엔진은 진화를 DNA 진화보다 먼저 묻는다 — DNA 진화가 더 이득이면 짝이 될 디지몬을 진화로 망가뜨리지 않도록 진화하지 않는다
        if (best != null && BestDna(engine, memory, beforeAttacks, out double dnaScore) != null && dnaScore >= bestScore)
            return null;
        return best;
    }

    public AiOptionUse? ChooseOption(GoldfishEngine engine, int memory, bool beforeAttacks)
    {
        if (!Tuning.UseOptions)
            return null;
        var state = engine.State;
        var ai = state.DummyOpponent;
        int limit = beforeAttacks ? memory : memory + MaxMemoryHandOver;
        CardData? best = null;
        double bestScore = Tuning.OptionMinScore;
        foreach (var card in ai.Hand.Where(c => c.Type == CardType.Option).Distinct())
        {
            int cost = CardScripts.EffectivePlayCost(ai, card, engine.State.Player);
            if (cost > limit || ActionValidator.MissingOptionColors(ai, card).Count > 0)
                continue;
            string main = AiBoard.MainPart(card.EffectText);
            if (!AiBoard.HasUsefulTarget(state, ai, main))
                continue;
            if (Tuning.SituationalOptions && (cost > memory || !AiBoard.OptionRemovesKeyThreat(state, ai, main)))
                continue;
            double value = Tuning.TargetAwareOptions ? AiBoard.OptionValue(state, ai, main) : AiBoard.TextValue(main);
            // 강화 효과는 어택 전에 써야 의미가 있다
            if (!beforeAttacks && value < 2.5)
                continue;
            double score = value - cost * Tuning.OptionCostWeight - Math.Max(0, cost - memory) * 0.4;
            if (score > bestScore)
            {
                bestScore = score;
                best = card;
            }
        }
        return best == null ? null : new AiOptionUse(best, null);
    }

    public AiAttack? ChooseNextAttack(GoldfishEngine engine, int memory)
    {
        if (!Tuning.SmartAttack)
            return Legacy.ChooseNextAttack(engine, memory);
        var state = engine.State;
        var ai = state.DummyOpponent;
        var attackers = ai.BattleArea.Where(engine.CanAttackThisTurn).ToList();
        if (attackers.Count == 0)
            return null;

        var targets = new List<CardInstance?> { null };
        targets.AddRange(state.Player.BattleArea.Where(EffectRules.CanBeAttackTarget));

        AiAttack? best = null;
        double bestScore = Tuning.AttackThreshold;
        foreach (var attacker in attackers)
        {
            // 혼자 남은 ≪블로커≫는 시큐리티를 지키도록 남겨 둔다 (다이렉트 어택 기회는 예외)
            bool loneBlocker = attacker.HasBlocker && ai.BattleArea.Count == 1;
            foreach (var target in targets)
            {
                double score = AiBoard.AttackScore(state, attacker, target, Tuning);
                if (loneBlocker && score < 1000)
                    score -= Tuning.LoneBlockerPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new AiAttack(attacker, target);
                }
            }
        }
        return best;
    }

    public bool ShouldUseMainAbility(GoldfishEngine engine, GoldfishEngine.MainAbility ability, int memory)
    {
        if (!Tuning.SmartMainAbility)
            return Legacy.ShouldUseMainAbility(engine, ability, memory);
        string text = AiBoard.MainPart(ability.Card.EffectText);
        return AiBoard.TextValue(text) > 0 && AiBoard.HasUsefulTarget(engine.State, engine.State.DummyOpponent, text);
    }

    public CardData? ChooseTamerToPlay(GoldfishEngine engine, int memory)
    {
        if (!Tuning.SmartPlay)
            return Legacy.ChooseTamerToPlay(engine, memory);
        var ai = engine.State.DummyOpponent;
        if (ai.Tamers.Count >= 2)
            return null;
        return ai.Hand
            .Where(c => c.Type == CardType.Tamer && CardScripts.EffectivePlayCost(ai, c, engine.State.Player) <= memory + MaxMemoryHandOver)
            .OrderBy(c => CardScripts.EffectivePlayCost(ai, c, engine.State.Player))
            .FirstOrDefault();
    }

    public CardData? ChooseDigimonToPlay(GoldfishEngine engine, int memory)
    {
        if (!Tuning.SmartPlay)
            return Legacy.ChooseDigimonToPlay(engine, memory);
        var state = engine.State;
        var ai = state.DummyOpponent;
        CardData? best = null;
        double bestScore = 0.5;
        foreach (var card in ai.Hand.Where(c => c.Type == CardType.Digimon).Distinct())
        {
            int cost = CardScripts.EffectivePlayCost(ai, card, engine.State.Player);
            if (cost > memory + MaxMemoryHandOver)
                continue;
            bool canGrow = ai.Hand.Any(h => h.Type == CardType.Digimon && h.EvoRequirementLevel == card.Level && h.EvoRequirementColor == card.Color);
            bool needWall = card.IsBlocker && state.Player.BattleArea.Count > 0 && ai.BattleArea.Count(d => d.HasBlocker) == 0;
            double score = card.DP / 1000.0 * 0.6 + card.Level * 0.5
                + (canGrow ? 1.5 : 0)
                + (needWall ? 2 : 0)
                + Math.Max(0, AiBoard.TextValue(AiBoard.MainPart(card.EffectText))) * 0.4
                - cost * 0.6
                - Math.Max(0, cost - memory) * 0.5;
            if (score > bestScore)
            {
                bestScore = score;
                best = card;
            }
        }
        return best;
    }

    /// <summary>
    /// DNA 진화: 두 마리를 겹쳐 더 센 한 마리(액티브 · 두 묶음의 진화원 · 1장 드로우)로 만든다.
    /// 이득 = 진화한 디지몬의 가치 − 두 마리 중 센 쪽의 가치 − 필드 한 자리를 잃는 손해(약한 쪽 가치의 일부) − 코스트.
    /// 어택 전이면 액티브로 바로 어택할 수 있는 이득을 더한다. 이득이 있는 조합 중 가장 좋은 것.
    /// </summary>
    public AiDnaEvolution? ChooseDnaEvolution(GoldfishEngine engine, int memory, bool beforeAttacks) =>
        BestDna(engine, memory, beforeAttacks, out _);

    private AiDnaEvolution? BestDna(GoldfishEngine engine, int memory, bool beforeAttacks, out double bestScore)
    {
        var ai = engine.State.DummyOpponent;
        int limit = beforeAttacks ? memory : memory + MaxMemoryHandOver;
        AiDnaEvolution? best = null;
        bestScore = 0.5;
        foreach (var card in ai.Hand.Where(c => c.Type == CardType.Digimon && GoldfishEngine.HasDna(c)).Distinct())
        {
            if (GoldfishEngine.GetDnaCost(card) is not int cost || cost > limit)
                continue;
            foreach (var (left, right) in engine.GetDnaPairsFor(ai, card))
            {
                double stronger = Math.Max(AiBoard.Value(left), AiBoard.Value(right));
                double weaker = Math.Min(AiBoard.Value(left), AiBoard.Value(right));
                int sources = left.EvolutionSources.Count + right.EvolutionSources.Count + 2;
                double result = card.DP / 1000.0 + card.Level + Math.Min(sources, 5) * 0.6 + (card.IsBlocker ? 1.5 : 0);
                double score = result - stronger
                    + 1.5                                                   // 1장 드로우
                    + (beforeAttacks ? 1.5 : 0)                             // 액티브 — 진화한 그대로 어택
                    + Math.Max(0, AiBoard.TextValue(AiBoard.MainPart(card.EffectText))) * 0.5
                    - weaker * 0.4                                          // 필드 한 자리를 잃는다
                    - cost * 0.35
                    - Math.Max(0, cost - memory) * 0.4;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new AiDnaEvolution(card, left, right);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// ≪볼텍스≫ · ≪에그제큐트≫: 대상(과 시큐리티)마다 어택 점수를 어림해 가장 좋은 것. ≪에그제큐트≫(canAttackSecurity)는
    /// 어택이 끝나면 이 디지몬이 소멸하므로 그 가치만큼 깎는다 — 사실상 다이렉트 어택 승리나 큰 이득일 때만 쓴다.
    /// </summary>
    public EndOfTurnAttack? ChooseEndOfTurnAttack(GoldfishEngine engine, CardInstance attacker, IReadOnlyList<CardInstance> targets, bool canAttackSecurity) =>
        BestExtraAttack(engine, attacker, targets, canAttackSecurity, selfCost: canAttackSecurity ? AiBoard.Value(attacker) : 0);   // ≪에그제큐트≫ = 어택 뒤 소멸

    /// <summary>효과 어택은 어택한 뒤 소멸하지 않는다 — 보통 어택과 같은 점수로 대상(또는 시큐리티)을 고른다</summary>
    public EndOfTurnAttack? ChooseEffectAttack(GoldfishEngine engine, CardInstance attacker, IReadOnlyList<CardInstance> targets, bool canAttackSecurity) =>
        BestExtraAttack(engine, attacker, targets, canAttackSecurity, selfCost: 0);

    private EndOfTurnAttack? BestExtraAttack(GoldfishEngine engine, CardInstance attacker, IReadOnlyList<CardInstance> targets, bool canAttackSecurity, double selfCost)
    {
        var state = engine.State;
        EndOfTurnAttack? best = null;
        double bestScore = Tuning.AttackThreshold;

        var options = targets.Select(t => (CardInstance?)t).ToList();
        if (canAttackSecurity)
            options.Add(null);
        foreach (var target in options)
        {
            double score = AiBoard.AttackScore(state, attacker, target, Tuning);
            // 배틀에서 이겨 살아남아도 ≪에그제큐트≫는 소멸한다 (배틀에서 지는 경우는 AttackScore가 이미 깎았다)
            bool survivesBattle = target == null || attacker.GetCurrentDP() > target.GetCurrentDP();
            if (survivesBattle && score < 1000)
                score -= selfCost;
            if (score > bestScore)
            {
                bestScore = score;
                best = new EndOfTurnAttack(target);
            }
        }
        return best;
    }
}

/// <summary>
/// 개선 AI의 점수 기준 · 켜고 끌 수 있는 판단 (승률 측정으로 조정한 값이 기본값).
/// Smart* · UseOptions를 끄면 그 결정만 기존 AI(Legacy)와 같아진다 — 어떤 판단이 승률에 도움이 되는지 나눠 잴 때 쓴다.
/// 조정 근거 (추천 덱 36조합 × 시드 20 = 720판, 같은 상대 봇): 기존 AI 56.5% → 처음 값 58.6% → 지금 값 66.8%.
/// 승률을 올린 것은 어택 판단(끄면 51.5%)과 공격적인 위험 기준이었고, 옵션을 자유롭게 쓰면 메모리를 상대에게 넘기는
/// 템포 손해로 3~5%p 낮아져 "꼭 필요할 때만"(SituationalOptions)으로 두었다.
/// </summary>
public sealed record SmartAiTuning
{
    /// <summary>이 점수를 넘는 어택만 한다</summary>
    public double AttackThreshold { get; init; } = -0.5;
    /// <summary>어택 뒤 레스트 상태로 상대의 더 센 디지몬에게 맞을 위험 (내 디지몬 가치에 곱함)</summary>
    public double ExposurePenalty { get; init; } = 0;
    /// <summary>시큐리티 체크에서 질 어림 확률의 배율 (1 = 기본표)</summary>
    public double DeathRiskScale { get; init; } = 0.5;
    /// <summary>시큐리티 1장 가치 = Base + (5 − 남은 장수) × Step</summary>
    public double SecurityBase { get; init; } = 3.0;
    public double SecurityStep { get; init; } = 0.8;
    /// <summary>혼자 남은 ≪블로커≫로 어택할 때 깎는 점수</summary>
    public double LoneBlockerPenalty { get; init; } = 2.5;

    public bool SmartAttack { get; init; } = true;
    public bool SmartEvolution { get; init; } = true;
    public bool SmartPlay { get; init; } = true;
    public bool SmartMainAbility { get; init; } = true;
    public bool UseOptions { get; init; } = true;
    /// <summary>옵션은 (효과 점수 − 코스트 비용)이 이 값을 넘을 때만 쓴다</summary>
    public double OptionMinScore { get; init; } = 0.5;
    /// <summary>옵션 코스트 1당 깎는 점수</summary>
    public double OptionCostWeight { get; init; } = 0.6;
    /// <summary>옵션 점수를 실제 대상(없앨 수 있는 상대 디지몬의 가치)으로 어림할지 (false = 문장 점수만)</summary>
    public bool TargetAwareOptions { get; init; } = false;
    /// <summary>
    /// 옵션은 꼭 필요할 때만: 어택을 막는 액티브 ≪블로커≫나 내 어느 디지몬보다 센 상대 디지몬을 없애는 제거 · 바운스이고,
    /// 지금 메모리 안에서 (상대에게 메모리를 넘기지 않고) 쓸 수 있을 때. 메모리를 상대에게 넘기는 템포 손해가 커서 이 기준이 승률에 낫다.
    /// </summary>
    public bool SituationalOptions { get; init; } = true;
}
