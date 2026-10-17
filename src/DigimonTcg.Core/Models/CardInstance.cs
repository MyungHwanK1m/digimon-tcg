using System;
using System.Collections.Generic;
using System.Linq;

namespace DigimonTcg.Core.Models;

public class CardInstance
{
    public string InstanceId { get; set; }
    public CardData TopCard { get; private set; }
    public Stack<CardData> EvolutionSources { get; private set; } = new Stack<CardData>();

    public bool IsSuspended { get; set; }          // Rest 상태 여부
    public int TurnPlayed { get; set; }            // 등장한 턴 (소환 멀미 체크용)
    public int TemporaryDpBonus { get; set; }      // 이번 턴 스킬/옵션 임시 DP 버프
    public int TemporarySecurityAttackBonus { get; set; } // 이번 턴 스킬/옵션 임시 시큐리티 어택 버프
    public int TamerDpBonus { get; set; }          // 필드 테이머 지속 DP 버프
    public int TamerSecurityAttackBonus { get; set; } // 필드 테이머 지속 시큐리티 어택 버프
    public bool HasUsedOncePerTurnEffect { get; set; }    // 턴당 1회 효과(메탈가루몬 재행동 등) 사용 여부
    public List<string> ActiveSkillBuffLabels { get; } = new(); // 이번 턴 적용된 스킬/옵션 버프 이름 목록

    public CardInstance(CardData baseCard, int currentTurn)
    {
        InstanceId = Guid.NewGuid().ToString();
        TopCard = baseCard;
        TurnPlayed = currentTurn;
    }

    public void Evolve(CardData newCard)
    {
        EvolutionSources.Push(TopCard);
        TopCard = newCard;

        // ST1-11 워그레이몬 등 [진화 시] 진화원 2장마다 시큐리티 어택 +1 처리
        if (newCard.CardId == "ST1-11")
        {
            int bonusChecks = EvolutionSources.Count / 2;
            if (bonusChecks > 0)
            {
                TemporarySecurityAttackBonus += bonusChecks;
                ActiveSkillBuffLabels.Add($"🔥워그레이몬 진화스킬 (시큐어택 +{bonusChecks})");
            }
        }
    }

    public int GetInheritedDpBonus() =>
        EvolutionSources.Sum(s => s.InheritedDpBonus);

    public int GetTotalBonusDP() =>
        GetInheritedDpBonus() + TamerDpBonus + TemporaryDpBonus;

    public int GetCurrentDP() =>
        TopCard.DP + GetTotalBonusDP();

    public int GetInheritedSecurityAttackBonus() =>
        EvolutionSources.Sum(s => s.InheritedSecurityAttackBonus);

    public int GetTotalBonusSecurityAttack() =>
        TopCard.SecurityAttackBonus + GetInheritedSecurityAttackBonus() + TamerSecurityAttackBonus + TemporarySecurityAttackBonus;

    public int GetSecurityAttackCount() =>
        Math.Max(0, 1 + GetTotalBonusSecurityAttack());

    /// <summary>
    /// 진화원 리스트 (아래에서 위로 쌓인 순서 반환 - UI 스택 표시용)
    /// </summary>
    public IReadOnlyList<CardData> GetEvolutionStackBottomUp()
    {
        return EvolutionSources.Reverse().ToList();
    }
}
