using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine;

/// <summary>
/// 1인 솔리테어 (Stage 1), 블로커 돌파 챌린지 (Stage 2), 실전 AI 대전 (Stage 3) 통합 룰 엔진
/// </summary>
public class GoldfishEngine
{
    public GameState State { get; }
    private readonly Random _rng = new();

    public GoldfishEngine(GameState state)
    {
        State = state;
    }

    public void Log(string message)
    {
        string entry = $"[T{State.CurrentTurn} | {State.CurrentPhase} | M:{State.MemoryGauge:+#;-#;0}] {message}";
        State.ActionLogs.Add(entry);
    }

    /// <summary>
    /// 덱 및 선택한 스테이지로 새 게임 초기화
    /// </summary>
    public void StartNewGame(
        IReadOnlyList<CardData> mainDeck,
        IReadOnlyList<CardData> digitamaDeck,
         int? seed = null,
        PracticeStage? stage = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : _rng;
        if (stage.HasValue)
        {
            State.CurrentStage = stage.Value;
        }

        State.CurrentTurn = 1;
        State.CurrentPhase = Phase.Active;
        State.MemoryGauge = 3;
        State.CanMulligan = true;
        State.BreedingActionTaken = false;
        State.IsGameOver = false;
        State.IsVictory = false;
        State.GameOverReason = string.Empty;
        State.TotalMemorySpent = 0;
        State.TotalEvolutionsCount = 0;
        State.Lv3StartedChains = 0;
        State.Lv6CompletedChains = 0;
        State.SecurityCheckHistory.Clear();
        State.LastRevealedSecurityCard = null;
        State.LastSecurityBattleResult = string.Empty;
        State.LastSecurityBattleWon = true;
        State.ActionLogs.Clear();

        State.TargetTurns = State.CurrentStage switch
        {
            PracticeStage.Stage1_Goldfish => 5,
            PracticeStage.Stage2_BlockerDefense => 6,
            PracticeStage.Stage3_AiBattle => 8,
            _ => 5
        };

        State.Player = new PlayerZones();
        State.DummyOpponent = new PlayerZones();

        // 플레이어 디지타마 덱 셔플 후 적재
        foreach (var d in digitamaDeck.OrderBy(_ => rng.Next()))
        {
            State.Player.DigitamaDeck.Push(d);
        }

        // 플레이어 메인 덱 셔플
        var shuffledMain = mainDeck.OrderBy(_ => rng.Next()).ToList();
        State.Player.MainDeck.AddRange(shuffledMain);

        // 플레이어 시큐리티 5장 배치
        for (int i = 0; i < 5 && State.Player.MainDeck.Count > 0; i++)
        {
            State.Player.SecurityStack.Add(State.Player.MainDeck[0]);
            State.Player.MainDeck.RemoveAt(0);
        }

        // 플레이어 초기 패 5장 드로우
        for (int i = 0; i < 5 && State.Player.MainDeck.Count > 0; i++)
        {
            State.Player.Hand.Add(State.Player.MainDeck[0]);
            State.Player.MainDeck.RemoveAt(0);
        }

        // 스테이지별 상대방 세팅
        SetupOpponentForStage(rng);

        string stageTitle = State.CurrentStage switch
        {
            PracticeStage.Stage1_Goldfish => "Stage 1: 기본 타임어택 (골드피시)",
            PracticeStage.Stage2_BlockerDefense => "Stage 2: 블로커 & 수비진 돌파 챌린지",
            PracticeStage.Stage3_AiBattle => "Stage 3: 실전 AI 대전 (메모리 줄다리기 & 공방전)",
            _ => "1인 연습 모드"
        };

        Log($"=== [{stageTitle}] 새 게임을 시작합니다! ===");
        ExecuteActivePhase();
        ExecuteDrawPhase();
        State.CurrentPhase = Phase.Breeding;
        Log("육성 페이즈 시작: [부화], [배틀 이동], [패스] 또는 [첫 손패 다시 뽑기(멀리건)]를 선택하세요.");
    }

    private void SetupOpponentForStage(Random rng)
    {
        var catalog = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

        if (State.CurrentStage == PracticeStage.Stage3_AiBattle)
        {
            // 플레이어 주 색상이 Red면 AI는 ST-2 Blue, 아니면 ST-1 Red 사용
            bool playerIsRed = State.Player.Hand.Concat(State.Player.MainDeck).Count(c => c.Color == CardColor.Red) >= 20;
            var (aiMain, aiDigitama) = playerIsRed
                ? StarterDecks.CreateSt2CocytusBluePreset()
                : StarterDecks.CreateSt1GaiaRedPreset();

            foreach (var d in aiDigitama.OrderBy(_ => rng.Next()))
                State.DummyOpponent.DigitamaDeck.Push(d);

            State.DummyOpponent.MainDeck.AddRange(aiMain.OrderBy(_ => rng.Next()));

            // 공식 룰: 셔플된 50장 메인 덱 맨 위 5장을 그대로 시큐리티에 배치 (디지몬/테이머/옵션 모두 포함!)
            for (int i = 0; i < 5 && State.DummyOpponent.MainDeck.Count > 0; i++)
            {
                State.DummyOpponent.SecurityStack.Add(State.DummyOpponent.MainDeck[0]);
                State.DummyOpponent.MainDeck.RemoveAt(0);
            }

            // 공식 룰: 그 다음 5장을 초기 손패(Hand)로 드로우
            for (int i = 0; i < 5 && State.DummyOpponent.MainDeck.Count > 0; i++)
            {
                State.DummyOpponent.Hand.Add(State.DummyOpponent.MainDeck[0]);
                State.DummyOpponent.MainDeck.RemoveAt(0);
            }

            // AI 초기 필드: 성장기 1체 배치
            var starterLv3 = catalog[playerIsRed ? "ST2-02" : "ST1-02"];
            State.DummyOpponent.BattleArea.Add(new CardInstance(starterLv3, 0));
            return;
        }

        // Stage 1 & Stage 2 공통: 무작위 Lv.3 ~ Lv.6 디지몬 5장으로 시큐리티 구성
        var dummyDigimonPool = catalog.Values
            .Where(c => c.Type == CardType.Digimon && c.Level <= 6)
            .ToList();

        for (int i = 0; i < 5; i++)
        {
            var randomDigimon = dummyDigimonPool[rng.Next(dummyDigimonPool.Count)];
            State.DummyOpponent.SecurityStack.Add(randomDigimon);
        }

        if (State.CurrentStage == PracticeStage.Stage2_BlockerDefense)
        {
            // Stage 2: 상대 필드에 액티브 ≪블로커≫ 코어드라몬(DP 6000) + 레스트 상태 가루몬(진화원 보유, DP 5000) 배치!
            var blocker = new CardInstance(catalog["ST1-06"], 0) { IsSuspended = false };
            var restedTarget = new CardInstance(catalog["ST2-03"], 0);
            restedTarget.Evolve(catalog["ST2-06"]);
            restedTarget.IsSuspended = true;

            State.DummyOpponent.BattleArea.Add(blocker);
            State.DummyOpponent.BattleArea.Add(restedTarget);
        }
    }

    /// <summary>
    /// 첫 턴 멀리건 (초기 손패 5장을 덱으로 되돌리고 다시 5장 드로우 - 게임당 1회)
    /// </summary>
    public ActionResult ExecuteMulligan()
    {
        if (State.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (!State.CanMulligan || State.CurrentTurn > 1)
            return ActionResult.Fail("멀리건(손패 다시 뽑기)은 1턴 시작 시 1회만 사용할 수 있습니다.");

        State.Player.MainDeck.AddRange(State.Player.Hand);
        State.Player.Hand.Clear();

        var shuffled = State.Player.MainDeck.OrderBy(_ => _rng.Next()).ToList();
        State.Player.MainDeck.Clear();
        State.Player.MainDeck.AddRange(shuffled);

        for (int i = 0; i < 5 && State.Player.MainDeck.Count > 0; i++)
        {
            DrawCard();
        }

        State.CanMulligan = false;
        Log("🔄 멀리건 실행: 초기 손패 5장을 덱에 섞고 새로운 5장을 뽑았습니다.");
        return ActionResult.Success("🔄 멀리건 완료! 새로운 첫 손패 5장을 뽑았습니다.");
    }

    /// <summary>
    /// <summary>
    /// 필드 테이머(신태일/매튜 등) 지속 버프 수치 재계산
    /// </summary>
    public void RecalculateFieldBuffs()
    {
        int taiCount = State.Player.Tamers.Count(t => t.CardId == "ST1-12" || t.Color == CardColor.Red);
        int mattCount = State.Player.Tamers.Count(t => t.CardId == "ST2-12" || t.Color == CardColor.Blue);

        foreach (var inst in State.Player.BattleArea)
        {
            inst.TamerDpBonus = (taiCount * 1000) + (inst.EvolutionSources.Count > 0 ? mattCount * 1000 : 0);
            inst.TamerSecurityAttackBonus = (inst.EvolutionSources.Count >= 4 && taiCount > 0) ? taiCount : 0;
        }

        if (State.Player.BreedingArea != null)
        {
            State.Player.BreedingArea.TamerDpBonus = 0;
            State.Player.BreedingArea.TamerSecurityAttackBonus = 0;
        }
    }

    /// <summary>
    /// 1. 액티브 페이즈: 레스트 상태인 모든 카드를 액티브 상태로 기상
    /// </summary>
    public void ExecuteActivePhase()
    {
        State.CurrentPhase = Phase.Active;
        int awakened = 0;
        foreach (var instance in State.Player.BattleArea)
        {
            if (instance.IsSuspended)
            {
                instance.IsSuspended = false;
                awakened++;
            }
            instance.TemporaryDpBonus = 0;
            instance.TemporarySecurityAttackBonus = 0;
            instance.HasUsedOncePerTurnEffect = false;
            instance.ActiveSkillBuffLabels.Clear();
        }
        if (State.Player.BreedingArea != null)
        {
            State.Player.BreedingArea.IsSuspended = false;
            State.Player.BreedingArea.TemporaryDpBonus = 0;
            State.Player.BreedingArea.TemporarySecurityAttackBonus = 0;
            State.Player.BreedingArea.HasUsedOncePerTurnEffect = false;
            State.Player.BreedingArea.ActiveSkillBuffLabels.Clear();
        }
        RecalculateFieldBuffs();
        Log($"액티브 페이즈 완료 (기상한 디지몬: {awakened}체)");
    }

    /// <summary>
    /// 2. 드로우 페이즈: 메인 덱에서 1장 드로우 (선공 첫 턴은 스킵)
    /// </summary>
    public CardData? ExecuteDrawPhase()
    {
        State.CurrentPhase = Phase.Draw;
        if (State.CurrentTurn == 1)
        {
            Log("선공 1턴째 드로우 페이즈는 규칙에 따라 스킵합니다.");
            return null;
        }

        if (State.Player.MainDeck.Count == 0)
        {
            State.IsGameOver = true;
            State.IsVictory = false;
            State.GameOverReason = "드로우 페이즈에 덱이 0장이 되어 패배했습니다.";
            Log(State.GameOverReason);
            return null;
        }

        var drawn = DrawCard();
        if (drawn != null)
        {
            Log($"드로우 페이즈: [{drawn.Name}] 1장을 드로우했습니다.");
        }
        return drawn;
    }

    public CardData? DrawCard()
    {
        if (State.Player.MainDeck.Count == 0)
            return null;

        var card = State.Player.MainDeck[0];
        State.Player.MainDeck.RemoveAt(0);
        State.Player.Hand.Add(card);
        return card;
    }

    /// <summary>
    /// 3. 육성 페이즈 - 부화
    /// </summary>
    public ActionResult HatchDigitama()
    {
        var validation = ActionValidator.ValidateHatch(State);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        var digitama = State.Player.DigitamaDeck.Pop();
        State.Player.BreedingArea = new CardInstance(digitama, State.CurrentTurn);
        State.BreedingActionTaken = true;
        Log($"육성 에어리어 부화: [{digitama.Name}(Lv.{digitama.Level})]");

        State.CurrentPhase = Phase.Main;
        Log("메인 페이즈로 전환되었습니다.");
        return ActionResult.Success($"{digitama.Name}(Lv.{digitama.Level})을 육성 에어리어에 부화하고 메인 페이즈로 이동했습니다.");
    }

    /// <summary>
    /// 3. 육성 페이즈 - 배틀 에어리어로 이동
    /// </summary>
    public ActionResult MoveFromBreedingArea()
    {
        var validation = ActionValidator.ValidateMoveFromBreeding(State);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        var moving = State.Player.BreedingArea!;
        State.Player.BreedingArea = null;
        moving.TurnPlayed = Math.Min(moving.TurnPlayed, State.CurrentTurn - 1);
        State.Player.BattleArea.Add(moving);
        State.BreedingActionTaken = true;
        RecalculateFieldBuffs();

        Log($"육성 에어리어 이동: [{moving.TopCard.Name}(Lv.{moving.TopCard.Level})] -> 배틀 에어리어");

        State.CurrentPhase = Phase.Main;
        Log("메인 페이즈로 전환되었습니다.");
        return ActionResult.Success($"{moving.TopCard.Name}(Lv.{moving.TopCard.Level})을 배틀 에어리어로 이동하고 메인 페이즈로 전환했습니다.");
    }

    /// <summary>
    /// 3. 육성 페이즈 - 패스 (메인 페이즈로 전환)
    /// </summary>
    public ActionResult PassBreedingPhase()
    {
        if (State.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        if (State.CurrentPhase != Phase.Breeding)
            return ActionResult.Fail("현재 육성 페이즈가 아닙니다.");

        State.CanMulligan = false;
        State.BreedingActionTaken = true;
        State.CurrentPhase = Phase.Main;
        Log("육성 페이즈를 패스하고 메인 페이즈로 진입합니다.");
        return ActionResult.Success("육성 페이즈를 패스하고 메인 페이즈로 진입했습니다.");
    }

    /// <summary>
    /// 4. 메인 페이즈 - 카드 등장 / 옵션 사용
    /// </summary>
    public ActionResult PlayCard(CardData card, CardInstance? target = null)
    {
        var validation = ActionValidator.ValidatePlay(State, card);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        State.Player.Hand.Remove(card);
        State.MemoryGauge -= card.PlayCost;
        State.TotalMemorySpent += card.PlayCost;

        string effectSummary = string.Empty;

        if (card.Type == CardType.Digimon)
        {
            var instance = new CardInstance(card, State.CurrentTurn);
            State.Player.BattleArea.Add(instance);
            RecalculateFieldBuffs();
            if (card.Level == 3)
            {
                State.Lv3StartedChains++;
            }
            Log($"디지몬 등장: [{card.Name}(Lv.{card.Level})] (코스트 {card.PlayCost} 지불)");

            // BT1-029 파피몬 [등장 시] 1드로우
            if (card.CardId == "BT1-029")
            {
                var drawn = DrawCard();
                if (drawn != null)
                {
                    effectSummary = $" [등장 시 효과로 {drawn.Name} 드로우!]";
                    Log($"[효과] 파피몬(BT1) 등장 시 효과로 [{drawn.Name}] 1장 드로우!");
                }
            }
        }
        else if (card.Type == CardType.Tamer)
        {
            State.Player.Tamers.Add(card);
            RecalculateFieldBuffs();
            effectSummary = card.CardId == "ST1-12"
                ? " [🎖️테이머 버프: 아군 전체 DP +1000 & 진화원 4장 이상 시큐어택 +1 활성!]"
                : " [🎖️테이머 버프: 진화원 보유 아군 DP +1000 & 턴 시작 메모리 보정 활성!]";
            Log($"테이머 등장: [{card.Name}] (코스트 {card.PlayCost} 지불){effectSummary}");
        }
        else if (card.Type == CardType.Option)
        {
            effectSummary = " " + ApplyOptionEffect(card, target);
            RecalculateFieldBuffs();
            State.Player.Trash.Add(card);
            Log($"옵션 사용: [{card.Name}] (코스트 {card.PlayCost} 지불 후 트래시 이동)");
        }

        CheckTurnEndAndAutoReset();
        return ActionResult.Success(validation.GuideMessage + effectSummary);
    }

    private string ApplyOptionEffect(CardData optionCard, CardInstance? target)
    {
        if (optionCard.CardId == "ST1-13") // 섀도우 윙: 아군 디지몬 DP +3000
        {
            var ally = target ?? State.Player.BattleArea.FirstOrDefault();
            if (ally != null)
            {
                ally.TemporaryDpBonus += 3000;
                ally.ActiveSkillBuffLabels.Add("✨섀도우 윙 (DP +3,000)");
                string msg = $"[{ally.TopCard.Name}]의 DP가 이번 턴 +3000 상승했습니다! (현재 DP: {ally.GetCurrentDP()})";
                Log("[효과] " + msg);
                return msg;
            }
        }
        else if (optionCard.CardId == "ST1-14") // 스타라이트 익스플로전: 아군 DP +2000 & 시큐리티 어택 +1
        {
            var ally = target ?? State.Player.BattleArea.FirstOrDefault();
            if (ally != null)
            {
                ally.TemporaryDpBonus += 2000;
                ally.TemporarySecurityAttackBonus += 1;
                ally.ActiveSkillBuffLabels.Add("✨스타라이트 (DP +2,000 / 시큐어택 +1)");
                string msg = $"[{ally.TopCard.Name}]에게 DP +2000 및 ≪시큐리티 어택 +1≫ 부여!";
                Log("[효과] " + msg);
                return msg;
            }
        }
        else if (optionCard.CardId == "ST1-16") // 가이아 포스: 상대 디지몬 1체 소멸
        {
            var enemy = (target != null && State.DummyOpponent.BattleArea.Contains(target))
                ? target
                : State.DummyOpponent.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();

            if (enemy != null)
            {
                DeleteOpponentDigimon(enemy);
                string msg = $"🔥 가이아 포스 발동! 상대 [{enemy.TopCard.Name}(DP {enemy.GetCurrentDP()})]를 즉시 소멸시켰습니다!";
                Log("[효과] " + msg);
                return msg;
            }
        }
        else if (optionCard.CardId == "ST1-15") // 기가 디스트로이어: DP 4000 이하 상대 디지몬 최대 2체 소멸 (없으면 대상 1체 소멸)
        {
            var lowDpEnemies = State.DummyOpponent.BattleArea.Where(d => d.GetCurrentDP() <= 4000).Take(2).ToList();
            if (lowDpEnemies.Count > 0)
            {
                foreach (var e in lowDpEnemies) DeleteOpponentDigimon(e);
                string names = string.Join(", ", lowDpEnemies.Select(e => e.TopCard.Name));
                string msg = $"💥 기가 디스트로이어 발동! 상대 [{names}] 소멸!";
                Log("[효과] " + msg);
                return msg;
            }
            else if (target != null && State.DummyOpponent.BattleArea.Contains(target))
            {
                DeleteOpponentDigimon(target);
                string msg = $"💥 기가 디스트로이어로 상대 [{target.TopCard.Name}] 소멸!";
                Log("[효과] " + msg);
                return msg;
            }
        }
        else if (optionCard.CardId == "ST2-13") // 해머 스파크: 메모리 +2
        {
            State.MemoryGauge += 2;
            string msg = $"⚡ 해머 스파크로 메모리 +2 획득! (현재 메모리: {State.MemoryGauge})";
            Log("[효과] " + msg);
            return msg;
        }
        else if (optionCard.CardId == "ST2-14") // 소로우 블루: 상대 디지몬 레스트(방어 무력화)
        {
            var enemy = (target != null && State.DummyOpponent.BattleArea.Contains(target))
                ? target
                : State.DummyOpponent.BattleArea.FirstOrDefault();

            if (enemy != null)
            {
                enemy.IsSuspended = true;
                string msg = $"❄️ 소로우 블루 발동! 상대 [{enemy.TopCard.Name}]을(를) 강제로 레스트(무력화)시켰습니다!";
                Log("[효과] " + msg);
                return msg;
            }
        }
        else if (optionCard.CardId == "ST2-15") // 카이저 네일: 내 디지몬 진화원 1장을 배틀 에어리어에 분리 등장
        {
            var ally = (target != null && State.Player.BattleArea.Contains(target))
                ? target
                : State.Player.BattleArea.FirstOrDefault(d => d.EvolutionSources.Count > 0);

            if (ally != null && ally.EvolutionSources.Count > 0)
            {
                var popped = ally.EvolutionSources.Pop();
                if (popped.Type == CardType.Digimon)
                {
                    var newInst = new CardInstance(popped, State.CurrentTurn);
                    State.Player.BattleArea.Add(newInst);
                    string msg = $"❄️ 카이저 네일 발동! [{ally.TopCard.Name}]의 진화원에서 [{popped.Name}(Lv.{popped.Level})]이(가) 배틀 에어리어에 공짜로 등장했습니다!";
                    Log("[효과] " + msg);
                    return msg;
                }
            }
        }
        else if (optionCard.CardId == "ST2-16") // 코큐토스 브레스: 상대 디지몬 1체를 패로 바운스
        {
            var enemy = (target != null && State.DummyOpponent.BattleArea.Contains(target))
                ? target
                : State.DummyOpponent.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault();

            if (enemy != null)
            {
                State.DummyOpponent.BattleArea.Remove(enemy);
                State.DummyOpponent.Hand.Add(enemy.TopCard);
                while (enemy.EvolutionSources.Count > 0)
                    State.DummyOpponent.Trash.Add(enemy.EvolutionSources.Pop());

                string msg = $"🌊 코큐토스 브레스 발동! 상대 [{enemy.TopCard.Name}]의 진화원을 모두 파기하고 패로 되돌렸습니다!";
                Log("[효과] " + msg);
                return msg;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 4. 메인 페이즈 - 디지몬 진화 (진화 보너스 1드로우 및 진화 시 효과 포함)
    /// </summary>
    public ActionResult EvolveDigimon(CardInstance target, CardData evoCard)
    {
        var validation = ActionValidator.ValidateEvolution(State, target, evoCard);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        string prevName = target.TopCard.Name;
        int prevLevel = target.TopCard.Level;

        State.Player.Hand.Remove(evoCard);
        target.Evolve(evoCard);
        State.MemoryGauge -= evoCard.EvoCost;
        State.TotalMemorySpent += evoCard.EvoCost;
        State.TotalEvolutionsCount++;
        RecalculateFieldBuffs();

        if (prevLevel == 2 && evoCard.Level == 3)
        {
            State.Lv3StartedChains++;
        }
        else if (prevLevel == 5 && evoCard.Level >= 6)
        {
            State.Lv6CompletedChains++;
        }

        // [진화 시] 특수 효과 처리
        if (evoCard.CardId == "ST1-08") // 가루다몬: 아군 DP +3000
        {
            target.TemporaryDpBonus += 3000;
            target.ActiveSkillBuffLabels.Add("🔥가루다몬 진화스킬 (DP +3,000)");
            Log($"[진화 시 효과] 가루다몬의 효과로 이번 턴 DP +3000! (현재 DP: {target.GetCurrentDP()})");
        }
        else if (evoCard.CardId == "ST2-09") // 쥬드몬: 상대 디지몬 진화원 2장 파기
        {
            var enemyWithSources = State.DummyOpponent.BattleArea.FirstOrDefault(d => d.EvolutionSources.Count > 0);
            if (enemyWithSources != null)
            {
                int trashed = 0;
                while (enemyWithSources.EvolutionSources.Count > 0 && trashed < 2)
                {
                    State.DummyOpponent.Trash.Add(enemyWithSources.EvolutionSources.Pop());
                    trashed++;
                }
                Log($"[진화 시 효과] 쥬드몬이 상대 [{enemyWithSources.TopCard.Name}]의 진화원 {trashed}장을 파기했습니다!");
            }
        }

        var drawn = DrawCard();
        string drawText = drawn != null ? $"진화 보너스 드로우: [{drawn.Name}]" : "덱이 비어 진화 보너스 드로우 없음";
        Log($"디지몬 진화: [{prevName}(Lv.{prevLevel})] -> [{evoCard.Name}(Lv.{evoCard.Level})] (코스트 {evoCard.EvoCost}, {drawText})");

        CheckTurnEndAndAutoReset();
        return validation;
    }

    /// <summary>
    /// 4. 메인 페이즈 - 상대 레스트 상태 디지몬 직접 공격 (필드 제압)
    /// </summary>
    public ActionResult AttackOpponentDigimon(CardInstance attacker, CardInstance defender)
    {
        var validation = ActionValidator.ValidateAttack(State, attacker, defender);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        attacker.IsSuspended = true;
        TriggerWhenAttackingEffects(attacker);

        int myDp = attacker.GetCurrentDP();
        int defDp = defender.GetCurrentDP();
        string resultMsg;

        if (myDp > defDp)
        {
            DeleteOpponentDigimon(defender);
            State.LastSecurityBattleWon = true;
            resultMsg = $"⚔️ 필드 배틀 승리! 내 [{attacker.TopCard.Name}(DP {myDp})] > 상대 [{defender.TopCard.Name}(DP {defDp})] -> 상대 디지몬을 격파했습니다!";
        }
        else if (myDp == defDp)
        {
            DeleteOpponentDigimon(defender);
            DeleteDigimon(attacker);
            State.LastSecurityBattleWon = false;
            resultMsg = $"💥 동귀어진! 내 [{attacker.TopCard.Name}(DP {myDp})] == 상대 [{defender.TopCard.Name}(DP {defDp})] -> 양쪽 디지몬이 모두 소멸했습니다!";
        }
        else
        {
            DeleteDigimon(attacker);
            State.LastSecurityBattleWon = false;
            resultMsg = $"💀 필드 배틀 패배! 내 [{attacker.TopCard.Name}(DP {myDp})] < 상대 [{defender.TopCard.Name}(DP {defDp})] -> 내 디지몬이 소멸했습니다!";
        }

        Log(resultMsg);
        return ActionResult.Success(resultMsg);
    }

    /// <summary>
    /// 4. 메인 페이즈 - 상대 시큐리티 공격 및 다이렉트 어택 판정 (상대 액티브 블로커 가로채기 포함)
    /// </summary>
    public ActionResult AttackSecurityOrDirect(CardInstance attacker)
    {
        var validation = ActionValidator.ValidateAttack(State, attacker, null);
        if (!validation.CanExecute)
            return validation;

        State.CanMulligan = false;
        attacker.IsSuspended = true;
        TriggerWhenAttackingEffects(attacker);

        // 1) 상대 필드에 액티브 상태인 ≪블로커≫ 디지몬이 있으면 시큐리티 공격을 가로채 방어!
        var activeBlocker = State.DummyOpponent.BattleArea.FirstOrDefault(d => d.TopCard.IsBlocker && !d.IsSuspended);
        if (activeBlocker != null)
        {
            activeBlocker.IsSuspended = true;
            int myDp = attacker.GetCurrentDP();
            int blkDp = activeBlocker.GetCurrentDP();
            string blkMsg;

            if (myDp > blkDp)
            {
                DeleteOpponentDigimon(activeBlocker);
                State.LastSecurityBattleWon = true;
                blkMsg = $"🛡️💥 상대 ≪블로커≫ [{activeBlocker.TopCard.Name}(DP {blkDp})]가 공격을 가로챘으나, 내 [{attacker.TopCard.Name}(DP {myDp})]가 블로커를 격파했습니다! (시큐리티 체크는 방어됨)";
            }
            else if (myDp == blkDp)
            {
                DeleteOpponentDigimon(activeBlocker);
                DeleteDigimon(attacker);
                State.LastSecurityBattleWon = false;
                blkMsg = $"🛡️💥 상대 ≪블로커≫ [{activeBlocker.TopCard.Name}(DP {blkDp})]와 동귀어진하여 양쪽 모두 소멸했습니다!";
            }
            else
            {
                DeleteDigimon(attacker);
                State.LastSecurityBattleWon = false;
                blkMsg = $"🛡️💀 상대 ≪블로커≫ [{activeBlocker.TopCard.Name}(DP {blkDp})]의 방어에 막혀 내 [{attacker.TopCard.Name}(DP {myDp})]가 소멸했습니다!";
            }

            State.LastSecurityBattleResult = blkMsg;
            Log(blkMsg);
            return ActionResult.Success(blkMsg);
        }

        // 2) 시큐리티가 0장인 상태에서 블로커도 없으면 -> 다이렉트 어택 승리!
        if (State.DummyOpponent.SecurityStack.Count == 0)
        {
            State.IsGameOver = true;
            State.IsVictory = true;
            string timeAttackEval = State.CurrentTurn <= State.TargetTurns
                ? $"목표 턴({State.TargetTurns}턴 이내) 달성 성공!"
                : $"목표 턴({State.TargetTurns}턴) 초과 클리어";

            State.GameOverReason =
                $"🎉 [{attacker.TopCard.Name}]의 다이렉트 어택 성공! {State.CurrentTurn}턴 만에 승리했습니다! ({timeAttackEval})";
            Log(State.GameOverReason);
            return ActionResult.Success(State.GameOverReason);
        }

        // 3) 공식 룰에 따른 시큐리티 체크 수행 (디지몬 배틀 / 테이머 공짜 등장 / 옵션 [시큐리티] 효과 발동)
        int checksToPerform = attacker.GetSecurityAttackCount();
        int attackerDp = attacker.GetCurrentDP();
        var battleSummaries = new List<string>();

        Log($"[{attacker.TopCard.Name}(DP {attackerDp})] 시큐리티 어택 선언! (체크 예정 매수: {checksToPerform}장)");

        for (int i = 0; i < checksToPerform; i++)
        {
            if (!State.Player.BattleArea.Contains(attacker))
            {
                battleSummaries.Add($"공격 디지몬 [{attacker.TopCard.Name}]이(가) 필드에서 벗어나 남은 시큐리티 체크가 중단되었습니다.");
                break;
            }

            if (State.DummyOpponent.SecurityStack.Count == 0)
            {
                battleSummaries.Add("남은 시큐리티가 0장이 되어 추가 체크를 종료합니다.");
                break;
            }

            attackerDp = attacker.GetCurrentDP();
            var securityCard = State.DummyOpponent.SecurityStack[0];
            State.DummyOpponent.SecurityStack.RemoveAt(0);
            State.LastRevealedSecurityCard = securityCard;

            // [공식 룰 A] 시큐리티에서 테이머 카드가 오픈된 경우 -> 배틀 없이 코스트 0으로 상대 테이머 존에 등장!
            if (securityCard.Type == CardType.Tamer)
            {
                State.DummyOpponent.Tamers.Add(securityCard);
                string msg = $"✨ [시큐리티 테이머 발동!] 오픈된 [{securityCard.Name}]이(가) 코스트 지불 없이 상대 필드에 등장! 내 [{attacker.TopCard.Name}]은(는) 생존합니다. (남은 시큐리티: {State.DummyOpponent.SecurityStack.Count}장)";
                State.LastSecurityBattleResult = $"시큐리티 테이머 [{securityCard.Name}] 공짜 등장! (내 디지몬 생존)";
                State.LastSecurityBattleWon = true;
                State.SecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                    securityCard, attacker.TopCard.Name, attackerDp, IsBattleWon: true, OutcomeSummary: "🎖️ [시큐리티] 테이머 공짜 등장"));
                battleSummaries.Add(msg);
                Log(msg);
                continue;
            }

            // [공식 룰 B] 시큐리티에서 옵션 카드가 오픈된 경우 -> 배틀 없이 [시큐리티] 효과 즉시 발동 후 트래시행!
            if (securityCard.Type == CardType.Option)
            {
                State.DummyOpponent.Trash.Add(securityCard);
                var (optMsg, optSummary, attackerRemoved) = ResolveOpponentSecurityOption(securityCard, attacker);
                State.LastSecurityBattleResult = optSummary;
                State.LastSecurityBattleWon = !attackerRemoved;
                State.SecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                    securityCard, attacker.TopCard.Name, attackerDp, IsBattleWon: !attackerRemoved, OutcomeSummary: optSummary));
                battleSummaries.Add(optMsg);
                Log(optMsg);

                if (attackerRemoved)
                {
                    break;
                }
                continue;
            }

            // [공식 룰 C] 시큐리티에서 디지몬 카드가 오픈된 경우 -> 시큐리티 디지몬과 DP 배틀 후 트래시행!
            State.DummyOpponent.Trash.Add(securityCard);
            int securityDp = securityCard.DP;

            if (attackerDp > securityDp)
            {
                string msg = $"✅ 배틀 승리! 공격자 [{attacker.TopCard.Name}(DP {attackerDp})] > 시큐리티 [{securityCard.Name}(DP {securityDp})] -> 내 디지몬 생존 & 시큐리티 파괴! (남은 시큐리티: {State.DummyOpponent.SecurityStack.Count}장)";
                State.LastSecurityBattleResult = $"배틀 승리! ({attacker.TopCard.Name} DP {attackerDp} > {securityCard.Name} DP {securityDp})";
                State.LastSecurityBattleWon = true;
                State.SecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                    securityCard, attacker.TopCard.Name, attackerDp, IsBattleWon: true, OutcomeSummary: $"✅ 배틀 승리 (DP {attackerDp} > {securityDp})"));
                battleSummaries.Add(msg);
                Log(msg);
            }
            else
            {
                string msg = $"💀 배틀 패배(내 디지몬 소멸)! 공격자 [{attacker.TopCard.Name}(DP {attackerDp})] <= 시큐리티 [{securityCard.Name}(DP {securityDp})] -> 내 [{attacker.TopCard.Name}] 소멸! (※ 디지몬 TCG 규칙상 오픈된 시큐리티 [{securityCard.Name}]도 함께 트래시로 이동합니다)";
                State.LastSecurityBattleResult = $"내 디지몬 소멸! ({attacker.TopCard.Name} DP {attackerDp} <= {securityCard.Name} DP {securityDp} — 시큐리티는 규칙상 함께 소모됨)";
                State.LastSecurityBattleWon = false;
                State.SecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                    securityCard, attacker.TopCard.Name, attackerDp, IsBattleWon: false, OutcomeSummary: $"💀 내 디지몬 소멸 (DP {attackerDp} <= {securityDp})"));
                battleSummaries.Add(msg);
                Log(msg);
                DeleteDigimon(attacker);
                break;
            }
        }

        // 공격 도중 시큐리티 해머 스파크 등으로 메모리가 0 미만이 되었다면, 공식 룰에 따라 어택 종료 후 턴이 넘어갑니다.
        if (State.MemoryGauge < 0)
        {
            CheckTurnEndAndAutoReset();
        }

        return ActionResult.Success(string.Join(" / ", battleSummaries));
    }

    private (string LogMessage, string ShortSummary, bool AttackerRemoved) ResolveOpponentSecurityOption(CardData optionCard, CardInstance attacker)
    {
        switch (optionCard.CardId)
        {
            case "ST1-16": // 가이아 포스: [시큐리티] 상대 디지몬 1체를 소멸시킨다
            {
                DeleteDigimon(attacker);
                return (
                    $"💥 [상대 시큐리티 가이아 포스 발동!] 시큐리티에서 오픈된 [가이아 포스]의 [시큐리티] 효과로 공격하던 내 [{attacker.TopCard.Name}]이(가) 즉시 소멸했습니다!",
                    $"💥 [시큐리티 가이아 포스] 내 [{attacker.TopCard.Name}] 소멸!",
                    true);
            }
            case "ST2-16": // 코큐토스 브레스: [시큐리티] 상대 디지몬 1체를 패로 되돌린다
            {
                if (State.Player.BattleArea.Remove(attacker))
                {
                    while (attacker.EvolutionSources.Count > 0)
                    {
                        State.Player.Trash.Add(attacker.EvolutionSources.Pop());
                    }
                    State.Player.Hand.Add(attacker.TopCard);
                }
                return (
                    $"🌊 [상대 시큐리티 코큐토스 브레스 발동!] 시큐리티에서 오픈된 [코큐토스 브레스] 효과로 공격하던 내 [{attacker.TopCard.Name}]이(가) 패로 바운스되었습니다!",
                    $"🌊 [시큐리티 코큐토스 브레스] 내 [{attacker.TopCard.Name}] 패로 바운스!",
                    true);
            }
            case "ST1-15": // 기가 디스트로이어: [시큐리티] DP 4000 이하 디지몬 2체까지 소멸
            {
                var lowTargets = State.Player.BattleArea.Where(d => d.GetCurrentDP() <= 4000).Take(2).ToList();
                bool removedAttacker = false;
                foreach (var t in lowTargets)
                {
                    if (ReferenceEquals(t, attacker)) removedAttacker = true;
                    DeleteDigimon(t);
                }
                return (
                    $"🔥 [상대 시큐리티 기가 디스트로이어 발동!] 내 필드의 DP 4000 이하 디지몬 {lowTargets.Count}체가 소멸했습니다!",
                    $"🔥 [시큐리티 기가 디스트로이어] DP 4000↓ {lowTargets.Count}체 소멸",
                    removedAttacker);
            }
            case "ST2-13": // 해머 스파크: [시큐리티] 메모리를 +2 얻는다 (상대 시점이므로 내 메모리 -2)
            {
                State.MemoryGauge -= 2;
                return (
                    $"⚡ [상대 시큐리티 해머 스파크 발동!] 상대가 메모리를 +2 얻어 내 메모리가 2 감소했습니다! (현재 메모리: {State.MemoryGauge})",
                    $"⚡ [시큐리티 해머 스파크] 내 메모리 -2 감소!",
                    false);
            }
            case "ST2-14": // 소로우 블루: [시큐리티] 상대 디지몬 1체를 레스트시킨다
            {
                var unsuspended = State.Player.BattleArea.FirstOrDefault(d => !d.IsSuspended);
                if (unsuspended != null)
                {
                    unsuspended.IsSuspended = true;
                }
                return (
                    $"❄️ [상대 시큐리티 소로우 블루 발동!] {(unsuspended != null ? $"내 [{unsuspended.TopCard.Name}]이(가) 레스트되었습니다!" : "레스트할 추가 디지몬이 없어 통과합니다.")}",
                    "❄️ [시큐리티 소로우 블루] 디지몬 레스트 발동",
                    false);
            }
            case "ST1-13": // 섀도우 윙
            case "ST1-14": // 스타라이트 익스플로전
            {
                int buff = optionCard.CardId == "ST1-14" ? 7000 : 3000;
                foreach (var opp in State.DummyOpponent.BattleArea)
                {
                    opp.TemporaryDpBonus += buff;
                }
                return (
                    $"✨ [상대 시큐리티 {optionCard.Name} 발동!] 이번 턴 동안 상대 필드의 모든 디지몬 DP가 +{buff:N0} 증가했습니다!",
                    $"✨ [시큐리티 {optionCard.Name}] 상대 디지몬 DP +{buff:N0}",
                    false);
            }
            case "ST2-15": // 카이저 네일: 진화원 1장 공짜 등장
            {
                var oppWithSrc = State.DummyOpponent.BattleArea.FirstOrDefault(d => d.EvolutionSources.Any(s => s.Type == CardType.Digimon));
                if (oppWithSrc != null)
                {
                    var temp = new Stack<CardData>();
                    CardData? extracted = null;
                    while (oppWithSrc.EvolutionSources.Count > 0)
                    {
                        var top = oppWithSrc.EvolutionSources.Pop();
                        if (extracted == null && top.Type == CardType.Digimon) extracted = top;
                        else temp.Push(top);
                    }
                    while (temp.Count > 0) oppWithSrc.EvolutionSources.Push(temp.Pop());
                    if (extracted != null)
                    {
                        State.DummyOpponent.BattleArea.Add(new CardInstance(extracted, State.CurrentTurn));
                        return (
                            $"❄️ [상대 시큐리티 카이저 네일 발동!] 상대 진화원에서 [{extracted.Name}]이(가) 필드에 공짜 등장했습니다!",
                            $"❄️ [시큐리티 카이저 네일] [{extracted.Name}] 등장!",
                            false);
                    }
                }
                return (
                    $"❄️ [상대 시큐리티 카이저 네일] 발동 가능한 진화원이 없어 불발로 트래시되었습니다.",
                    "❄️ [시큐리티 카이저 네일] 효과 불발",
                    false);
            }
            default:
                return (
                    $"✨ [상대 시큐리티 옵션 [{optionCard.Name}] 오픈!] [시큐리티] 효과 처리 후 트래시로 이동합니다.",
                    $"✨ [시큐리티 옵션] {optionCard.Name} 발동",
                    false);
        }
    }

    private void TriggerWhenAttackingEffects(CardInstance attacker)
    {
        // ST2-11 메탈가루몬: [어택 시] (턴당 1회) 이 디지몬을 액티브 상태로 전환 (재행동)
        if (attacker.TopCard.CardId == "ST2-11" && !attacker.HasUsedOncePerTurnEffect)
        {
            attacker.HasUsedOncePerTurnEffect = true;
            attacker.IsSuspended = false;
            Log("❄️ [어택 시 효과] 메탈가루몬이 액티브 상태로 전환되어 이번 턴 한 번 더 공격할 수 있습니다!");
        }

        // ST2-03 파피몬 / ST2-06 가루몬 진화원 효과: [어택 시] 상대 디지몬 진화원 1장 파기
        if (attacker.EvolutionSources.Any(s => s.CardId is "ST2-03" or "ST2-06"))
        {
            var enemyWithSource = State.DummyOpponent.BattleArea.FirstOrDefault(d => d.EvolutionSources.Count > 0);
            if (enemyWithSource != null)
            {
                var trashed = enemyWithSource.EvolutionSources.Pop();
                State.DummyOpponent.Trash.Add(trashed);
                Log($"❄️ [진화원 효과] 상대 [{enemyWithSource.TopCard.Name}]의 진화원 [{trashed.Name}]을(를) 파기했습니다!");
            }
        }
    }

    private void DeleteDigimon(CardInstance instance)
    {
        if (State.Player.BattleArea.Remove(instance))
        {
            State.Player.Trash.Add(instance.TopCard);
            while (instance.EvolutionSources.Count > 0)
            {
                State.Player.Trash.Add(instance.EvolutionSources.Pop());
            }
        }
    }

    private void DeleteOpponentDigimon(CardInstance instance)
    {
        if (State.DummyOpponent.BattleArea.Remove(instance))
        {
            State.DummyOpponent.Trash.Add(instance.TopCard);
            while (instance.EvolutionSources.Count > 0)
            {
                State.DummyOpponent.Trash.Add(instance.EvolutionSources.Pop());
            }
        }
    }

    /// <summary>
    /// 수동으로 턴 종료 선언
    /// </summary>
    public ActionResult PassTurn()
    {
        if (State.IsGameOver)
            return ActionResult.Fail("이미 종료된 게임입니다.");

        State.CanMulligan = false;
        State.MemoryGauge = -3;
        Log("플레이어가 턴 종료(Pass)를 선언했습니다.");
        CheckTurnEndAndAutoReset();
        return ActionResult.Success($"턴을 종료했습니다. 상대 턴 진행 후 {State.CurrentTurn}턴(메모리 +{State.MemoryGauge})이 시작됩니다.");
    }

    /// <summary>
    /// <summary>
    /// 5. 턴 종료 체크 및 스테이지별 상대방(골드피시 / 수비 웨이브 / AI 대전) 턴 진행
    /// </summary>
    public bool CheckTurnEndAndAutoReset()
    {
        if (State.IsGameOver || State.MemoryGauge >= 0)
            return false;

        State.CurrentPhase = Phase.TurnEnd;
        int memoryPassedToOpponent = Math.Abs(State.MemoryGauge);
        Log($"메모리가 {State.MemoryGauge}(0 미만)이 되어 플레이어 턴이 종료됩니다. (상대에게 넘어간 메모리: +{memoryPassedToOpponent})");

        int nextPlayerStartingMemory = 3;

        if (State.CurrentStage == PracticeStage.Stage2_BlockerDefense)
        {
            ExecuteStage2OpponentWave();
        }
        else if (State.CurrentStage == PracticeStage.Stage3_AiBattle)
        {
            nextPlayerStartingMemory = ExecuteStage3AiTurn(memoryPassedToOpponent);
            if (State.IsGameOver)
                return true;
        }

        State.CurrentTurn++;
        State.BreedingActionTaken = false;

        if (State.CurrentStage == PracticeStage.Stage3_AiBattle)
        {
            // [공식 룰] AI가 마이너스(-)로 넘긴 메모리 절대값만큼 내 턴 시작 메모리가 됩니다! (무조건 3 고정 아님)
            int mem = nextPlayerStartingMemory;

            // 신태일(ST1-12): 내 턴 시작 시 메모리가 2 이하라면 3으로 보정
            if (State.Player.Tamers.Any(t => t.CardId == "ST1-12") && mem < 3)
            {
                mem = 3;
                Log("🎖️ [테이머 신태일 효과] 시작 메모리가 2 이하이므로 3메모리로 보정되었습니다!");
            }

            // 매튜(ST2-12): 내 턴 시작 시 메모리 +1 추가
            int mattBonus = State.Player.Tamers.Count(t => t.CardId == "ST2-12");
            if (mattBonus > 0)
            {
                mem += mattBonus;
                Log($"🎖️ [테이머 매튜 효과] 시작 메모리를 +{mattBonus} 추가 확보했습니다!");
            }

            State.MemoryGauge = mem;
        }
        else
        {
            // Stage 1 & 2 연습 모드: 기본 3메모리 + 테이머 보너스
            int tamerBonus = State.Player.Tamers.Count(t => t.CardId is "ST1-12" or "ST2-12");
            State.MemoryGauge = Math.Max(nextPlayerStartingMemory, 3) + tamerBonus;
        }

        Log($"=== {State.CurrentTurn}턴 시작! (내 시작 메모리: +{State.MemoryGauge}) ===");
        ExecuteActivePhase();
        ExecuteDrawPhase();
        if (!State.IsGameOver)
        {
            State.CurrentPhase = Phase.Breeding;
        }
        return true;
    }

    private void ExecuteStage2OpponentWave()
    {
        foreach (var d in State.DummyOpponent.BattleArea)
        {
            d.IsSuspended = false;
            d.TemporaryDpBonus = 0;
            d.TemporarySecurityAttackBonus = 0;
        }

        if (State.DummyOpponent.BattleArea.Count < 2)
        {
            var catalog = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
            var blocker = catalog[State.CurrentTurn % 2 == 0 ? "ST2-07" : "ST1-06"]; // 그리즈몬 or 코어드라몬
            State.DummyOpponent.BattleArea.Add(new CardInstance(blocker, State.CurrentTurn));
            Log($"🛡️ [Stage 2 수비 증원] 상대 필드에 ≪블로커≫ [{blocker.Name}(DP {blocker.DP})]가 추가 배치되었습니다!");
        }
        else
        {
            Log("🛡️ [Stage 2 상대 턴] 상대 수비진의 모든 디지몬이 액티브 상태로 기상했습니다.");
        }
    }

    private int ExecuteStage3AiTurn(int aiStartingMemory)
    {
        // [공식 룰] 플레이어가 넘겨준 메모리(예: -1이면 +1, -2이면 +2)로 AI 턴을 시작합니다!
        int aiMemory = aiStartingMemory;
        if (State.DummyOpponent.Tamers.Any(t => t.CardId == "ST1-12") && aiMemory < 3)
        {
            aiMemory = 3;
        }
        aiMemory += State.DummyOpponent.Tamers.Count(t => t.CardId == "ST2-12");

        Log($"🤖 === [AI 상대 턴 시작 | 플레이어가 넘겨준 AI 시작 메모리: +{aiMemory}] ===");

        // 1. AI 액티브 페이즈
        foreach (var d in State.DummyOpponent.BattleArea)
        {
            d.IsSuspended = false;
            d.TemporaryDpBonus = 0;
            d.TemporarySecurityAttackBonus = 0;
            d.HasUsedOncePerTurnEffect = false;
        }

        // 2. AI 드로우 페이즈
        if (State.DummyOpponent.MainDeck.Count > 0)
        {
            var drawn = State.DummyOpponent.MainDeck[0];
            State.DummyOpponent.MainDeck.RemoveAt(0);
            State.DummyOpponent.Hand.Add(drawn);
        }

        // 3-A. [어택 전 메인 페이즈] 메모리가 0 이상으로 유지되는 선에서 먼저 진화 (진화 직후 바로 어택 가능!)
        bool preAttackEvolved = true;
        while (aiMemory >= 0 && preAttackEvolved)
        {
            preAttackEvolved = false;
            foreach (var fieldDigimon in State.DummyOpponent.BattleArea.ToList())
            {
                var safeEvo = State.DummyOpponent.Hand
                    .FirstOrDefault(c => c.Type == CardType.Digimon
                                         && c.EvoRequirementLevel == fieldDigimon.TopCard.Level
                                         && c.EvoCost <= aiMemory);

                if (safeEvo != null)
                {
                    State.DummyOpponent.Hand.Remove(safeEvo);
                    int beforeMem = aiMemory;
                    string prev = fieldDigimon.TopCard.Name;
                    fieldDigimon.Evolve(safeEvo);
                    aiMemory -= safeEvo.EvoCost;
                    if (State.DummyOpponent.MainDeck.Count > 0)
                    {
                        State.DummyOpponent.Hand.Add(State.DummyOpponent.MainDeck[0]);
                        State.DummyOpponent.MainDeck.RemoveAt(0);
                    }
                    Log($"🤖 [AI 진화] [{prev}] -> [{safeEvo.Name}(Lv.{safeEvo.Level}, 코스트 {safeEvo.EvoCost})] (AI 메모리: +{beforeMem} → +{aiMemory})");
                    preAttackEvolved = true;
                    break;
                }
            }
        }

        // 3-B. [AI 어택 선언] 공식 룰상 메모리가 0 이상일 때만 공격할 수 있습니다!
        var attackers = State.DummyOpponent.BattleArea
            .Where(d => !d.IsSuspended && d.TurnPlayed < State.CurrentTurn && (!d.TopCard.IsBlocker || State.DummyOpponent.BattleArea.Count >= 2))
            .OrderByDescending(d => d.GetCurrentDP())
            .ToList();

        foreach (var aiAttacker in attackers)
        {
            // 공격 도중 내 시큐리티 해머 스파크 등으로 AI 메모리가 0 미만이 되면 즉시 AI 턴 종료!
            if (aiMemory < 0)
            {
                Log($"⚡ AI 메모리가 {aiMemory}(0 미만)이 되어 AI의 추가 공격 및 메인 페이즈가 즉시 종료됩니다!");
                break;
            }

            if (!State.DummyOpponent.BattleArea.Contains(aiAttacker) || aiAttacker.IsSuspended)
                continue;

            aiAttacker.IsSuspended = true;
            int aiDp = aiAttacker.GetCurrentDP();
            int aiChecks = aiAttacker.GetSecurityAttackCount();

            // 플레이어에게 액티브 블로커가 있으면 자동 블록!
            var playerBlocker = State.Player.BattleArea.FirstOrDefault(d => d.TopCard.IsBlocker && !d.IsSuspended);
            if (playerBlocker != null)
            {
                playerBlocker.IsSuspended = true;
                int myBlkDp = playerBlocker.GetCurrentDP();
                if (myBlkDp > aiDp)
                {
                    DeleteOpponentDigimon(aiAttacker);
                    Log($"🛡️ [내 블로커 방어 성공!] 내 [{playerBlocker.TopCard.Name}(DP {myBlkDp})]이(가) AI의 [{aiAttacker.TopCard.Name}(DP {aiDp})]을(를) 블록하여 격파했습니다!");
                }
                else
                {
                    DeleteDigimon(playerBlocker);
                    if (myBlkDp == aiDp) DeleteOpponentDigimon(aiAttacker);
                    Log($"🛡️ [내 블로커 희생 방어] 내 [{playerBlocker.TopCard.Name}]이(가) AI의 [{aiAttacker.TopCard.Name}] 공격을 대신 막고 소멸했습니다!");
                }
                continue;
            }

            // 플레이어 시큐리티가 0장이면 AI의 다이렉트 어택 -> 플레이어 패배!
            if (State.Player.SecurityStack.Count == 0)
            {
                State.IsGameOver = true;
                State.IsVictory = false;
                State.GameOverReason = $"💀 AI의 [{aiAttacker.TopCard.Name}]에게 다이렉트 어택을 허용하여 패배했습니다! (블로커를 세우거나 먼저 시큐리티를 돌파하세요)";
                Log(State.GameOverReason);
                return 3;
            }

            // AI 디지몬의 시큐리티 어택 매수만큼 내 시큐리티를 공식 룰대로 오픈!
            for (int c = 0; c < aiChecks; c++)
            {
                if (!State.DummyOpponent.BattleArea.Contains(aiAttacker) || State.Player.SecurityStack.Count == 0)
                    break;

                aiDp = aiAttacker.GetCurrentDP();
                var mySec = State.Player.SecurityStack[0];
                State.Player.SecurityStack.RemoveAt(0);

                // 1) 내 시큐리티에서 [테이머]가 나온 경우 -> 공식 룰: 코스트 지불 없이 내 테이머 존에 무료 등장!
                if (mySec.Type == CardType.Tamer)
                {
                    State.Player.Tamers.Add(mySec);
                    RecalculateFieldBuffs();
                    string tamerMsg = $"✨ [내 시큐리티 테이머 발동!] AI [{aiAttacker.TopCard.Name}]의 공격으로 오픈된 내 시큐리티 [{mySec.Name}]이(가) 코스트 없이 내 필드에 무료 등장했습니다! (내 남은 시큐리티: {State.Player.SecurityStack.Count}장)";
                    State.PlayerSecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                        mySec, aiAttacker.TopCard.Name, aiDp, IsBattleWon: true, OutcomeSummary: "🎖️ 내 테이머 무료 등장!"));
                    Log(tamerMsg);
                    continue;
                }

                // 2) 내 시큐리티에서 [옵션]이 나온 경우 -> 공식 룰: [시큐리티] 효과를 즉시 무료 발동 후 트래시행!
                if (mySec.Type == CardType.Option)
                {
                    State.Player.Trash.Add(mySec);
                    var (optLog, optSummary, aiRemoved) = ResolvePlayerSecurityOptionAgainstAi(mySec, aiAttacker, ref aiMemory);
                    State.PlayerSecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                        mySec, aiAttacker.TopCard.Name, aiDp, IsBattleWon: aiRemoved, OutcomeSummary: optSummary));
                    Log(optLog);
                    if (aiRemoved)
                        break;
                    continue;
                }

                // 3) 내 시큐리티에서 [디지몬]이 나온 경우 -> 공식 룰: 시큐리티 DP 배틀 후 트래시행!
                State.Player.Trash.Add(mySec);
                if (mySec.DP >= aiDp)
                {
                    DeleteOpponentDigimon(aiAttacker);
                    string winMsg = $"🛡️💥 [내 시큐리티 역격파!] AI [{aiAttacker.TopCard.Name}(DP {aiDp})]가 내 시큐리티 디지몬 [{mySec.Name}(DP {mySec.DP})]와 배틀하여 역으로 소멸했습니다! (내 남은 시큐리티: {State.Player.SecurityStack.Count}장)";
                    State.PlayerSecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                        mySec, aiAttacker.TopCard.Name, aiDp, IsBattleWon: true, OutcomeSummary: $"💥 AI [{aiAttacker.TopCard.Name}] 역격파!"));
                    Log(winMsg);
                    break;
                }
                else
                {
                    string loseMsg = $"🤖⚔️ AI [{aiAttacker.TopCard.Name}(DP {aiDp})]의 공격으로 내 시큐리티 [{mySec.Name}(DP {mySec.DP})]이(가) 파괴되었습니다! (내 남은 시큐리티: {State.Player.SecurityStack.Count}장)";
                    State.PlayerSecurityCheckHistory.Add(new SecurityCheckHistoryItem(
                        mySec, aiAttacker.TopCard.Name, aiDp, IsBattleWon: false, OutcomeSummary: $"⚔️ 시큐리티 돌파됨 (내 DP {mySec.DP} < AI {aiDp})"));
                    Log(loseMsg);
                }
            }
        }

        // 3-C. [어택 후 메인 페이즈] AI가 남은 메모리를 사용하여 진화/등장 (메모리가 마이너스(-)로 넘어가면 그 절대값이 플레이어의 다음 턴 메모리가 됨!)
        bool acted = true;
        while (aiMemory >= 0 && acted)
        {
            acted = false;

            // 1) 진화 가능한 카드가 있으면 진화 (코스트를 지불하여 aiMemory가 음수로 내려갈 수 있음)
            foreach (var fieldDigimon in State.DummyOpponent.BattleArea.ToList())
            {
                var evoCandidate = State.DummyOpponent.Hand
                    .FirstOrDefault(c => c.Type == CardType.Digimon && c.EvoRequirementLevel == fieldDigimon.TopCard.Level);

                if (evoCandidate != null)
                {
                    State.DummyOpponent.Hand.Remove(evoCandidate);
                    int beforeMem = aiMemory;
                    string prev = fieldDigimon.TopCard.Name;
                    fieldDigimon.Evolve(evoCandidate);
                    aiMemory -= evoCandidate.EvoCost;
                    if (State.DummyOpponent.MainDeck.Count > 0)
                    {
                        State.DummyOpponent.Hand.Add(State.DummyOpponent.MainDeck[0]);
                        State.DummyOpponent.MainDeck.RemoveAt(0);
                    }
                    Log($"🤖 [AI 진화] [{prev}] -> [{evoCandidate.Name}(Lv.{evoCandidate.Level}, 코스트 {evoCandidate.EvoCost})] (AI 메모리: +{beforeMem} → {aiMemory})");
                    acted = true;
                    break;
                }
            }

            if (aiMemory < 0) break;

            // 2) 테이머가 패에 있고 필드에 테이머가 없으면 테이머 등장
            var tamerCandidate = State.DummyOpponent.Hand.FirstOrDefault(c => c.Type == CardType.Tamer);
            if (tamerCandidate != null && State.DummyOpponent.Tamers.Count < 2)
            {
                State.DummyOpponent.Hand.Remove(tamerCandidate);
                int beforeMem = aiMemory;
                State.DummyOpponent.Tamers.Add(tamerCandidate);
                aiMemory -= tamerCandidate.PlayCost;
                Log($"🤖 [AI 테이머 등장] [{tamerCandidate.Name}(코스트 {tamerCandidate.PlayCost})] 배치! (AI 메모리: +{beforeMem} → {aiMemory})");
                acted = true;
                if (aiMemory < 0) break;
            }

            // 3) 패에서 디지몬 등장 (메모리가 0 미만이 되어 플레이어에게 1~6 메모리가 넘어가도록 코스트 지불!)
            var playCandidate = State.DummyOpponent.Hand
                .Where(c => c.Type == CardType.Digimon && c.PlayCost <= aiMemory + 5)
                .OrderByDescending(c => c.Level <= 4 ? c.PlayCost : 0)
                .ThenBy(c => c.PlayCost)
                .FirstOrDefault();

            if (playCandidate != null && State.DummyOpponent.BattleArea.Count < 5)
            {
                State.DummyOpponent.Hand.Remove(playCandidate);
                int beforeMem = aiMemory;
                var summoned = new CardInstance(playCandidate, State.CurrentTurn);
                State.DummyOpponent.BattleArea.Add(summoned);
                aiMemory -= playCandidate.PlayCost;
                Log($"🤖 [AI 등장] [{playCandidate.Name}(Lv.{playCandidate.Level}, 코스트 {playCandidate.PlayCost})] 소환! (AI 메모리: +{beforeMem} → {aiMemory})");
                acted = true;
            }
        }

        if (aiMemory >= 0)
        {
            // AI가 더 이상 낼 카드가 없어 패스(Pass)를 선언한 경우 -> 공식 룰에 따라 상대(플레이어)에게 3메모리 제공
            aiMemory = -3;
            Log("🤖 [AI 턴 종료] AI가 더 이상 행동하지 않고 패스(Pass)를 선언하여 메모리가 -3(내 메모리 +3)으로 넘어옵니다.");
        }
        else
        {
            Log($"🤖 [AI 턴 종료] AI 메모리가 {aiMemory}(0 미만)이 되어 내 턴(시작 메모리 +{Math.Abs(aiMemory)})으로 넘어옵니다!");
        }

        return Math.Abs(aiMemory);
    }

    private (string LogMessage, string ShortSummary, bool AiAttackerRemoved) ResolvePlayerSecurityOptionAgainstAi(
        CardData myOption,
        CardInstance aiAttacker,
        ref int aiMemory)
    {
        switch (myOption.CardId)
        {
            case "ST1-16": // 가이아 포스: AI 공격 디지몬 즉시 소멸!
                DeleteOpponentDigimon(aiAttacker);
                return (
                    $"🔥 [내 시큐리티 가이아 포스 발동!] 내 시큐리티에서 [가이아 포스]가 오픈되어 공격해 온 AI의 [{aiAttacker.TopCard.Name}]을(를) 즉시 소멸시켰습니다!",
                    $"🔥 [시큐리티 가이아 포스] AI [{aiAttacker.TopCard.Name}] 소멸!",
                    true);

            case "ST2-16": // 코큐토스 브레스: AI 공격 디지몬 패로 바운스!
                if (State.DummyOpponent.BattleArea.Remove(aiAttacker))
                {
                    while (aiAttacker.EvolutionSources.Count > 0)
                        State.DummyOpponent.Trash.Add(aiAttacker.EvolutionSources.Pop());
                    State.DummyOpponent.Hand.Add(aiAttacker.TopCard);
                }
                return (
                    $"🌊 [내 시큐리티 코큐토스 브레스 발동!] 내 시큐리티에서 [코큐토스 브레스]가 오픈되어 공격해 온 AI의 [{aiAttacker.TopCard.Name}]을(를) 패로 되돌렸습니다!",
                    $"🌊 [시큐리티 코큐토스 브레스] AI [{aiAttacker.TopCard.Name}] 바운스!",
                    true);

            case "ST1-15": // 기가 디스트로이어: DP 4000 이하 AI 디지몬 최대 2체 소멸
            {
                var targets = State.DummyOpponent.BattleArea.Where(d => d.GetCurrentDP() <= 4000).Take(2).ToList();
                bool removedAttacker = false;
                foreach (var t in targets)
                {
                    if (ReferenceEquals(t, aiAttacker)) removedAttacker = true;
                    DeleteOpponentDigimon(t);
                }
                return (
                    $"🔥 [내 시큐리티 기가 디스트로이어 발동!] AI 필드의 DP 4000 이하 디지몬 {targets.Count}체를 소멸시켰습니다!",
                    $"🔥 [시큐리티 기가 디스트로이어] AI {targets.Count}체 소멸!",
                    removedAttacker);
            }

            case "ST2-13": // 해머 스파크: [시큐리티] 메모리를 +2 얻는다 (AI 메모리 -2 차감 -> 내 다음 턴 시작 메모리 증가!)
                aiMemory -= 2;
                return (
                    $"⚡ [내 시큐리티 해머 스파크 발동!] 내가 메모리를 +2 얻어 AI의 남은 메모리를 2 빼앗았습니다! (AI 현재 메모리: {aiMemory})",
                    "⚡ [시큐리티 해머 스파크] 메모리 +2 확보!",
                    false);

            case "ST2-14": // 소로우 블루: AI 디지몬 1체 레스트
            {
                var activeAi = State.DummyOpponent.BattleArea.FirstOrDefault(d => !d.IsSuspended);
                if (activeAi != null) activeAi.IsSuspended = true;
                return (
                    $"❄️ [내 시큐리티 소로우 블루 발동!] {(activeAi != null ? $"AI의 [{activeAi.TopCard.Name}]을(를) 강제 레스트시켜 추가 공격을 봉쇄했습니다!" : "레스트할 추가 AI 디지몬이 없습니다.")}",
                    "❄️ [시큐리티 소로우 블루] AI 디지몬 레스트!",
                    false);
            }

            case "ST1-13": // 섀도우 윙
            case "ST1-14": // 스타라이트 익스플로전
            {
                int dpBuff = myOption.CardId == "ST1-14" ? 7000 : 3000;
                foreach (var ally in State.Player.BattleArea)
                {
                    ally.TemporaryDpBonus += dpBuff;
                }
                return (
                    $"✨ [내 시큐리티 {myOption.Name} 발동!] 내 필드의 모든 디지몬 DP가 +{dpBuff:N0} 증가했습니다!",
                    $"✨ [시큐리티 {myOption.Name}] 아군 전체 DP +{dpBuff:N0}!",
                    false);
            }

            case "ST2-15": // 카이저 네일: 내 디지몬 진화원 1장 무료 등장
            {
                var allyWithSrc = State.Player.BattleArea.FirstOrDefault(d => d.EvolutionSources.Any(s => s.Type == CardType.Digimon));
                if (allyWithSrc != null)
                {
                    var temp = new Stack<CardData>();
                    CardData? extracted = null;
                    while (allyWithSrc.EvolutionSources.Count > 0)
                    {
                        var top = allyWithSrc.EvolutionSources.Pop();
                        if (extracted == null && top.Type == CardType.Digimon) extracted = top;
                        else temp.Push(top);
                    }
                    while (temp.Count > 0) allyWithSrc.EvolutionSources.Push(temp.Pop());
                    if (extracted != null)
                    {
                        State.Player.BattleArea.Add(new CardInstance(extracted, State.CurrentTurn));
                        RecalculateFieldBuffs();
                        return (
                            $"❄️ [내 시큐리티 카이저 네일 발동!] 내 진화원에서 [{extracted.Name}]이(가) 코스트 없이 배틀 에어리어에 등장했습니다!",
                            $"❄️ [시큐리티 카이저 네일] [{extracted.Name}] 무료 등장!",
                            false);
                    }
                }
                return (
                    "❄️ [내 시큐리티 카이저 네일] 꺼낼 진화원이 없어 트래시로 이동합니다.",
                    "❄️ [시큐리티 카이저 네일] 불발",
                    false);
            }

            default:
                return (
                    $"✨ [내 시큐리티 [{myOption.Name}] 발동!] 시큐리티 효과 처리 후 트래시로 이동합니다.",
                    $"✨ [시큐리티 {myOption.Name}] 발동",
                    false);
        }
    }
}
