namespace DigimonTcg.Core.Models;

public enum CardType
{
    Digitama,
    Digimon,
    Tamer,
    Option
}

public enum CardColor
{
    Red,
    Blue,
    Yellow,
    Green,
    Black,
    Purple,
    White
}

public enum Phase
{
    Active,
    Draw,
    Breeding,
    Main,
    TurnEnd,
    Setup      // 게임 준비 (손패 5장 → 멀리건 → 시큐리티 5장 전, 공식 룰 5-2)
}

public enum PracticeStage
{
    Stage1_Goldfish,       // 1단계: 기본 타임어택 (상대 무방비, +3 메모리 고정)
    Stage2_BlockerDefense, // 2단계: 블로커 & 수비진 돌파 챌린지 (상대 필드 블로커/디지몬 배치)
    Stage3_AiBattle        // 3단계: 실전 AI 대전 (메모리 줄다리기 & AI 공격/전개 공방전)
}
