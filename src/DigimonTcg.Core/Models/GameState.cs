using System.Collections.Generic;

namespace DigimonTcg.Core.Models;

public record SecurityCheckHistoryItem(
    CardData SecurityCard,
    string AttackerName,
    int AttackerDp,
    bool IsBattleWon,
    string OutcomeSummary = ""
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
}

public class GameState
{
    public PracticeStage CurrentStage { get; set; } = PracticeStage.Stage1_Goldfish;
    public int CurrentTurn { get; set; } = 1;
    public Phase CurrentPhase { get; set; } = Phase.Active;

    // 메모리 위치: -10 (상대 우세) ~ +10 (플레이어 우세)
    public int MemoryGauge { get; set; } = 0;

    // 코인 토스 결과: 플레이어 선공 여부 (선공은 1턴째 드로우 없음)
    public bool PlayerGoesFirst { get; set; } = true;

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

    public PlayerZones Player { get; set; } = new PlayerZones();
    public PlayerZones DummyOpponent { get; set; } = new PlayerZones();
    public string OpponentDeckName { get; set; } = "🤖 자동 카운터 덱";

    public double MemoryEfficiencyPerTurn =>
        CurrentTurn > 0 ? (double)TotalMemorySpent / CurrentTurn : 0.0;

    public double EvolutionChainSuccessRate =>
        Lv3StartedChains > 0 ? (double)Lv6CompletedChains / Lv3StartedChains * 100.0 : 0.0;
}
