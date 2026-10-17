using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine;

public record ActionResult
{
    public bool CanExecute { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string GuideMessage { get; init; } = string.Empty;

    public static ActionResult Success(string guide) =>
        new ActionResult { CanExecute = true, Reason = string.Empty, GuideMessage = guide };

    public static ActionResult Fail(string reason) =>
        new ActionResult { CanExecute = false, Reason = reason, GuideMessage = string.Empty };
}

public static class ActionValidator
{
    // 1. 등장 유효성 검사
    public static ActionResult ValidatePlay(GameState state, CardData card)
    {
        if (state.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (state.CurrentPhase != Phase.Main)
            return ActionResult.Fail("메인 페이즈에만 카드를 등장시킬 수 있습니다.");

        if (card.Type == CardType.Digitama)
            return ActionResult.Fail("디지타마 카드는 패에서 등장시킬 수 없으며 육성 페이즈에 부화해야 합니다.");

        int projectedMemory = state.MemoryGauge - card.PlayCost;
        if (projectedMemory < -10)
            return ActionResult.Fail($"메모리가 -10 미만이 되는 코스트({card.PlayCost})는 지불할 수 없습니다.");

        string guide = $"[{card.Name}] 등장 코스트 {card.PlayCost}를 사용합니다.";

        if (projectedMemory < 0)
            guide += $" (주의: 메모리가 {projectedMemory}가 되어 카드 처리 후 턴이 종료됩니다)";
        else
            guide += $" (남은 메모리: {projectedMemory})";

        return ActionResult.Success(guide);
    }

    // 2. 진화 유효성 검사 (명세서 기본 시그니처 유지)
    public static ActionResult ValidateEvolution(CardInstance? target, CardData evoCard)
    {
        if (target == null)
            return ActionResult.Fail("진화 대상 디지몬이 선택되지 않았습니다.");

        if (evoCard.Type != CardType.Digimon)
            return ActionResult.Fail("디지몬 카드만 진화에 사용할 수 있습니다.");

        if (target.TopCard.Level != evoCard.EvoRequirementLevel)
            return ActionResult.Fail($"Lv.{evoCard.EvoRequirementLevel} 디지몬 위에만 진화할 수 있습니다. (현재 대상: Lv.{target.TopCard.Level})");

        if (target.TopCard.Color != evoCard.EvoRequirementColor)
            return ActionResult.Fail($"색상이 일치하지 않습니다. ({evoCard.EvoRequirementColor} 필요)");

        string guide = $"진화 코스트 {evoCard.EvoCost} 지불. 진화 성공 시 카드 1장을 추가로 드로우합니다.";
        return ActionResult.Success(guide);
    }

    // 2-1. 진화 유효성 검사 (GameState 포함 확장 오버로드)
    public static ActionResult ValidateEvolution(GameState state, CardInstance? target, CardData evoCard)
    {
        if (state.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (state.CurrentPhase != Phase.Main)
            return ActionResult.Fail("메인 페이즈에만 진화할 수 있습니다.");

        var baseCheck = ValidateEvolution(target, evoCard);
        if (!baseCheck.CanExecute)
            return baseCheck;

        int projectedMemory = state.MemoryGauge - evoCard.EvoCost;
        if (projectedMemory < -10)
            return ActionResult.Fail($"메모리가 -10 미만이 되는 진화 코스트({evoCard.EvoCost})는 지불할 수 없습니다.");

        string guide = baseCheck.GuideMessage;
        if (projectedMemory < 0)
            guide += $" (주의: 메모리가 {projectedMemory}가 되어 진화 및 1드로우 후 턴이 종료됩니다)";

        return ActionResult.Success(guide);
    }

    // 3. 공격 유효성 검사
    public static ActionResult ValidateAttack(GameState state, CardInstance? attacker, CardInstance? defenderTarget = null)
    {
        if (state.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (state.CurrentPhase != Phase.Main)
            return ActionResult.Fail("메인 페이즈에만 공격할 수 있습니다.");

        if (attacker == null)
            return ActionResult.Fail("공격할 디지몬을 선택해주세요.");

        if (state.Player.BreedingArea == attacker)
            return ActionResult.Fail("육성 에어리어의 디지몬은 공격할 수 없습니다. 배틀 에어리어로 이동 후 공격하세요.");

        if (attacker.IsSuspended)
            return ActionResult.Fail("이미 레스트(Rest) 상태인 디지몬은 공격할 수 없습니다.");

        if (attacker.TurnPlayed == state.CurrentTurn)
            return ActionResult.Fail("이번 턴에 등장한 디지몬은 소환 멀미로 인해 공격할 수 없습니다. (진화한 경우에도 등장 턴에는 공격 불가)");

        if (defenderTarget != null && !defenderTarget.IsSuspended)
            return ActionResult.Fail("상대의 스탠드(Active) 상태 디지몬은 공격할 수 없습니다.");

        string guide;
        if (defenderTarget == null)
        {
            var activeBlocker = state.DummyOpponent.BattleArea.Find(d => d.TopCard.IsBlocker && !d.IsSuspended);
            if (activeBlocker != null)
            {
                int myDp = attacker.GetCurrentDP();
                int blkDp = activeBlocker.GetCurrentDP();
                string blkOutcome = myDp > blkDp
                    ? $"✅ 블로커 격파 가능! (내 DP {myDp} > 블로커 DP {blkDp})"
                    : $"⚠️ 주의: 블로커에게 막혀 내 디지몬이 소멸합니다! (내 DP {myDp} <= 블로커 DP {blkDp})";

                guide = $"🛡️ 상대 필드에 액티브 ≪블로커≫ [{activeBlocker.TopCard.Name}(DP {blkDp})]가 있어 시큐리티 공격을 가로채 방어합니다! — {blkOutcome}";
            }
            else if (state.DummyOpponent.SecurityStack.Count > 0)
            {
                var nextSec = state.DummyOpponent.SecurityStack[0];
                int myDp = attacker.GetCurrentDP();
                int checks = attacker.GetSecurityAttackCount();
                int cleared = state.DummyOpponent.Trash.Count;
                string outcome = myDp > nextSec.DP
                    ? $"✅ 격파 가능! (내 DP {myDp} > 시큐리티 DP {nextSec.DP})"
                    : $"⚠️ 주의: 내 디지몬 소멸 위험! (내 DP {myDp} <= 시큐리티 DP {nextSec.DP})";

                guide = $"시큐리티 #{cleared + 1} [{nextSec.Name}(Lv.{nextSec.Level}, DP {nextSec.DP})] 공격! (체크 매수: {checks}장) — {outcome}";
            }
            else
            {
                guide = "🏆 상대 시큐리티 5장이 모두 파괴되었습니다! 플레이어에게 다이렉트 어택을 가해 게임에서 승리합니다!";
            }
        }
        else
        {
            int myDp = attacker.GetCurrentDP();
            int defDp = defenderTarget.GetCurrentDP();
            string outcome = myDp > defDp
                ? $"✅ 상대 디지몬 격파! (내 DP {myDp} > 상대 DP {defDp})"
                : myDp == defDp
                    ? $"💥 동귀어진(양쪽 모두 소멸)! (내 DP {myDp} == 상대 DP {defDp})"
                    : $"💀 내 디지몬 패배/소멸! (내 DP {myDp} < 상대 DP {defDp})";

            guide = $"상대 레스트 디지몬 [{defenderTarget.TopCard.Name}(DP {defDp})]을 공격합니다! — {outcome}";
        }

        return ActionResult.Success(guide);
    }

    // 4. 육성 페이즈 - 부화 유효성 검사
    public static ActionResult ValidateHatch(GameState state)
    {
        if (state.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (state.CurrentPhase != Phase.Breeding)
            return ActionResult.Fail("육성 페이즈에만 디지타마를 부화할 수 있습니다.");

        if (state.BreedingActionTaken)
            return ActionResult.Fail("이번 육성 페이즈에 이미 액션을 수행했습니다. (부화/이동 중 턴당 1회만 가능)");

        if (state.Player.BreedingArea != null)
            return ActionResult.Fail("육성 에어리어에 이미 디지몬이 존재합니다.");

        if (state.Player.DigitamaDeck.Count == 0)
            return ActionResult.Fail("디지타마 덱에 남은 카드가 없습니다.");

        return ActionResult.Success("디지타마 덱에서 Lv.2 디지타마를 육성 에어리어(절대 안전지대)에 부화합니다.");
    }

    // 5. 육성 페이즈 - 이동 유효성 검사
    public static ActionResult ValidateMoveFromBreeding(GameState state)
    {
        if (state.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (state.CurrentPhase != Phase.Breeding)
            return ActionResult.Fail("육성 페이즈에만 배틀 에어리어로 이동할 수 있습니다.");

        if (state.BreedingActionTaken)
            return ActionResult.Fail("이번 육성 페이즈에 이미 액션을 수행했습니다. (부화/이동 중 턴당 1회만 가능)");

        if (state.Player.BreedingArea == null)
            return ActionResult.Fail("육성 에어리어에 이동할 디지몬이 없습니다.");

        if (state.Player.BreedingArea.TopCard.Level < 3)
            return ActionResult.Fail("Lv.3(성장기) 이상으로 진화한 디지몬만 배틀 에어리어로 이동할 수 있습니다.");

        return ActionResult.Success($"육성 에어리어의 {state.Player.BreedingArea.TopCard.Name}(Lv.{state.Player.BreedingArea.TopCard.Level})을 배틀 에어리어로 이동합니다. (이동한 디지몬은 소환 멀미 없이 바로 공격 가능)");
    }
}
