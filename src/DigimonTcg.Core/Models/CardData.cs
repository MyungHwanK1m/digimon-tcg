namespace DigimonTcg.Core.Models;

public class CardData
{
    public string CardId { get; set; } = string.Empty;              // 예: "ST1-03"
    public string Name { get; set; } = string.Empty;                // 예: "아구몬"
    public CardType Type { get; set; }
    public CardColor Color { get; set; }
    public int Level { get; set; }                                  // Lv.2 ~ Lv.7 ( 테이머/옵션은 0 )
    public int PlayCost { get; set; }                               // 등장 코스트
    public int EvoCost { get; set; }                                // 진화 코스트
    public int EvoRequirementLevel { get; set; }                    // 필요 진화 전 레벨
    public CardColor EvoRequirementColor { get; set; }
    public int DP { get; set; }                                     // 기본 전투력
    public string EffectText { get; set; } = string.Empty;          // 메인 효과 설명
    public string InheritedEffectText { get; set; } = string.Empty; // 진화원 효과 설명

    // 인게임 자동 계산을 위한 보조 수치
    public bool IsBlocker { get; set; }                             // ≪블로커≫ 보유 여부
    public int InheritedDpBonus { get; set; }                       // 진화원 DP 버프 (예: +1000)
    public int SecurityAttackBonus { get; set; }                    // 기본 시큐리티 어택 추가 수치
    public int InheritedSecurityAttackBonus { get; set; }           // 진화원 시큐리티 어택 추가 수치
    public string ImageUrl { get; set; } = string.Empty;            // 카드 이미지 URL (API 연동용)

    public string DisplaySummary =>
        Type == CardType.Digitama
            ? $"[{CardId}] {Name} (Lv.{Level} 디지타마)"
            : Type == CardType.Digimon
                ? $"[{CardId}] {Name} (Lv.{Level} | DP {DP} | 등장 {PlayCost} / 진화 {EvoCost})"
                : $"[{CardId}] {Name} ({Type} | 코스트 {PlayCost})";
}
