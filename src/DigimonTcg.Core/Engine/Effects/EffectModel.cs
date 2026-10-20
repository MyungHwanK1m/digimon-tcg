using System;
using System.Collections.Generic;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>효과가 발동하는 시점 (카드 텍스트의 【 】/[ ] 표기)</summary>
public enum EffectTrigger
{
    OnPlay,                         // [등장 시]
    WhenDigivolving,                // [진화 시]
    WhenAttacking,                  // [어택 시]
    OnDeletion,                     // [소멸 시]
    Main,                           // 옵션 [메인]
    Security,                       // [시큐리티]
    AttackBlocked,                  // 이 디지몬의 어택이 블록당했을 때
    DeletedOpponentInBattle,        // 배틀에서 상대 디지몬만 소멸시켰을 때
    OpponentDigimonDeletedByZeroDp, // 상대 디지몬이 DP 0이 되어 소멸했을 때
    AllySameNamePlayed,             // 같은 명칭의 다른 자신의 디지몬이 등장했을 때
    OpponentTurnEndWithoutAttack,   // [상대의 턴 종료 시] 이 턴에 상대가 어택하지 않았다면
}

/// <summary>효과가 적힌 곳 — 맨 위 카드(메인)인지, 진화원인지</summary>
public enum EffectSource { Main, Inherited }

/// <summary>규칙이 정해져 있어서 이름만으로 동작하는 키워드 효과</summary>
public enum Keyword
{
    Blocker,                 // ≪블로커≫
    Jamming,                 // ≪재밍≫: 시큐리티 디지몬과의 배틀로는 소멸하지 않는다
    Piercing,                // ≪관통≫: 배틀에서 상대 디지몬만 소멸시키면 시큐리티 체크
    Retaliation,             // ≪길동무≫: 배틀에서 소멸하면 상대 디지몬도 소멸
    Reboot,                  // ≪재기동≫: 상대의 액티브 페이즈에도 액티브
    UnblockableBySourceless, // 진화원을 갖지 않은 상대 디지몬에게는 블록당하지 않는다 (원뿔몬 BT1-034)
    NegateSecurityOptions,   // 체크한 옵션 카드의 [시큐리티] 효과는 발휘하지 않는다 (워그레이몬 BT1-025)
}

/// <summary>지속 효과가 바꾸는 수치</summary>
public enum StaticStat
{
    Dp,                 // 이 디지몬의 DP (진화원 · 자기 자신)
    DpInBattle,         // 배틀하고 있는 동안만 더하는 DP (StaticContext.BattleOpponentHasSources로 상대 조건 판단)
    SecurityAttack,     // 이 디지몬의 ≪시큐리티 어택 +N≫
    AllyDigimonDp,      // 테이머: 자신의 디지몬 전부의 DP
    SecurityDigimonDp,  // 테이머: 자신의 시큐리티 디지몬의 DP
}

/// <summary>옵션 [메인] 효과를 쓸 때 UI가 고르게 할 대상</summary>
public enum TargetKind { None, OwnDigimon, OwnDigimonWithDigimonSource, OpponentDigimon }

/// <summary>
/// 카드 효과 한 절: 발동 시점 + 출처 + (조건) + 처리.
/// 같은 시점에 여러 효과가 발동하면 메인 효과 → 진화원 효과(아래에서 위 순서)로 처리한다.
/// </summary>
public sealed record CardEffect(EffectTrigger Trigger, EffectSource Source, Action<EffectContext> Resolve)
{
    /// <summary>발동 조건 (없으면 항상). [턴에 1회]는 조건을 만족했을 때만 사용 기록을 남긴다.</summary>
    public Func<EffectContext, bool>? Condition { get; init; }

    /// <summary>[턴에 1회] — 효과를 가진 디지몬마다 카드 번호 기준으로 기록 (CardInstance.UsedOncePerTurn)</summary>
    public bool OncePerTurn { get; init; }
}

/// <summary>지속 효과 한 절 — 필드 상태가 바뀔 때마다 엔진이 다시 계산한다</summary>
public sealed record StaticEffect(StaticStat Stat, EffectSource Source, Func<StaticContext, int> Value);

/// <summary>지속 효과 계산에 필요한 정보</summary>
public sealed class StaticContext
{
    public required GameState State { get; init; }
    public required PlayerZones Owner { get; init; }
    public required PlayerZones Enemy { get; init; }

    /// <summary>효과를 가진 디지몬 (테이머 효과면 효과를 받는 디지몬)</summary>
    public CardInstance? Digimon { get; init; }

    /// <summary>지금이 효과 주인의 턴인지 ([자신의 턴] / [상대의 턴] 효과)</summary>
    public bool OwnersTurn { get; init; }

    /// <summary>DpInBattle 계산 때: 배틀 상대가 진화원을 가졌는지 (시큐리티 디지몬은 진화원이 없다)</summary>
    public bool BattleOpponentHasSources { get; init; }
}

/// <summary>[자신의 턴 개시 시] 메모리 보정 (테이머) — 상대 필드가 Condition을 만족하면 장마다 Amount. Reason은 로그 표시용</summary>
public sealed record StartOfTurnMemoryEffect(int Amount, Func<PlayerZones, bool> Condition, string Reason);

/// <summary>카드 한 장의 효과 정의 전부</summary>
public sealed class CardScript
{
    public CardScript(string cardId) => CardId = cardId;

    public string CardId { get; }
    public List<CardEffect> Effects { get; } = new();
    public List<StaticEffect> Statics { get; } = new();
    public HashSet<Keyword> Keywords { get; } = new();
    public HashSet<Keyword> InheritedKeywords { get; } = new();

    /// <summary>옵션 [메인] 효과의 대상 종류 (UI 대상 선택)</summary>
    public TargetKind MainTarget { get; set; }

    /// <summary>카드에 적힌 다른 진화 조건 (예: 오메가몬 "Lv.6 레드 · 블루에서 진화") — 진화 대상 디지몬을 받아 진화할 수 있는지</summary>
    public Func<CardInstance, bool>? AlternativeEvolution { get; set; }

    public StartOfTurnMemoryEffect? StartOfTurnMemory { get; set; }
}
