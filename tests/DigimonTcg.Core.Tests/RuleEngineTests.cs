using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.DeckBuilder;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

public class RuleEngineTests
{
    [Fact]
    public void StarterDeckPresets_ShouldPassDeckValidationAndGoldenRatio()
    {
        foreach (var preset in StarterDecks.GetRecommendedDeckPresets())
        {
            var (main, digitama) = preset.DeckFactory();
            var (valid, errors) = DeckValidator.ValidateDeck(main, digitama);
            Assert.True(valid, $"{preset.Name}: " + string.Join(", ", errors));

            var ratio = DeckValidator.CalculateRatios(main, digitama);
            Assert.All(ratio.Items, item => Assert.Equal("적정", item.StatusText));
        }
    }

    [Fact]
    public void ActionValidator_ShouldBlockSummoningSicknessAndWrongPhase()
    {
        var state = new GameState { CurrentTurn = 1, CurrentPhase = Phase.Breeding, MemoryGauge = 3 };
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        // 육성 페이즈에는 등장 불가
        var playRes = ActionValidator.ValidatePlay(state, cards["ST1-03"]);
        Assert.False(playRes.CanExecute);

        // 메인 페이즈 전환 후 등장한 디지몬은 소환 멀미로 공격 불가
        state.CurrentPhase = Phase.Main;
        var engine = new GoldfishEngine(state);
        engine.PlayCard(cards["ST1-02"]);

        var playedDigimon = state.Player.BattleArea.Single();
        var attackRes = ActionValidator.ValidateAttack(state, playedDigimon);
        Assert.False(attackRes.CanExecute);
        Assert.Contains("소환 멀미", attackRes.Reason);
    }

    [Fact]
    public void GoldfishEngine_EvolutionShouldDrawBonusCardAndResetMemoryOnTurnEnd()
    {
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        engine.StartNewGame(main, digitama, seed: 10);
        engine.BeginGame();

        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);
        state.Player.Hand.Clear();
        state.Player.Hand.Add(cards["ST1-03"]); // 아구몬 (진화 0)
        state.Player.Hand.Add(cards["ST1-07"]); // 그레이몬 (진화 2)
        state.Player.Hand.Add(cards["ST1-09"]); // 메탈그레이몬 (진화 3)

        engine.HatchDigitama();
        Assert.Equal(Phase.Main, state.CurrentPhase);

        int handBefore = state.Player.Hand.Count;
        var evo1 = engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-03"]);
        Assert.True(evo1.CanExecute);
        Assert.Equal(handBefore, state.Player.Hand.Count);

        engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-07"]); // 메모리: 3 -> 1
        Assert.Equal(1, state.MemoryGauge);

        engine.EvolveDigimon(state.Player.BreedingArea!, cards["ST1-09"]); // 메모리: 1 - 3 = -2 -> 턴 종료 및 2턴 +3 리셋
        Assert.Equal(2, state.CurrentTurn);
        Assert.Equal(3, state.MemoryGauge);
        Assert.Equal(Phase.Breeding, state.CurrentPhase);
    }

    [Fact]
    public void SecurityBattle_ShouldDestroyAttackerWhenSecurityDpIsEqualOrHigher()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3 };
        var engine = new GoldfishEngine(state);
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        // 공격자: 아구몬 (DP 2000, 이전 턴 등장)
        var attacker = new CardInstance(cards["ST1-03"], currentTurn: 1);
        state.Player.BattleArea.Add(attacker);

        // 더미 시큐리티: 그레이몬 (DP 4000)
        state.DummyOpponent.SecurityStack.Add(cards["ST1-07"]);

        var result = engine.AttackSecurityOrDirect(attacker);
        Assert.True(result.CanExecute);
        Assert.Empty(state.Player.BattleArea); // 공격자 소멸 확인
        Assert.Empty(state.DummyOpponent.SecurityStack); // 시큐리티도 트래시로 이동 확인
        Assert.Contains(cards["ST1-03"], state.Player.Trash);
    }

    [Fact]
    public void DirectAttack_WhenSecurityIsEmpty_ShouldTriggerVictory()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, MemoryGauge = 3 };
        var engine = new GoldfishEngine(state);
        var cards = StarterDecks.GetGaiaRedSampleCards().ToDictionary(c => c.CardId);

        var attacker = new CardInstance(cards["ST1-11"], currentTurn: 2);
        state.Player.BattleArea.Add(attacker);
        state.DummyOpponent.SecurityStack.Clear(); // 시큐리티 0장

        var result = engine.AttackSecurityOrDirect(attacker);
        Assert.True(result.CanExecute);
        Assert.True(state.IsGameOver);
        Assert.True(state.IsVictory);
    }

    [Fact]
    public void CardCatalogService_ShouldFilterByColorLevelAndKeyword()
    {
        // 실제 data/cards_cache.json을 열거나 다시 쓰지 않도록 임시 폴더 (내장 카드만으로 검색)
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dtcg_test_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var catalog = new CardCatalogService(dir);
            var redLv6 = catalog.SearchCards(color: CardColor.Red, level: 6);
            Assert.NotEmpty(redLv6);
            Assert.Contains(redLv6, c => c.CardId == "ST1-11" && c.Name == "워그레이몬");

            var blueCards = catalog.SearchCards(color: CardColor.Blue);
            Assert.NotEmpty(blueCards);
            Assert.All(blueCards, c => Assert.Equal(CardColor.Blue, c.Color));
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void CardCatalogService_UnreadableCache_ShouldNeverBeOverwritten()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dtcg_test_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            string cachePath = System.IO.Path.Combine(dir, "cards_cache.json");
            const string broken = "[{ \"CardId\": \"BT1-001\", ";   // 반쯤 쓴 파일
            System.IO.File.WriteAllText(cachePath, broken);

            var catalog = new CardCatalogService(dir);

            Assert.NotNull(catalog.LastCacheLoadError);
            Assert.NotEmpty(catalog.Catalog);                 // 내장 카드로 실행
            Assert.False(catalog.SaveCache());                // 이후 저장(동기화 등)도 막는다
            Assert.Equal(broken, System.IO.File.ReadAllText(cachePath));
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ApiCardParser_ShouldMapCurrentApiFieldsAndOnlyLeadingKeywords()
    {
        // DigimonCard.io 현재 응답 형식 (id / main_effect / source_effect, 키워드는 전각 괄호)
        const string json = """
        [
          {"name":"Gargomon","type":"Digimon","id":"BT1-047","level":4,"play_cost":5,"evolution_cost":2,"evolution_color":"Green","evolution_level":3,
           "color":"Green","dp":5000,"main_effect":"＜Blocker＞ (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)","source_effect":""},
          {"name":"Lillymon","type":"Digimon","id":"BT1-079","level":5,"play_cost":7,"evolution_cost":2,"evolution_color":"Green","evolution_level":4,
           "color":"Green","dp":7000,"main_effect":"＜Security A. +2＞ (This Digimon checks 2 additional security cards.)","source_effect":""},
          {"name":"Garurumon","type":"Digimon","id":"BT1-030","level":4,"play_cost":4,"evolution_cost":2,"evolution_color":"Blue","evolution_level":3,
           "color":"Blue","dp":5000,"main_effect":"[Your Turn] This Digimon gains ＜Blocker＞.","source_effect":""},
          {"name":"Bebydomon","type":"Digi-Egg","id":"BT1-002","level":2,"play_cost":null,"evolution_cost":null,"evolution_level":null,"color":"Red","dp":null,
           "main_effect":"","source_effect":"[Your Turn] This Digimon gets +1000 DP."},
          {"name":"Izzy Izumi","type":"Tamer","id":"BT1-088","level":null,"play_cost":2,"evolution_cost":null,"color":"Green","dp":null,"main_effect":"[Main] ...","source_effect":""}
        ]
        """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var cards = doc.RootElement.EnumerateArray().Select(CardCatalogService.ParseApiCard).ToList();

        var blocker = cards[0]!;
        Assert.Equal("BT1-047", blocker.CardId);
        Assert.True(blocker.IsBlocker);
        Assert.Equal(3, blocker.EvoRequirementLevel);
        Assert.Equal(CardColor.Green, blocker.EvoRequirementColor);
        Assert.False(blocker.IsEffectImplemented);

        Assert.Equal(2, cards[1]!.SecurityAttackBonus);
        Assert.False(cards[2]!.IsBlocker);   // 조건부로 얻는 키워드는 자동 적용하지 않음

        var egg = cards[3]!;
        Assert.Equal(CardType.Digitama, egg.Type);
        Assert.Equal(2, egg.Level);
        Assert.Equal(0, egg.PlayCost);
        Assert.Equal(0, egg.DP);

        var tamer = cards[4]!;
        Assert.Equal(CardType.Tamer, tamer.Type);
        Assert.Equal(0, tamer.Level);
        Assert.Equal(2, tamer.PlayCost);
    }

    [Fact]
    public void Catalog_ShouldMarkBuiltInKoreanCardsAsEffectImplemented()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dtcg_test_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var catalog = new CardCatalogService(dir);
            Assert.NotEmpty(catalog.Catalog);
            Assert.All(catalog.Catalog, c => Assert.True(c.IsEffectImplemented));
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void MemoryHandOver_ShouldPassExactOverspendAndApplyTamerEffectsOnlyAtTurnStart()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var (oppMain, oppDigitama) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, digitama, seed: 3, stage: PracticeStage.Stage3_AiBattle,
            opponentMainDeck: oppMain, opponentDigitamaDeck: oppDigitama, opponentDeckName: "테스트 상대");
        engine.BeginGame(playerGoesFirst: true);

        // 선공 첫 턴: 메모리 0에서 3코스트 카드를 내면 -3 → 상대는 정확히 +3으로 시작
        engine.PassBreedingPhase();
        var lv3 = state.Player.Hand.First(c => c.Type == CardType.Digimon && c.PlayCost == 3);
        engine.PlayCard(lv3);
        Assert.Contains(state.ActionLogs, l => l.Contains("AI 상대 턴 시작") && l.Contains("넘겨받은 메모리 +3"));

        // AI 턴 종료 후 내 시작 메모리 = AI가 넘긴 값 (테이머가 없으면 보정 없음)
        var aiEnd = state.ActionLogs.Last(l => l.Contains("[AI 턴 종료]"));
        int myStart = state.MemoryGauge;
        if (state.Player.Tamers.Count == 0)
            Assert.Contains($"+{myStart}", aiEnd);
        Assert.Contains(state.ActionLogs, l => l.Contains($"상대가 넘겨준 메모리 +") && l.Contains($"내 시작 메모리: +{myStart}"));
    }

    [Fact]
    public void CoinToss_OpponentFirst_ShouldRunAiOpeningTurnThenPlayerDrawsOnTurnOne()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var (oppMain, oppDigitama) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, digitama, seed: 7, stage: PracticeStage.Stage3_AiBattle,
            opponentMainDeck: oppMain, opponentDigitamaDeck: oppDigitama, opponentDeckName: "테스트 상대");

        // 공식 룰: 양쪽 모두 빈 필드에서 시작
        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Null(state.DummyOpponent.BreedingArea);

        // 공식 룰: 메모리 게이지는 0에서 시작
        Assert.Equal(0, state.MemoryGauge);

        // 공식 룰 5-2: 게임 준비 단계 — 손패 5장만 받고, 시큐리티는 멀리건이 끝난 뒤에 놓는다
        Assert.Equal(Phase.Setup, state.CurrentPhase);
        Assert.Equal(5, state.Player.Hand.Count);
        Assert.Empty(state.Player.SecurityStack);
        Assert.Empty(state.DummyOpponent.SecurityStack);

        Assert.True(engine.DecideFirstPlayer(playerGoesFirst: false).CanExecute);
        Assert.True(engine.ExecuteMulligan().CanExecute);          // 후공도 게임 시작 전에 멀리건 1회
        Assert.False(engine.ExecuteMulligan().CanExecute);
        Assert.Equal(5, state.Player.Hand.Count);
        int oppDeckBefore = state.DummyOpponent.MainDeck.Count;

        var res = engine.BeginGame();

        Assert.True(res.CanExecute);
        Assert.False(state.PlayerGoesFirst);
        Assert.Equal(1, state.CurrentTurn);
        Assert.False(engine.ExecuteMulligan().CanExecute);          // 게임이 시작되면 멀리건 불가
        // 선공 AI는 첫 턴 드로우 없음 (덱 매수는 시큐리티 5장만큼 줄고, 드로우로는 줄지 않음)
        Assert.True(state.DummyOpponent.MainDeck.Count <= oppDeckBefore - 5);
        Assert.Contains(state.ActionLogs, l => l.Contains("상대 AI 선공"));
        // 선공 첫 턴 AI는 공격하지 않음 → 내 시큐리티 5장 그대로
        Assert.Equal(5, state.Player.SecurityStack.Count);
        Assert.Equal(5, state.DummyOpponent.SecurityStack.Count);
        // 상대 턴이 내 1턴보다 먼저 기록됨
        Assert.DoesNotContain(state.ActionLogs.TakeWhile(l => !l.Contains("AI 상대 턴 시작")), l => l.Contains("육성 페이즈 시작"));
        // UI 리플레이용 상대 행동 기록: 턴 시작 ~ 턴 종료, 공격 없음
        Assert.Equal(OpponentActionKind.TurnStart, state.OpponentTurnEvents.First().Kind);
        Assert.Equal(OpponentActionKind.TurnEnd, state.OpponentTurnEvents.Last().Kind);
        Assert.DoesNotContain(state.OpponentTurnEvents, ev => ev.Kind == OpponentActionKind.Attack);
        // AI도 첫 턴에 디지타마를 부화하고, 넘겨주는 메모리는 최대 3
        Assert.Contains(state.OpponentTurnEvents, ev => ev.Kind == OpponentActionKind.Breeding);
        Assert.True(state.DummyOpponent.BreedingArea != null || state.DummyOpponent.BattleArea.Count > 0);
        Assert.InRange(state.MemoryGauge, 1, 3);
        if (!state.IsGameOver)
        {
            // 후공 플레이어는 1턴째에도 드로우 → 손패 6장, 육성 페이즈에서 시작
            Assert.Equal(6, state.Player.Hand.Count);
            Assert.Equal(Phase.Breeding, state.CurrentPhase);
            Assert.True(state.MemoryGauge >= 0);
        }

        // 게임을 두 번 시작할 수는 없음
        Assert.False(engine.BeginGame().CanExecute);
    }

    // ===== 실제 카드 효과 (공식 한글판 텍스트 기준) =====

    private static (GameState State, GoldfishEngine Engine, System.Collections.Generic.Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3, TurnSequence = 3 };
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        return (state, new GoldfishEngine(state), cards);
    }

    [Fact]
    public void BuiltInCards_ShouldMatchRealCardNumbers()
    {
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        Assert.Equal(120, cards.Count);
        Assert.Equal("로제몬", cards["ST4-12"].Name);
        Assert.Equal(6, cards["ST4-12"].Level);
        Assert.Equal("헤라클레스캅테리몬", cards["ST4-13"].Name);
        Assert.Equal(CardType.Digimon, cards["ST4-13"].Type);
        Assert.Equal("장한솔", cards["ST4-14"].Name);
        Assert.Equal(CardType.Tamer, cards["ST4-14"].Type);
        Assert.Equal("제노사이드 어택", cards["ST5-16"].Name);
        Assert.Equal(5, cards["ST5-16"].PlayCost);
        Assert.Equal("SR", cards["ST1-11"].Rarity);
        Assert.True(cards["ST1-06"].IsBlocker);
        Assert.Equal(1, cards["ST1-07"].InheritedSecurityAttackBonus);
        Assert.Equal(1000, cards["ST1-03"].InheritedDpBonus);
    }

    /// <summary>AI 대전: 내 필드에 ≪블로커≫(코어드라몬)가 있고, AI의 버드라몬이 어택할 수 있는 상태</summary>
    private static (GameState State, GoldfishEngine Engine, CardInstance Blocker, CardInstance AiAttacker) NewAiAttackWithMyBlocker()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        engine.PlayerChoosesBlocks = true;
        engine.AiPolicy = new DigimonTcg.Core.Engine.Ai.LegacyAiPolicy();   // 블록 규칙 테스트라 블로커가 있어도 어택하는 기존 AI로 고정
        var blocker = new CardInstance(cards["ST1-06"], currentTurn: 1);       // ≪블로커≫ DP 6000
        state.Player.BattleArea.Add(blocker);
        state.Player.SecurityStack.Add(cards["ST2-02"]);
        state.Player.SecurityStack.Add(cards["ST2-02"]);
        state.Player.MainDeck.Add(cards["ST2-02"]);
        var aiAttacker = new CardInstance(cards["ST1-05"], currentTurn: 1);    // 버드라몬 DP 5000
        state.DummyOpponent.BattleArea.Add(aiAttacker);
        state.DummyOpponent.MainDeck.Add(cards["ST1-16"]);                    // AI 드로우용 (덱 0장이면 AI 패배)
        return (state, engine, blocker, aiAttacker);
    }

    [Fact]
    public void AiAttack_WithMyBlocker_ShouldWaitForMyBlockChoice()
    {
        var (state, engine, blocker, aiAttacker) = NewAiAttackWithMyBlocker();

        engine.PassTurn();

        Assert.NotNull(state.PendingBlock);
        Assert.Same(aiAttacker, state.PendingBlock!.Attacker);
        Assert.Contains(blocker, state.PendingBlock.Candidates);
        Assert.Equal(2, state.Player.SecurityStack.Count);       // 아직 아무것도 처리되지 않았다
        Assert.Equal(2, state.CurrentTurn);
        Assert.False(engine.PassTurn().CanExecute);              // 고르기 전에는 다른 행동 불가
    }

    [Fact]
    public void AiAttack_WhenIChooseToBlock_ShouldBattleBlockerAndKeepSecurity()
    {
        var (state, engine, blocker, aiAttacker) = NewAiAttackWithMyBlocker();
        engine.PassTurn();

        Assert.True(engine.ResolvePendingBlock(blocker).CanExecute);

        Assert.Null(state.PendingBlock);
        Assert.Equal(2, state.Player.SecurityStack.Count);
        Assert.DoesNotContain(aiAttacker, state.DummyOpponent.BattleArea);   // DP 6000 블로커가 5000 어택커를 격파
        Assert.Contains(blocker, state.Player.BattleArea);
        Assert.Equal(3, state.CurrentTurn);                                  // AI 턴이 끝나고 내 턴이 시작됐다
        Assert.Equal(Phase.Breeding, state.CurrentPhase);
    }

    [Fact]
    public void AiAttack_WhenIDeclineToBlock_ShouldCheckMySecurity()
    {
        var (state, engine, blocker, aiAttacker) = NewAiAttackWithMyBlocker();
        engine.PassTurn();

        Assert.True(engine.ResolvePendingBlock(null).CanExecute);

        Assert.Null(state.PendingBlock);
        Assert.Single(state.Player.SecurityStack);                          // 시큐리티 1장 체크됨
        Assert.Contains(aiAttacker, state.DummyOpponent.BattleArea);        // 쉬라몬 DP 3000 < 5000
        Assert.Contains(blocker, state.Player.BattleArea);
        Assert.Equal(3, state.CurrentTurn);
    }

    [Fact]
    public void AiAttack_WithoutPlayerChoice_ShouldAutoBlockWithStrongestBlocker()
    {
        var (state, engine, blocker, aiAttacker) = NewAiAttackWithMyBlocker();
        engine.PlayerChoosesBlocks = false;

        engine.PassTurn();

        Assert.Null(state.PendingBlock);
        Assert.Equal(2, state.Player.SecurityStack.Count);
        Assert.DoesNotContain(aiAttacker, state.DummyOpponent.BattleArea);
    }

    /// <summary>내 코어드라몬(DP 6000)이 시큐리티를 어택하고, 상대 필드에는 더 약한 ≪블로커≫가 있는 상태</summary>
    private static (GameState State, GoldfishEngine Engine, CardInstance EnemyBlocker) NewAttackIntoWeakEnemyBlocker(PracticeStage stage)
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = stage;
        var weakBlocker = cards.Values.Where(c => c.Type == CardType.Digimon && c.IsBlocker && c.DP < 6000).OrderBy(c => c.DP).First();
        var enemyBlocker = new CardInstance(weakBlocker, currentTurn: 1);
        state.DummyOpponent.BattleArea.Add(enemyBlocker);
        for (int i = 0; i < 3; i++)
            state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        var attacker = new CardInstance(cards["ST1-06"], currentTurn: 1);
        state.Player.BattleArea.Add(attacker);
        engine.AttackSecurityOrDirect(attacker);
        return (state, engine, enemyBlocker);
    }

    [Fact]
    public void AiBlocker_ShouldNotThrowItselfAwayWhenItWouldLose()
    {
        var (state, _, enemyBlocker) = NewAttackIntoWeakEnemyBlocker(PracticeStage.Stage3_AiBattle);

        Assert.Contains(enemyBlocker, state.DummyOpponent.BattleArea);
        Assert.False(enemyBlocker.IsSuspended);
        Assert.Equal(2, state.DummyOpponent.SecurityStack.Count);
    }

    [Fact]
    public void PracticeDummyBlocker_ShouldAlwaysBlock()
    {
        var (state, _, enemyBlocker) = NewAttackIntoWeakEnemyBlocker(PracticeStage.Stage2_BlockerDefense);

        Assert.DoesNotContain(enemyBlocker, state.DummyOpponent.BattleArea);
        Assert.Equal(3, state.DummyOpponent.SecurityStack.Count);
    }

    [Fact]
    public void AiBattle_AiWithEmptyDeck_ShouldLoseAtItsDrawPhase()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.Player.MainDeck.Add(cards["ST2-02"]);

        engine.PassTurn();

        Assert.True(state.IsGameOver);
        Assert.True(state.IsVictory);
    }

    [Fact]
    public void AiBattle_AttackPreview_ShouldNotRevealFaceDownSecurity()
    {
        var (state, _, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        var attacker = new CardInstance(cards["ST1-05"], currentTurn: 1);
        state.Player.BattleArea.Add(attacker);
        state.DummyOpponent.SecurityStack.Add(cards["ST2-07"]);   // 그리즈몬
        state.DummyOpponent.Trash.Add(cards["ST2-02"]);           // 트래시 장수는 시큐리티 번호와 무관

        var preview = ActionValidator.ValidateAttack(state, attacker, null);

        Assert.DoesNotContain(cards["ST2-07"].Name, preview.GuideMessage);
        Assert.Contains("시큐리티 #1", preview.GuideMessage);
    }

    [Fact]
    public void AiFirst_DigimonPlayedOnAiOpeningTurn_ShouldAttackOnNextAiTurn()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        var (oppMain, oppDigitama) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, digitama, seed: 11, stage: PracticeStage.Stage3_AiBattle,
            opponentMainDeck: oppMain, opponentDigitamaDeck: oppDigitama);
        engine.BeginGame(playerGoesFirst: false);

        // AI 선공 턴은 0번째 턴 → 그 턴에 낸 디지몬은 다음 AI 턴(내 1턴 뒤)에 공격할 수 있어야 한다
        Assert.Equal(1, state.CurrentTurn);
        Assert.Contains(state.ActionLogs, l => l.StartsWith("[T0 ") && l.Contains("AI 상대 턴 시작"));
        Assert.All(state.DummyOpponent.BattleArea, d => Assert.True(d.TurnPlayed < 1));

        // AI 선공 턴에 등장한 디지몬 (TurnPlayed 0)은 내 1턴이 끝난 뒤 AI 턴에 공격한다
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        var openingDigimon = new CardInstance(cards["ST2-02"], currentTurn: 0);
        state.DummyOpponent.BattleArea.Clear();
        state.DummyOpponent.BattleArea.Add(openingDigimon);
        state.DummyOpponent.MainDeck.Add(cards["ST2-02"]);
        int mySecurity = state.Player.SecurityStack.Count;
        state.CurrentPhase = Phase.Main;
        engine.PassTurn();
        Assert.True(openingDigimon.IsSuspended || !state.DummyOpponent.BattleArea.Contains(openingDigimon));   // 어택했다
        Assert.True(state.Player.SecurityStack.Count < mySecurity || state.ActionLogs.Any(l => l.Contains("블록")));
    }

    [Fact]
    public void Memory_ShouldStopAt10_AndCostsPast10CanStillBePaid()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 0;
        var bigCard = new CardData { CardId = "TEST-12", Name = "코스트 12", Type = CardType.Digimon, Level = 6, PlayCost = 12, DP = 12000 };
        state.Player.Hand.Add(bigCard);

        Assert.True(ActionValidator.ValidatePlay(state, bigCard).CanExecute);   // 공식 룰: 10을 넘는 코스트도 지불 가능

        state.MemoryGauge = 9;
        state.MemoryGauge += 5;
        Assert.Equal(10, state.MemoryGauge);
        state.MemoryGauge = -15;
        Assert.Equal(-10, state.MemoryGauge);
    }

    [Fact]
    public void SecurityTamerWithoutSecurityEffect_ShouldBeTrashedNotPlayed()
    {
        var (state, engine, cards) = NewMainPhase();
        var plainTamer = new CardData { CardId = "TEST-T1", Name = "효과 없는 테이머", Type = CardType.Tamer, PlayCost = 2, EffectText = "[자신의 턴] 아무 일도 없다." };
        state.DummyOpponent.SecurityStack.Add(plainTamer);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-12"]);                 // 신태일: [시큐리티] 등장
        var attacker = new CardInstance(cards["ST1-05"], currentTurn: 1) { TemporarySecurityAttackBonus = 1 };
        state.Player.BattleArea.Add(attacker);

        engine.AttackSecurityOrDirect(attacker);

        Assert.Contains(plainTamer, state.DummyOpponent.Trash);
        Assert.DoesNotContain(plainTamer, state.DummyOpponent.Tamers);
        Assert.Contains(cards["ST1-12"], state.DummyOpponent.Tamers);
        Assert.Contains(attacker, state.Player.BattleArea);                      // DP 0 배틀로 처리되지 않는다
    }

    [Fact]
    public void BlockerDigimon_WhenAttacking_ShouldLoseTwoMemory()
    {
        var (state, engine, cards) = NewMainPhase();
        var coredramon = new CardInstance(cards["ST1-06"], currentTurn: 1);   // ≪블로커≫ [어택 시] 메모리 -2
        state.Player.BattleArea.Add(coredramon);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);              // DP 3000 < 6000

        engine.AttackSecurityOrDirect(coredramon);

        Assert.Equal(1, state.MemoryGauge);
        Assert.Contains(coredramon, state.Player.BattleArea);
    }

    [Fact]
    public void TaiKamiya_ShouldGiveDpOnlyDuringOwnersTurn()
    {
        var (state, engine, cards) = NewMainPhase();
        var agumon = new CardInstance(cards["ST1-02"], currentTurn: 1);
        state.Player.BattleArea.Add(agumon);
        state.Player.Tamers.Add(cards["ST1-12"]);   // 신태일: [자신의 턴] 자신의 디지몬 전부 DP +1000

        engine.RecalculateFieldBuffs();
        Assert.Equal(4000, agumon.GetCurrentDP());

        agumon.IsOwnersTurn = false;                // 상대 턴에는 적용되지 않는다
        engine.RecalculateFieldBuffs();
        Assert.Equal(3000, agumon.GetCurrentDP());
    }

    [Fact]
    public void SorrowBlue_ShouldStopOpponentDigimonWithoutSourcesFromBlocking()
    {
        var (state, engine, cards) = NewMainPhase();
        var attacker = new CardInstance(cards["ST1-03"], currentTurn: 1);
        state.Player.BattleArea.Add(attacker);
        var enemyBlocker = new CardInstance(cards["ST2-07"], currentTurn: 1);   // 그리즈몬 ≪블로커≫, 진화원 없음
        state.DummyOpponent.BattleArea.Add(enemyBlocker);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);
        state.Player.Hand.Add(cards["ST2-14"]);
        state.Player.Tamers.Add(cards["ST2-12"]);                                // 옵션 색 조건: 블루 테이머

        engine.PlayCard(cards["ST2-14"], enemyBlocker);   // 소로 블루 (코스트 2) → 메모리 1
        Assert.False(EffectRules.CanBlock(state, enemyBlocker));

        engine.AttackSecurityOrDirect(attacker);
        Assert.Contains(enemyBlocker, state.DummyOpponent.BattleArea);   // 블록하지 못했다
        Assert.Empty(state.DummyOpponent.SecurityStack);                  // 시큐리티가 체크됨
    }

    [Fact]
    public void SevenHeavens_ShouldDeleteDigimonDroppedToZeroDp()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 7;
        var enemy = new CardInstance(cards["ST2-08"], currentTurn: 1);    // DP 7000
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Tamers.Add(cards["ST3-12"]);                         // 옵션 색 조건: 옐로 테이머
        state.Player.Hand.Add(cards["ST3-16"]);

        engine.PlayCard(cards["ST3-16"], enemy);

        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Contains(cards["ST2-08"], state.DummyOpponent.Trash);
    }

    [Fact]
    public void LaserEye_ShouldDeDigivolveButNotBelowLevelThree()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 4;
        var enemy = new CardInstance(cards["ST2-03"], currentTurn: 1);    // 파피몬 Lv.3
        enemy.Evolve(cards["ST2-06"]);                                     // → 가루몬 Lv.4
        state.DummyOpponent.BattleArea.Add(enemy);
        var lv3 = new CardInstance(cards["ST2-02"], currentTurn: 1);      // 진화원 없음 → 퇴화 불가
        state.DummyOpponent.BattleArea.Add(lv3);
        state.Player.Hand.Add(cards["ST5-15"]);
        state.Player.Tamers.Add(cards["ST5-14"]);                         // 옵션 색 조건: 블랙 테이머

        engine.PlayCard(cards["ST5-15"]);

        Assert.Equal("ST2-03", enemy.TopCard.CardId);
        Assert.Empty(enemy.EvolutionSources);
        Assert.Equal("ST2-02", lv3.TopCard.CardId);
        Assert.Contains(cards["ST2-06"], state.DummyOpponent.Trash);
    }

    [Fact]
    public void Retaliation_ShouldDeleteTheDigimonThatWonTheBattle()
    {
        var (state, engine, cards) = NewMainPhase();
        var devimon = new CardInstance(cards["BT2-074"], currentTurn: 1);   // ≪길동무≫ DP 4000
        state.Player.BattleArea.Add(devimon);
        var enemy = new CardInstance(cards["ST2-08"], currentTurn: 1) { IsSuspended = true };   // DP 7000
        state.DummyOpponent.BattleArea.Add(enemy);

        engine.AttackOpponentDigimon(devimon, enemy);

        Assert.Empty(state.Player.BattleArea);
        Assert.Empty(state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void Piercing_ShouldCheckSecurityAfterWinningBattle()
    {
        var (state, engine, cards) = NewMainPhase();
        var hercules = new CardInstance(cards["ST4-13"], currentTurn: 1);   // ≪관통≫ DP 12000
        state.Player.BattleArea.Add(hercules);
        var enemy = new CardInstance(cards["ST2-02"], currentTurn: 1) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(enemy);
        state.DummyOpponent.SecurityStack.Add(cards["ST2-04"]);

        engine.AttackOpponentDigimon(hercules, enemy);

        Assert.Empty(state.DummyOpponent.BattleArea);
        Assert.Empty(state.DummyOpponent.SecurityStack);
        Assert.Contains(hercules, state.Player.BattleArea);
    }

    /// <summary>6개 추천 덱끼리 무작위로 끝까지 대전시켜 카드 효과 처리 중 예외 · 무한 루프가 없는지 확인</summary>
    [Fact]
    public void AiBattle_RandomPlayoutsWithAllPresets_ShouldFinishWithoutErrors()
    {
        var presets = StarterDecks.GetRecommendedDeckPresets();
        int games = 0;
        int finished = 0, effectLogs = 0;
        for (int seed = 0; seed < 8; seed++)
        {
            foreach (var mine in presets)
            {
                foreach (var theirs in presets)
                {
                    var rng = new System.Random(seed * 100 + games);
                    var state = new GameState();
                    var engine = new GoldfishEngine(state) { PlayerChoosesBlocks = games % 2 == 0 };
                    var (main, egg) = mine.DeckFactory();
                    var (oMain, oEgg) = theirs.DeckFactory();
                    engine.StartNewGame(main, egg, seed: seed * 31 + games, stage: PracticeStage.Stage3_AiBattle,
                        opponentMainDeck: oMain, opponentDigitamaDeck: oEgg, opponentDeckName: theirs.Name);
                    Assert.True(engine.DecideFirstPlayer(playerGoesFirst: rng.Next(2) != 0).CanExecute);
                    if (rng.Next(3) == 0)
                        engine.ExecuteMulligan();
                    Assert.True(engine.BeginGame().CanExecute);

                    for (int step = 0; step < 400 && !state.IsGameOver && state.CurrentTurn < 30; step++)
                        PlayRandomAction(state, engine, rng);

                    if (state.IsGameOver) finished++;
                    effectLogs += state.ActionLogs.Count(l => l.Contains("[효과]") || l.Contains("[진화 시]") || l.Contains("[어택 시]") || l.Contains("진화원 효과"));
                    games++;
                }
            }
        }
        Assert.Equal(8 * presets.Count * presets.Count, games);
        Assert.True(finished > games / 2, $"끝난 게임 {finished}/{games}");
        Assert.True(effectLogs > games, $"효과 발동 로그 {effectLogs}");
    }

    private static void PlayRandomAction(GameState state, GoldfishEngine engine, System.Random rng)
    {
        if (state.PendingBlock is { } pending)
        {
            int pick = rng.Next(pending.Candidates.Count + 1);
            Assert.True(engine.ResolvePendingBlock(pick < pending.Candidates.Count ? pending.Candidates[pick] : null).CanExecute);
            return;
        }

        if (state.CurrentPhase == Phase.Breeding)
        {
            if (state.Player.BreedingArea == null && state.Player.DigitamaDeck.Count > 0) engine.HatchDigitama();
            else if (state.Player.BreedingArea is { } b && b.TopCard.Level >= 3) engine.MoveFromBreedingArea();
            else engine.PassBreedingPhase();
            return;
        }

        int roll = rng.Next(10);
        var hand = state.Player.Hand.ToList();
        var field = state.Player.BattleArea.ToList();
        if (state.Player.BreedingArea != null) field.Add(state.Player.BreedingArea);

        if (roll < 3 && hand.Count > 0)
        {
            var card = hand[rng.Next(hand.Count)];
            if (card.Type == CardType.Digimon && field.Count > 0 && rng.Next(2) == 0)
            {
                var target = field[rng.Next(field.Count)];
                engine.EvolveDigimon(target, card);
            }
            else if (card.Type != CardType.Digitama)
            {
                var targets = state.Player.BattleArea.Concat(state.DummyOpponent.BattleArea).ToList();
                engine.PlayCard(card, targets.Count > 0 && rng.Next(2) == 0 ? targets[rng.Next(targets.Count)] : null);
            }
            return;
        }

        if (roll < 8)
        {
            var attacker = state.Player.BattleArea.FirstOrDefault(d => !d.IsSuspended && d.TurnPlayed < state.CurrentTurn);
            if (attacker != null)
            {
                var rested = state.DummyOpponent.BattleArea.FirstOrDefault(d => d.IsSuspended);
                if (rested != null && rng.Next(2) == 0) engine.AttackOpponentDigimon(attacker, rested);
                else engine.AttackSecurityOrDirect(attacker);
                return;
            }
        }

        engine.PassTurn();
    }

    [Fact]
    public void Matt_ShouldGainMemoryOnlyWhenOpponentHasDigimonWithoutSources()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, digitama) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, digitama, seed: 5);   // 연습 모드: 매 턴 3
        engine.BeginGame();
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        state.Player.Tamers.Add(cards["ST2-12"]);

        engine.PassTurn();
        Assert.Equal(3, state.MemoryGauge);              // 상대 필드가 비어 있으면 보정 없음

        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST1-02"], state.CurrentTurn));
        engine.PassTurn();
        Assert.Equal(4, state.MemoryGauge);              // 진화원 없는 상대 디지몬 → +1
    }
}
