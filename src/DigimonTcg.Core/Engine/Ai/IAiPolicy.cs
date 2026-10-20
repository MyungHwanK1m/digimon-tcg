using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Ai;

/// <summary>AI 진화 한 번: 필드(배틀 · 육성 에어리어)의 Base 위에 패의 Card로 진화</summary>
public readonly record struct AiEvolution(CardInstance Base, CardData Card);

/// <summary>AI 어택 한 번: Target이 null이면 상대 플레이어(시큐리티), 아니면 상대의 레스트 디지몬</summary>
public readonly record struct AiAttack(CardInstance Attacker, CardInstance? Target);

/// <summary>AI 옵션 사용 한 번: Target이 null이면 엔진이 대상을 고른다 (AI 평가 순서)</summary>
public readonly record struct AiOptionUse(CardData Card, CardInstance? Target);

/// <summary>
/// AI(State.DummyOpponent)의 턴 중 결정 — 엔진의 AI 턴은 각 결정 지점에서 이 정책에 묻는다.
/// 모든 메서드는 상태를 바꾸지 않는다 (실제 처리는 엔진이 한다). memory = 지금 AI 쪽 메모리 (음수 = 이미 상대 쪽).
/// - LegacyAiPolicy: 정책을 분리하기 전 AI와 같은 결정 (승률 비교 기준)
/// - SmartAiPolicy: 한 수 앞 평가 · 옵션 사용 · [메인] 기동 판단
/// </summary>
public interface IAiPolicy
{
    string Name { get; }

    /// <summary>턴을 넘길 때 상대에게 넘겨도 되는 메모리 상한 (비싼 카드를 무리하게 내지 않는다)</summary>
    int MaxMemoryHandOver { get; }

    /// <summary>다음 진화. beforeAttacks면 어택 전 (메모리를 상대 쪽으로 넘기지 않는 진화만). null = 진화하지 않음</summary>
    AiEvolution? ChooseEvolution(GoldfishEngine engine, int memory, bool beforeAttacks);

    /// <summary>다음에 쓸 옵션 카드 (메인 효과). null = 쓰지 않음</summary>
    AiOptionUse? ChooseOption(GoldfishEngine engine, int memory, bool beforeAttacks);

    /// <summary>다음 어택. 엔진은 어택 하나를 처리할 때마다 다시 묻는다 (결과로 필드가 바뀌므로). null = 어택 끝</summary>
    AiAttack? ChooseNextAttack(GoldfishEngine engine, int memory);

    /// <summary>테이머 · 디지몬의 [메인] 기동 효과를 쓸지</summary>
    bool ShouldUseMainAbility(GoldfishEngine engine, GoldfishEngine.MainAbility ability, int memory);

    /// <summary>패에서 낼 테이머. null = 내지 않음</summary>
    CardData? ChooseTamerToPlay(GoldfishEngine engine, int memory);

    /// <summary>패에서 낼 디지몬. null = 내지 않음</summary>
    CardData? ChooseDigimonToPlay(GoldfishEngine engine, int memory);
}
