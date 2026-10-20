using System;
using System.Collections.Generic;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 효과 하나를 처리할 때 넘겨받는 정보와 공용 동작.
/// Owner / Enemy는 효과 주인 기준이므로 같은 스크립트가 플레이어 · AI 어느 쪽에서도 그대로 동작한다.
/// </summary>
public sealed class EffectContext
{
    internal EffectContext(GoldfishEngine engine, PlayerZones owner, CardInstance? self, CardInstance? other, CardInstance? chosenTarget)
    {
        Engine = engine;
        Owner = owner;
        Enemy = engine.EnemyOf(owner);
        Self = self;
        Other = other;
        ChosenTarget = chosenTarget;
    }

    internal GoldfishEngine Engine { get; }
    public GameState State => Engine.State;

    /// <summary>효과 주인의 필드</summary>
    public PlayerZones Owner { get; }
    public PlayerZones Enemy { get; }
    public bool OwnerIsPlayer => ReferenceEquals(Owner, State.Player);

    /// <summary>지금 처리 중인 효과가 적힌 카드 (메인 효과면 맨 위 카드 / 옵션, 진화원 효과면 그 진화원)</summary>
    public CardData Card { get; internal set; } = null!;

    /// <summary>효과를 가진 디지몬 (옵션 · 테이머 효과면 null)</summary>
    public CardInstance? Self { get; }

    /// <summary>
    /// 시점별 상대 카드: [어택 시] = 어택 대상 디지몬(시큐리티 어택이면 null),
    /// [시큐리티] = 어택 중인 상대 디지몬, 같은 명칭 등장 = 등장한 디지몬
    /// </summary>
    public CardInstance? Other { get; }

    /// <summary>플레이어가 UI에서 고른 대상 (옵션 [메인])</summary>
    public CardInstance? ChosenTarget { get; }

    /// <summary>GameState.TurnSequence — "다음 상대의 턴 종료 시까지" = TurnSequence + 1</summary>
    public int TurnSequence => State.TurnSequence;

    // ---- 처리 결과 (엔진이 로그 · 결과 메시지에 쓴다) ----

    /// <summary>[등장 시] 요약 / 옵션 [메인] 결과 메시지 / [시큐리티] 상세</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>[시큐리티] 결과 한 줄 요약 (시큐리티 체크 기록)</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>[시큐리티] 효과로 어택 중인 디지몬이 필드를 떠났는지</summary>
    public bool AttackerRemoved { get; set; }

    /// <summary>[시큐리티] 효과를 발휘한 이 카드를 트래시 대신 패에 넣는지</summary>
    public bool ReturnToHand { get; set; }

    /// <summary>[시큐리티] 결과를 한 번에 정한다</summary>
    public void SecurityResult(string detail, string summary, bool attackerRemoved = false, bool returnToHand = false)
    {
        Message = detail;
        Summary = summary;
        AttackerRemoved = attackerRemoved;
        ReturnToHand = returnToHand;
    }

    // ---- 공용 동작 ----

    public void Log(string message) => Engine.EffectLog(Owner, message);

    /// <summary>효과 주인 기준 메모리 (AI 턴에는 AI 메모리)</summary>
    public int OwnerMemory => Engine.MemoryOf(Owner);

    public void AddMemory(int amount) => Engine.AddMemory(Owner, amount);

    /// <summary>[이 턴 종료 시] 메모리 -N 예약</summary>
    public void LoseMemoryAtEndOfTurn(int amount) => Engine.AddEndOfTurnMemoryLoss(Owner, amount);

    public CardData? Draw() => Engine.DrawFor(Owner);

    /// <summary>덱 위에서 count장을 오픈해 조건에 맞는 카드를 최대 maxTake장 패에 넣고, 나머지는 덱 아래로</summary>
    public List<CardData> RevealToHand(int count, Func<CardData, bool> match, int maxTake) =>
        Engine.RevealAndTake(Owner, count, match, maxTake);

    /// <summary>≪리커버리 +N (덱)≫</summary>
    public int Recovery(int count) => Engine.Recovery(Owner, count);

    /// <summary>자신의 덱 위에서 N장 파기</summary>
    public void Mill(int count, string reason) => Engine.Mill(Owner, count, reason);

    /// <summary>자신의 트래시에서 조건에 맞는 디지몬을 코스트 없이 등장 ([등장 시] 효과는 발휘하지 않음)</summary>
    public List<CardData> PlayFromTrash(Func<CardData, bool> match, int maxCount) =>
        Engine.PlayFromTrash(Owner, match, maxCount);

    /// <summary>카드를 코스트 없이 등장시킨다 ([등장 시] 효과는 발휘하지 않음)</summary>
    public CardInstance PlayWithoutCost(CardData card)
    {
        var inst = new CardInstance(card, State.CurrentTurn);
        Owner.BattleArea.Add(inst);
        return inst;
    }

    /// <summary>상대 디지몬을 효과로 소멸시킨다 (≪회피≫로 막으면 false)</summary>
    public bool DeleteEnemy(CardInstance target) => Engine.DeleteFrom(Enemy, target);
    /// <summary>자신의 디지몬을 소멸시킨다 (코스트 — ≪회피≫ 등으로 막을 수 없음)</summary>
    public void DeleteOwn(CardInstance target) => Engine.DeleteFrom(Owner, target, GoldfishEngine.DeletionCause.Cost);

    /// <summary>상대 디지몬을 패로 되돌린다 (진화원은 트래시)</summary>
    public void BounceEnemy(CardInstance target) => Engine.ReturnToHand(Enemy, target);

    /// <summary>DP -N (이 턴 동안) — DP가 0이 되면 소멸. 반환: 소멸 여부</summary>
    public bool DpMinus(CardInstance target, int amount, string source) => Engine.ApplyDpMinus(target, amount, source);

    /// <summary>상대 디지몬 ≪퇴화 N≫ — 버린 매수</summary>
    public int DeDigivolveEnemy(CardInstance target, int count) => Engine.DeDigivolve(Enemy, target, count);

    /// <summary>상대 디지몬 중 대상 고르기: preferred가 조건에 맞으면 그것, 아니면 DP가 가장 높은 것</summary>
    public CardInstance? PickEnemy(Func<CardInstance, bool>? match = null, CardInstance? preferred = null) =>
        GoldfishEngine.PickTarget(Enemy.BattleArea, preferred, match);

    public CardInstance? PickOwn(Func<CardInstance, bool>? match = null, CardInstance? preferred = null) =>
        GoldfishEngine.PickTarget(Owner.BattleArea, preferred, match);

    /// <summary>상대 디지몬 1마리의 진화원을 아래에서 1장 파기 (가장 레벨이 높은 대상)</summary>
    public void TrashBottomSourceOfEnemy(Func<CardInstance, bool> match, string source) =>
        Engine.TrashBottomSourceOfEnemy(Enemy, match, source);

    public void RecalculateFieldBuffs() => Engine.RecalculateFieldBuffs();
}
