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
    public string Name => "개선 AI (한 수 앞 평가)";
    public int MaxMemoryHandOver => 3;

    /// <summary>이 점수를 넘는 어택만 한다 (작은 이득을 위해 디지몬을 위험에 빠뜨리지 않는다)</summary>
    private const double AttackThreshold = 0.25;

    public AiEvolution? ChooseEvolution(GoldfishEngine engine, int memory, bool beforeAttacks)
    {
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
                int cost = ActionValidator.EvolutionCost(field, card);
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
        return best;
    }

    public AiOptionUse? ChooseOption(GoldfishEngine engine, int memory, bool beforeAttacks)
    {
        var state = engine.State;
        var ai = state.DummyOpponent;
        int limit = beforeAttacks ? memory : memory + MaxMemoryHandOver;
        CardData? best = null;
        double bestScore = 0.5;
        foreach (var card in ai.Hand.Where(c => c.Type == CardType.Option).Distinct())
        {
            int cost = CardScripts.EffectivePlayCost(ai, card);
            if (cost > limit || ActionValidator.MissingOptionColors(ai, card).Count > 0)
                continue;
            string main = AiBoard.MainPart(card.EffectText);
            if (!AiBoard.HasUsefulTarget(state, ai, main))
                continue;
            double value = AiBoard.TextValue(main);
            // 강화 효과는 어택 전에 써야 의미가 있다
            if (!beforeAttacks && value < 2.5)
                continue;
            double score = value - cost * 0.6 - Math.Max(0, cost - memory) * 0.4;
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
        var state = engine.State;
        var ai = state.DummyOpponent;
        var attackers = ai.BattleArea.Where(engine.CanAttackThisTurn).ToList();
        if (attackers.Count == 0)
            return null;

        var targets = new List<CardInstance?> { null };
        targets.AddRange(state.Player.BattleArea.Where(d => d.IsSuspended));

        AiAttack? best = null;
        double bestScore = AttackThreshold;
        foreach (var attacker in attackers)
        {
            // 혼자 남은 ≪블로커≫는 시큐리티를 지키도록 남겨 둔다 (다이렉트 어택 기회는 예외)
            bool loneBlocker = attacker.HasBlocker && ai.BattleArea.Count == 1;
            foreach (var target in targets)
            {
                double score = AiBoard.AttackScore(state, attacker, target);
                if (loneBlocker && score < 1000)
                    score -= 2.5;
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
        string text = AiBoard.MainPart(ability.Card.EffectText);
        return AiBoard.TextValue(text) > 0 && AiBoard.HasUsefulTarget(engine.State, engine.State.DummyOpponent, text);
    }

    public CardData? ChooseTamerToPlay(GoldfishEngine engine, int memory)
    {
        var ai = engine.State.DummyOpponent;
        if (ai.Tamers.Count >= 2)
            return null;
        return ai.Hand
            .Where(c => c.Type == CardType.Tamer && CardScripts.EffectivePlayCost(ai, c) <= memory + MaxMemoryHandOver)
            .OrderBy(c => CardScripts.EffectivePlayCost(ai, c))
            .FirstOrDefault();
    }

    public CardData? ChooseDigimonToPlay(GoldfishEngine engine, int memory)
    {
        var state = engine.State;
        var ai = state.DummyOpponent;
        CardData? best = null;
        double bestScore = 0.5;
        foreach (var card in ai.Hand.Where(c => c.Type == CardType.Digimon).Distinct())
        {
            int cost = CardScripts.EffectivePlayCost(ai, card);
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
}
