using System.Collections.Generic;

namespace DigimonTcg.Core.Models;

public record SecurityCheckHistoryItem(
    CardData SecurityCard,
    string AttackerName,
    int AttackerDp,
    bool IsBattleWon,
    string OutcomeSummary = ""
);

/// <summary>상대(AI) 턴에 일어난 행동 1건 — UI가 상대 턴을 한 장면씩 다시 보여 주는 데 사용한다.</summary>
public enum OpponentActionKind { TurnStart, Breeding, Evolve, Play, Tamer, Attack, Block, SecurityResult, DirectAttack, TurnEnd, Effect }

public record OpponentActionEvent(
    OpponentActionKind Kind,
    string Title,
    string Detail,
    CardData? Card = null,
    CardData? SubCard = null
);

/// <summary>AI의 어택에 대해 플레이어가 블록할지(어느 블로커로 막을지) 고를 차례 — 공식 룰상 블록은 방어 측의 선택이다.</summary>
public record PendingBlockDecision(
    CardInstance Attacker,
    IReadOnlyList<CardInstance> Candidates,
    int AttackerDp,
    int SecurityChecks
);

public class PlayerZones
{
    public Stack<CardData> DigitamaDeck { get; set; } = new Stack<CardData>();
    public List<CardData> MainDeck { get; set; } = new List<CardData>();
    public List<CardData> Hand { get; set; } = new List<CardData>();
    public List<CardData> SecurityStack { get; set; } = new List<CardData>();
    public List<CardData> Trash { get; set; } = new List<CardData>();

    public CardInstance? BreedingArea { get; set; }
    public List<CardInstance> BattleArea { get; set; } = new List<CardInstance>();
    public List<CardData> Tamers { get; set; } = new List<CardData>();

    // 레스트 상태인 테이머 (같은 카드 객체가 여러 장일 수 있어 장 수로 센다) — 자신의 액티브 페이즈에 비운다
    public List<CardData> SuspendedTamers { get; set; } = new List<CardData>();

    /// <summary>이 테이머 카드 중 액티브 상태인 장이 있는지</summary>
    public bool HasActiveTamer(CardData tamer) =>
        Tamers.Count(t => ReferenceEquals(t, tamer)) > SuspendedTamers.Count(t => ReferenceEquals(t, tamer));
}

public class GameState
{
    public PracticeStage CurrentStage { get; set; } = PracticeStage.Stage1_Goldfish;
    public int CurrentTurn { get; set; } = 1;
    public Phase CurrentPhase { get; set; } = Phase.Active;

    // 메모리 위치: -10 (상대 우세) ~ +10 (플레이어 우세)
    // 공식 룰 1-4-2-2: 메모리는 양쪽 모두 10을 넘지 않는다 (10을 넘는 코스트도 지불할 수 있고, 마커는 10에서 멈춘다)
    private int _memoryGauge;
    public int MemoryGauge
    {
        get => _memoryGauge;
        set => _memoryGauge = Math.Clamp(value, -10, 10);
    }

    // 코인 토스 결과: 플레이어 선공 여부 (선공은 1턴째 드로우 없음)
    public bool PlayerGoesFirst { get; set; } = true;

    // 턴 순번: 플레이어 턴 · 상대 턴이 시작될 때마다 1씩 증가 ("다음 상대의 턴 종료 시까지" 같은 기간 효과 기준)
    public int TurnSequence { get; set; }

    // "이 턴 종료 시, 메모리를 -3 한다" (메탈그레이몬 BT1-021 등) — 턴 종료 시 상대에게 추가로 넘어가는 메모리
    public int PendingEndOfTurnMemoryLoss { get; set; }

    // 시큐리티 디지몬 DP 버프 (스타라이트 익스플로전 · 천국의 문 등): (DP, 유효한 마지막 TurnSequence)
    public List<(int Dp, int Until)> PlayerSecurityDpBuffs { get; set; } = new();
    public List<(int Dp, int Until)> OpponentSecurityDpBuffs { get; set; } = new();

    // 섀도 윙 [시큐리티]: "다음 자신의 턴 종료 시까지 자신의 디지몬 전부 ≪시큐리티 어택 +1≫" (유효한 마지막 TurnSequence)
    public int PlayerAllSecurityAttackUntil { get; set; }
    public int OpponentAllSecurityAttackUntil { get; set; }

    // 이번 상대 턴에 상대 디지몬이 어택했는지 (토이아구몬 ST5-04 등 [상대의 턴 종료 시] 효과용)
    public bool OpponentAttackedThisTurn { get; set; }

    // 1턴 시작 시 멀리건(손패 1회 다시 뽑기) 가능 여부
    public bool CanMulligan { get; set; } = true;

    // 육성 페이즈 액션 수행 여부 (턴당 1회: 부화 / 이동 / 패스 중 택 1)
    public bool BreedingActionTaken { get; set; }

    // 상대 시큐리티 체크 이력 및 내 시큐리티 피격 이력
    public List<SecurityCheckHistoryItem> SecurityCheckHistory { get; set; } = new List<SecurityCheckHistoryItem>();
    public List<SecurityCheckHistoryItem> PlayerSecurityCheckHistory { get; set; } = new List<SecurityCheckHistoryItem>();
    public CardData? LastRevealedSecurityCard { get; set; }
    public string LastSecurityBattleResult { get; set; } = string.Empty;
    public bool LastSecurityBattleWon { get; set; } = true;

    // 게임 종료 및 타임어택 평가 지표 (HUD용)
    public int TargetTurns { get; set; } = 5;
    public bool IsGameOver { get; set; }
    public bool IsVictory { get; set; }
    public string GameOverReason { get; set; } = string.Empty;

    public int TotalMemorySpent { get; set; }
    public int TotalEvolutionsCount { get; set; }
    public int Lv3StartedChains { get; set; }
    public int Lv6CompletedChains { get; set; }
    public List<string> ActionLogs { get; set; } = new List<string>();

    // 직전 상대 턴의 행동 목록 (UI가 리플레이 후 비운다)
    public List<OpponentActionEvent> OpponentTurnEvents { get; set; } = new List<OpponentActionEvent>();

    // AI 턴이 플레이어의 블록 선택을 기다리며 멈춰 있는 상태 (null = 대기 없음)
    public PendingBlockDecision? PendingBlock { get; set; }

    // ≪진격≫으로 어택할 수 있게 된 내 디지몬 — 메모리가 상대 쪽이어도 턴이 끝나지 않고 이 디지몬의 어택 1번만 할 수 있다
    public CardInstance? BlitzAttacker { get; set; }

    public PlayerZones Player { get; set; } = new PlayerZones();
    public PlayerZones DummyOpponent { get; set; } = new PlayerZones();
    public string OpponentDeckName { get; set; } = "🤖 자동 카운터 덱";

    public double MemoryEfficiencyPerTurn =>
        CurrentTurn > 0 ? (double)TotalMemorySpent / CurrentTurn : 0.0;

    public double EvolutionChainSuccessRate =>
        Lv3StartedChains > 0 ? (double)Lv6CompletedChains / Lv3StartedChains * 100.0 : 0.0;
}
