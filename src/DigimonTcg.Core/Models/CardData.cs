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
    public string Rarity { get; set; } = string.Empty;              // 카드 등급: C / U / R / SR / SEC / P (API 동기화로 채워짐)

    // 대전 엔진이 이 카드의 효과를 구현했는지 여부 (내장 한글 카드 = true, API로 받은 카드 = false)
    // 미구현 카드는 대전에서 DP · 코스트 · 진화 조건과 자동 인식 키워드(블로커 / 시큐리티 어택)만 적용된다.
    // 카드 번호로 판별하므로 덱 프리셋 · 손패 · 필드에서 새로 만든 카드 객체도 같은 결과가 나온다.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEffectImplemented => DigimonTcg.Core.Data.StarterDecks.IsBuiltInCard(CardId);

    /// <summary>내장 카드지만 효과 문장이 아직 대전에서 동작하지 않는 카드</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasUnimplementedBuiltInEffect => IsEffectImplemented && DigimonTcg.Core.Data.StarterDecks.IsTextOnlyEffectCard(CardId);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAutoKeywords => !IsEffectImplemented && (IsBlocker || SecurityAttackBonus > 0 || InheritedSecurityAttackBonus > 0);

    public string DisplaySummary =>
        Type == CardType.Digitama
            ? $"[{CardId}] {Name} (Lv.{Level} 디지타마)"
            : Type == CardType.Digimon
                ? $"[{CardId}] {Name} (Lv.{Level} | DP {DP} | 등장 {PlayCost} / 진화 {EvoCost})"
                : $"[{CardId}] {Name} ({Type} | 코스트 {PlayCost})";
}
