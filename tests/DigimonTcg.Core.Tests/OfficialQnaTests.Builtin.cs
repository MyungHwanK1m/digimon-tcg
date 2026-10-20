using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>공식 카드 Q&A — 내장 120장 (STK-2 ~ STK-6, BT1 ~ BT5). 요약은 직접 쓴 것이고 Q&A 원문은 옮기지 않았다.</summary>
public partial class OfficialQnaTests
{
    /// <summary>"~할 수 있다"를 모두 거절하는 선택기 (나머지는 추천 답)</summary>
    private sealed class DeclineAll : IChoiceProvider
    {
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => false;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => optional ? null : recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
    }

    private static CardData Api(string id, string name, int level, int dp, CardColor color, string main = "", CardType type = CardType.Digimon)
    {
        var card = new CardData
        {
            CardId = id, Name = name, Type = type, Color = color, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewAiBattle(int memory = 5)
    {
        var (state, engine, cards) = NewMainPhase(memory);
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        return (state, engine, cards);
    }

    // ---------- 메모리가 부족해도 어택 (블로커 디지몬 [어택 시] 메모리 -2) ----------

    /// <summary>ST2-07 · ST3-07 · ST4-08 · ST5-08 · ST6-08 Q1 — 메모리 2 미만이어도 어택할 수 있고, 어택이 끝난 뒤 턴이 넘어간다</summary>
    [Theory]
    [InlineData("ST2-07")]
    [InlineData("ST3-07")]
    [InlineData("ST4-08")]
    [InlineData("ST5-08")]
    [InlineData("ST6-08")]
    public void Qna_BlockerLoseMemory_Q1_CanAttackWithLowMemory(string id)
    {
        var (state, engine, cards) = NewMainPhase(memory: 1);
        var digi = new CardInstance(cards[id], 1);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);                        // DP 1000
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        Assert.True(engine.AttackSecurityOrDirect(digi).CanExecute);
        Assert.Single(state.DummyOpponent.SecurityStack);
        Assert.Equal(3, state.CurrentTurn);
    }

    // ---------- BT1 ----------

    /// <summary>BT1-001 Q1 — 상대 디지몬에게 어택했을 때의 DP +1000은 시큐리티 디지몬과의 배틀에는 적용되지 않는다</summary>
    [Fact]
    public void Qna_BT1_001_Q1_NoBonusAgainstSecurityDigimon()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "BT1-001", "ST1-02");                               // DP 3000
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);                        // DP 3000 → 같으면 내 디지몬 소멸

        engine.AttackSecurityOrDirect(digi);

        Assert.DoesNotContain(digi, state.Player.BattleArea);
    }

    /// <summary>BT1-003 Q1 — 상대 육성 에어리어의 진화원 없는 디지몬은 조건에 들어가지 않는다</summary>
    [Fact]
    public void Qna_BT1_003_Q1_BreedingAreaIsNotReferenced()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "BT1-003", "ST2-02");
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.BattleArea.Add(Stack(cards, 1, "ST2-02", "ST2-06"));       // 진화원 있음
        state.DummyOpponent.BreedingArea = new CardInstance(cards["ST2-02"], 1);      // 진화원 없음 (육성)
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        int hand = state.Player.Hand.Count;

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(hand, state.Player.Hand.Count);
    }

    /// <summary>BT1-010 Q1 — 레드 이외의 테이머도 패에 넣을 수 있다</summary>
    [Fact]
    public void Qna_BT1_010_Q1_NonRedTamerCanBeAdded()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.MainDeck.Insert(0, cards["ST2-12"]);                              // 블루 테이머
        state.Player.Hand.Add(cards["BT1-010"]);

        engine.PlayCard(cards["BT1-010"]);

        Assert.Contains(cards["ST2-12"], state.Player.Hand);
    }

    /// <summary>BT1-021 · BT1-040 Q1 — [어택 시] 메모리 +3 뒤 그 디지몬이 소멸해도 턴 종료 시 -3은 발휘된다</summary>
    [Theory]
    [InlineData("BT1-021", "ST1-07", "ST1-03")]
    [InlineData("BT1-040", "ST2-06", "ST2-03")]
    public void Qna_GainThenLoseMemory_Q1_LossStillAppliesAfterDeletion(string id, string lv4, string lv3)
    {
        var (state, engine, cards) = NewAiBattle(memory: 0);
        var digi = Stack(cards, 1, lv3, lv4, id);                                      // DP 7000
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Clear();
        state.DummyOpponent.SecurityStack.Add(cards["ST3-11"]);                        // DP 10000 → 내 디지몬 소멸

        engine.AttackSecurityOrDirect(digi);
        Assert.DoesNotContain(digi, state.Player.BattleArea);
        Assert.Equal(3, state.MemoryGauge);

        engine.PassTurn();                                                             // 패스(상대 3) + 턴 종료 시 -3 → 상대 6

        Assert.Contains(state.ActionLogs, l => l.Contains("AI 상대 턴 시작") && l.Contains("+6"));
    }

    /// <summary>BT1-025 Q1 — 발휘하지 않은 옵션의 [시큐리티]는 그대로 트래시</summary>
    [Fact]
    public void Qna_BT1_025_Q1_IgnoredSecurityOptionGoesToTrash()
    {
        var (state, engine, cards) = NewMainPhase();
        var war = Stack(cards, 1, "ST1-03", "ST1-07", "ST1-09", "BT1-025");
        state.Player.BattleArea.Add(war);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-16"]);                        // 가이아 포스 (발휘되면 내 디지몬 소멸)

        engine.AttackSecurityOrDirect(war);

        Assert.Contains(war, state.Player.BattleArea);
        Assert.Contains(cards["ST1-16"], state.DummyOpponent.Trash);
    }

    /// <summary>BT1-034 Q1 — 진화원 없는 상대 ≪블로커≫는 이 디지몬을 블록할 수 없다</summary>
    [Fact]
    public void Qna_BT1_034_Q1_NoSourceBlockerCannotBlock()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST2-03", "BT1-034", "ST2-08");                     // 원뿔몬(BT1-034) 진화원
        state.Player.BattleArea.Add(digi);
        var blocker = new CardInstance(cards["ST2-07"], 1);                            // 진화원 없는 블로커
        state.DummyOpponent.BattleArea.Add(blocker);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.False(blocker.IsSuspended);
        Assert.Empty(state.DummyOpponent.SecurityStack);
    }

    /// <summary>BT1-048 Q1 — 덱이 3장 이하여도 가능한 만큼 공개해서 처리한다</summary>
    [Fact]
    public void Qna_BT1_048_Q1_WorksWithSmallDeck()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.MainDeck.Clear();
        state.Player.MainDeck.Add(cards["ST3-12"]);                                    // 옐로 테이머
        state.Player.MainDeck.Add(cards["ST1-02"]);
        state.Player.Hand.Add(cards["BT1-048"]);

        Assert.True(engine.PlayCard(cards["BT1-048"]).CanExecute);

        Assert.Contains(cards["ST3-12"], state.Player.Hand);
        Assert.Single(state.Player.MainDeck);
    }

    /// <summary>BT1-060 Q1 — 시큐리티 3장마다 +1000이므로 2장 이하면 보너스가 없다</summary>
    [Fact]
    public void Qna_BT1_060_Q1_NoBonusUnderThreeSecurity()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST3-02", "ST3-06", "BT1-060", "ST3-11");
        state.Player.BattleArea.Add(digi);
        state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.Player.SecurityStack.Add(cards["ST1-02"]);

        engine.RecalculateFieldBuffs();
        Assert.Equal(cards["ST3-11"].DP, digi.GetCurrentDP());

        state.Player.SecurityStack.Add(cards["ST1-02"]);
        engine.RecalculateFieldBuffs();
        Assert.Equal(cards["ST3-11"].DP + 1000, digi.GetCurrentDP());
    }

    /// <summary>BT1-067 Q1 — 그린 이외의 Lv.4 디지몬도 패에 넣을 수 있다</summary>
    [Fact]
    public void Qna_BT1_067_Q1_NonGreenLevel4CanBeAdded()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(cards["ST4-02"], 1));
        state.Player.MainDeck.Insert(0, cards["ST1-07"]);                              // 레드 Lv.4
        state.Player.Hand.Add(cards["BT1-067"]);

        engine.PlayCard(cards["BT1-067"]);

        Assert.Contains(cards["ST1-07"], state.Player.Hand);
    }

    /// <summary>BT1-074 Q1 — 그린 이외의 Lv.5 이상 디지몬도 패에 넣을 수 있다</summary>
    [Fact]
    public void Qna_BT1_074_Q1_NonGreenLevel5CanBeAdded()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = new CardInstance(cards["ST4-02"], 1);                              // 그린 Lv.3
        state.Player.BattleArea.Add(digi);
        state.Player.MainDeck.Insert(0, cards["ST1-09"]);                              // 레드 Lv.5
        state.Player.Hand.Add(cards["BT1-074"]);

        engine.EvolveDigimon(digi, cards["BT1-074"]);

        Assert.Contains(cards["ST1-09"], state.Player.Hand);
    }

    /// <summary>BT1-084 Q1 — 레드 또는 블루 Lv.6에서 진화할 수 있다</summary>
    [Fact]
    public void Qna_BT1_084_Q1_DigivolvesFromRedOrBlueLevel6()
    {
        var cards = Cards();
        Assert.True(ActionValidator.ValidateEvolution(new CardInstance(cards["ST1-11"], 1), cards["BT1-084"]).CanExecute);
        Assert.True(ActionValidator.ValidateEvolution(new CardInstance(cards["ST2-11"], 1), cards["BT1-084"]).CanExecute);
        Assert.False(ActionValidator.ValidateEvolution(new CardInstance(cards["ST3-11"], 1), cards["BT1-084"]).CanExecute);
    }

    // ---------- BT2 · BT3 · BT5 ----------

    /// <summary>BT2-020 Q1 — [어택 시] 효과로 파기한 상대 시큐리티는 [시큐리티] 효과를 발휘하지 않는다</summary>
    [Fact]
    public void Qna_BT2_020_Q1_TrashedSecurityDoesNotActivate()
    {
        var (state, engine, cards) = NewMainPhase();
        var gal = Stack(cards, 1, "BT2-009", "ST1-07", "ST1-09", "BT2-020");
        state.Player.BattleArea.Add(gal);
        for (int i = 0; i < 10; i++) state.DummyOpponent.Trash.Add(cards["ST2-02"]);  // 트래시 10장 → 1장 파기
        state.DummyOpponent.SecurityStack.Add(cards["ST1-16"]);                        // 파기될 카드 (가이아 포스)
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(gal);

        Assert.Contains(gal, state.Player.BattleArea);
        Assert.Contains(cards["ST1-16"], state.DummyOpponent.Trash);
    }

    /// <summary>BT2-021 Q1 — 이미 액티브인 디지몬이 "액티브로 한다"를 받아도 "액티브가 되었을 때"가 아니다</summary>
    [Fact]
    public void Qna_BT2_021_Q1_AlreadyActiveDoesNotTrigger()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = Stack(cards, 1, "BT2-021", "ST2-06", "ST2-08", "ST2-11");           // 액티브 상태
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["BT5-086"]);
        int hand = state.Player.Hand.Count;

        engine.EvolveDigimon(digi, cards["BT5-086"]);                                  // [진화 시] 이 디지몬을 액티브로

        Assert.Equal(hand, state.Player.Hand.Count);                                   // 진화 1드로우만 (브이몬 드로우 없음)
    }

    /// <summary>BT2-032 Q1 — 이 디지몬이 블루 테이머를 레스트시키는 효과가 아니다</summary>
    [Fact]
    public void Qna_BT2_032_Q1_DoesNotSuspendOwnTamer()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = Stack(cards, 1, "BT2-021", "ST2-06", "ST2-08");
        state.Player.BattleArea.Add(digi);
        state.Player.Tamers.Add(cards["ST2-12"]);
        state.Player.Hand.Add(cards["BT2-032"]);

        engine.EvolveDigimon(digi, cards["BT2-032"]);

        Assert.True(state.Player.HasActiveTamer(cards["ST2-12"]));
        Assert.DoesNotContain(engine.GetMainAbilities(state.Player), a => a.Card.CardId == "BT2-032");
    }

    /// <summary>BT2-039 Q1 — [어택 시] Lv.3 등장은 임의 (쓰지 않을 수 있다)</summary>
    [Fact]
    public void Qna_BT2_039_Q1_AttackEffectIsOptional()
    {
        var (state, engine, cards) = NewMainPhase();
        engine.PlayerChoices = new DeclineAll();
        var magna = Stack(cards, 1, "ST3-02", "ST3-06", "ST3-09", "BT2-039");
        state.Player.BattleArea.Add(magna);
        state.Player.Hand.Add(cards["ST3-02"]);                                        // 옐로 Lv.3
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(magna);

        Assert.Contains(cards["ST3-02"], state.Player.Hand);
        Assert.Single(state.Player.BattleArea);
    }

    /// <summary>BT2-059 Q1 — "같은 명칭"은 크리사리몬이 아니라 이 카드를 진화원으로 가진 디지몬의 이름</summary>
    [Fact]
    public void Qna_BT2_059_Q1_SameNameMeansEvolvedDigimon()
    {
        var (state, engine, cards) = NewMainPhase(memory: 8);
        var digi = Stack(cards, 1, "BT2-052", "BT2-059", "ST5-09");                    // 맨 위: 메탈그레이몬(흑)
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST1-09"]);                                        // 같은 이름 「메탈그레이몬」 (레드)

        engine.PlayCard(cards["ST1-09"]);

        Assert.Equal(8 - cards["ST1-09"].PlayCost + 1, state.MemoryGauge);
    }

    /// <summary>BT3-031 Q1 — 육성 에어리어에서의 진화에는 진화 코스트 -2가 적용되지 않는다</summary>
    [Fact]
    public void Qna_BT3_031_Q1_NoReductionInBreedingArea()
    {
        var (state, engine, cards) = NewMainPhase(memory: 8);
        var paildramon = Api("QNA-PAILDRA", "파일드라몬", 5, 7000, CardColor.Blue);
        state.Player.BreedingArea = new CardInstance(paildramon, 1);
        state.Player.Hand.Add(cards["BT3-031"]);

        engine.EvolveDigimon(state.Player.BreedingArea, cards["BT3-031"]);

        Assert.Equal(8 - cards["BT3-031"].EvoCost, state.MemoryGauge);
    }

    /// <summary>BT5-086 Q1 — ≪진격≫으로 먼저 어택한 뒤 "액티브로 한다"로 다시 액티브가 될 수 있다</summary>
    [Fact]
    public void Qna_BT5_086_Q1_BlitzThenUnsuspend()
    {
        var (state, engine, cards) = NewMainPhase(memory: 2);
        var digi = Stack(cards, 1, "ST1-03", "ST1-07", "ST1-09", "ST1-11");
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);
        state.Player.Hand.Add(cards["BT5-086"]);

        engine.EvolveDigimon(digi, cards["BT5-086"]);                                  // 메모리 2 - 4 = -2 → ≪진격≫
        Assert.Same(digi, state.BlitzAttacker);
        engine.AttackSecurityOrDirect(digi);

        Assert.False(digi.IsSuspended);
    }

    // ---------- STK-2 ----------

    /// <summary>ST2-08 Q1 — 진화원 없는 상대 디지몬이 1마리라도 있으면 시큐리티 어택 +1</summary>
    [Fact]
    public void Qna_ST2_08_Q1_OneNoSourceDigimonIsEnough()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST2-03", "ST2-08", "ST2-11");
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.BattleArea.Add(Stack(cards, 1, "ST2-02", "ST2-06"));
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-02"], 1));

        engine.RecalculateFieldBuffs();

        Assert.Equal(2, digi.GetSecurityAttackCount());
    }

    /// <summary>ST2-09 Q1 — 진화원이 1장뿐인 상대 디지몬에게도 쓸 수 있고 그 1장만 파기한다</summary>
    [Fact]
    public void Qna_ST2_09_Q1_TrashesTheOnlySource()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = Stack(cards, 1, "ST2-03", "ST2-06");
        state.Player.BattleArea.Add(digi);
        var enemy = Stack(cards, 1, "ST1-03", "ST1-07");                               // 진화원 1장
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST2-09"]);

        engine.EvolveDigimon(digi, cards["ST2-09"]);

        Assert.Empty(enemy.EvolutionSources);
        Assert.Contains(cards["ST1-03"], state.DummyOpponent.Trash);
    }

    /// <summary>ST2-11 Q1 — 액티브로 되돌아가는 것은 각 턴 첫 어택 1회뿐</summary>
    [Fact]
    public void Qna_ST2_11_Q1_OnlyOncePerTurn()
    {
        var (state, engine, cards) = NewMainPhase();
        var metal = Stack(cards, 1, "ST2-03", "ST2-06", "ST2-08", "ST2-11");
        state.Player.BattleArea.Add(metal);
        for (int i = 0; i < 3; i++) state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(metal);
        Assert.False(metal.IsSuspended);
        engine.AttackSecurityOrDirect(metal);
        Assert.True(metal.IsSuspended);
    }

    /// <summary>ST2-12 Q1 — 진화원 없는 상대 디지몬이 여러 마리여도 메모리 +1만</summary>
    [Fact]
    public void Qna_ST2_12_Q1_OnlyPlusOne()
    {
        var state = new GameState();
        var engine = new GoldfishEngine(state);
        var (main, egg) = StarterDecks.CreateSt2CocytusBluePreset();
        engine.StartNewGame(main, egg, seed: 11);
        engine.BeginGame();
        var cards = Cards();
        state.Player.Tamers.Add(cards["ST2-12"]);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST1-02"], 0));
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST1-04"], 0));

        engine.PassTurn();                                                             // 연습 모드: 기본 3

        Assert.Equal(4, state.MemoryGauge);
    }

    /// <summary>ST2-13 Q1 — [시큐리티] 메모리 +2로 상대 턴은 끝나지만, 그 어택(남은 체크)이 끝난 뒤에 교대한다</summary>
    [Fact]
    public void Qna_ST2_13_Q1_TurnEndsAfterTheAttackFinishes()
    {
        var (state, engine, cards) = NewAiBattle(memory: 1);
        state.Player.SecurityStack.Add(cards["ST2-13"]);                               // 해머 스파크
        state.Player.SecurityStack.Add(cards["ST3-04"]);
        state.Player.SecurityStack.Add(cards["ST3-04"]);
        state.DummyOpponent.BattleArea.Add(Stack(cards, 0, "ST1-03", "ST1-07", "ST1-09")); // 체크 2장 (그레이몬 진화원)
        var second = new CardInstance(cards["ST1-04"], 0);                             // 두 번째 어택 후보
        state.DummyOpponent.BattleArea.Add(second);
        state.Player.Hand.Add(cards["ST1-02"]);

        engine.PlayCard(cards["ST1-02"]);                                              // 1 - 2 = -1 → 상대 메모리 1로 시작

        Assert.Single(state.Player.SecurityStack);                                     // 2장 모두 체크됨
        Assert.False(second.IsSuspended);                                              // 다음 어택은 없음
    }

    /// <summary>ST2-14 Q1 — 소로 블루를 받은 레스트 상태 상대 디지몬도 어택할 수 있다</summary>
    [Fact]
    public void Qna_ST2_14_Q1_LockedSuspendedDigimonCanBeAttacked()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var mine = Stack(cards, 1, "ST2-03", "ST2-06");
        state.Player.BattleArea.Add(mine);
        var enemy = new CardInstance(cards["ST1-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST2-14"]);
        engine.PlayCard(cards["ST2-14"], enemy);
        enemy.IsSuspended = true;

        Assert.True(engine.AttackOpponentDigimon(mine, enemy).CanExecute);
        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    /// <summary>ST2-15 Q1 — 진화원의 디지타마(Lv.2)는 카이저 네일로 등장시킬 수 없다</summary>
    [Fact]
    public void Qna_ST2_15_Q1_DigiEggSourceCannotBePlayed()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var ally = Stack(cards, 1, "ST2-01", "ST2-02");                                // 진화원: 디지타마뿐
        state.Player.BattleArea.Add(ally);
        state.Player.Hand.Add(cards["ST2-15"]);

        engine.PlayCard(cards["ST2-15"], ally);

        Assert.Single(state.Player.BattleArea);
        Assert.Single(ally.EvolutionSources);
    }

    // ---------- STK-3 ----------

    /// <summary>ST3-01 · ST3-04 Q1 — 상대 디지몬이 DP 0으로 소멸하면 그 진화원을 가진 내 디지몬 전부가 발휘한다</summary>
    [Fact]
    public void Qna_ST3_04_Q1_AllHoldersTrigger()
    {
        var (state, engine, cards) = NewMainPhase(memory: 7);
        state.Player.BattleArea.Add(Stack(cards, 1, "ST3-04", "ST3-06"));
        state.Player.BattleArea.Add(Stack(cards, 1, "ST3-04", "ST3-05"));
        var enemy = new CardInstance(cards["ST2-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST3-16"]);                                        // 세븐 헤븐즈 (코스트 7)

        engine.PlayCard(cards["ST3-16"], enemy);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
        Assert.Equal(2, state.MemoryGauge);                                            // 7 - 7 + 1 + 1
    }

    /// <summary>ST3-01 Q1 — 같은 조건으로 토코몬 진화원을 가진 디지몬 전부가 DP +1000</summary>
    [Fact]
    public void Qna_ST3_01_Q1_AllHoldersTrigger()
    {
        var (state, engine, cards) = NewMainPhase(memory: 7);
        var a = Stack(cards, 1, "ST3-01", "ST3-02", "ST3-06");
        var b = Stack(cards, 1, "ST3-01", "ST3-02", "ST3-05");
        state.Player.BattleArea.Add(a);
        state.Player.BattleArea.Add(b);
        var enemy = new CardInstance(cards["ST2-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST3-16"]);
        int aBefore = a.GetCurrentDP(), bBefore = b.GetCurrentDP();

        engine.PlayCard(cards["ST3-16"], enemy);
        engine.RecalculateFieldBuffs();

        Assert.Equal(aBefore + 1000, a.GetCurrentDP());
        Assert.Equal(bBefore + 1000, b.GetCurrentDP());
    }

    /// <summary>ST3-05 Q1 — 시큐리티가 8장이어도 메모리 +1까지</summary>
    [Fact]
    public void Qna_ST3_05_Q1_OnlyPlusOne()
    {
        var (state, engine, cards) = NewMainPhase(memory: 3);
        var digi = Stack(cards, 1, "ST3-02", "ST3-05", "ST3-09");
        state.Player.BattleArea.Add(digi);
        for (int i = 0; i < 8; i++) state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(4, state.MemoryGauge);
    }

    /// <summary>ST3-08 · ST3-11 · ST3-16 Q1 — DP 마이너스로 DP 0이 된 디지몬은 소멸한다</summary>
    [Fact]
    public void Qna_ST3_08_Q1_MinusThousandDeletesDp1000()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST3-02", "ST3-06", "ST3-08", "ST3-11");
        state.Player.BattleArea.Add(digi);
        var enemy = new CardInstance(cards["ST3-04"], 1);                              // DP 1000
        state.DummyOpponent.BattleArea.Add(enemy);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void Qna_ST3_11_Q1_MinusFourThousandDeletesDp4000()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST3-02", "ST3-06", "ST3-09", "ST3-11");
        state.Player.BattleArea.Add(digi);
        var enemy = new CardInstance(cards["ST1-04"], 1);                              // DP 4000
        state.DummyOpponent.BattleArea.Add(enemy);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    [Fact]
    public void Qna_ST3_16_Q1_MinusTenThousandDeletesDp10000()
    {
        var (state, engine, cards) = NewMainPhase(memory: 7);
        state.Player.BattleArea.Add(new CardInstance(cards["ST3-02"], 1));
        var enemy = new CardInstance(cards["ST3-11"], 1);                              // DP 10000
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST3-16"]);

        engine.PlayCard(cards["ST3-16"], enemy);

        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    /// <summary>ST3-12 Q1 — 리키 여러 장이면 시큐리티 디지몬 DP +2000이 겹친다</summary>
    [Fact]
    public void Qna_ST3_12_Q1_MultipleTkStack()
    {
        var (state, engine, cards) = NewAiBattle(memory: 0);
        state.Player.Tamers.Add(cards["ST3-12"]);
        state.Player.Tamers.Add(cards["ST3-12"]);
        state.Player.SecurityStack.Add(cards["ST1-02"]);                               // DP 3000 + 4000 = 7000
        var attacker = new CardInstance(cards["ST2-08"], 0);                           // DP 7000
        state.DummyOpponent.BattleArea.Add(attacker);

        engine.PassTurn();

        Assert.DoesNotContain(attacker, state.DummyOpponent.BattleArea);               // 같으면 어택한 쪽 소멸
    }

    /// <summary>ST3-13 Q1 — 천국의 문 [시큐리티]가 같은 턴에 두 번 발휘되면 겹친다</summary>
    [Fact]
    public void Qna_ST3_13_Q1_TwoActivationsStack()
    {
        var (state, engine, cards) = NewAiBattle(memory: 0);
        state.Player.SecurityStack.Add(cards["ST3-13"]);
        state.Player.SecurityStack.Add(cards["ST3-13"]);
        state.Player.SecurityStack.Add(cards["ST2-02"]);                               // DP 3000 + 5000 + 5000 = 13000
        var attacker = new CardInstance(Api("QNA-SA2-12000", "테스트 공격수", 6, 12000, CardColor.Red,
            "＜Security A. +2＞ (This Digimon checks 2 additional security cards.)"), 0);
        state.DummyOpponent.BattleArea.Add(attacker);

        engine.PassTurn();

        Assert.DoesNotContain(attacker, state.DummyOpponent.BattleArea);
    }

    // ---------- STK-4 ----------

    /// <summary>ST4-04 · ST4-06 Q1 — 플레이어에게 어택했다가 블록당해 배틀해도 "상대 디지몬에게 어택" 보너스는 없다</summary>
    [Theory]
    [InlineData("ST4-04", "ST4-07", 6000)]
    [InlineData("ST4-06", "ST4-09", 7000)]
    public void Qna_ST4_04_Q1_NoBonusWhenBlocked(string source, string top, int topDp)
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = source == "ST4-04" ? Stack(cards, 1, source, top) : Stack(cards, 1, "ST4-02", source, top);
        state.Player.BattleArea.Add(digi);
        var blocker = new CardInstance(Api($"QNA-BLK-{topDp}", "테스트 블로커", 4, topDp, CardColor.Blue,
            "＜Blocker＞ (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)"), 1);
        state.DummyOpponent.BattleArea.Add(blocker);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.True(blocker.IsSuspended);
        Assert.DoesNotContain(digi, state.Player.BattleArea);                          // 같은 DP → 보너스 없이 소멸
    }

    /// <summary>ST4-11 Q1 — 진화원 효과로 파기한 상대 시큐리티는 [시큐리티] 효과를 발휘하지 않는다</summary>
    [Fact]
    public void Qna_ST4_11_Q1_TrashedSecurityDoesNotActivate()
    {
        var (state, engine, cards) = NewMainPhase();
        var digi = Stack(cards, 1, "ST4-03", "ST4-08", "ST4-11", "ST4-13");
        state.Player.BattleArea.Add(digi);
        var enemy = new CardInstance(cards["ST1-02"], 1) { IsSuspended = true };
        state.DummyOpponent.BattleArea.Add(enemy);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-16"]);                        // 파기될 카드
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackOpponentDigimon(digi, enemy);

        Assert.Contains(digi, state.Player.BattleArea);
        Assert.Contains(cards["ST1-16"], state.DummyOpponent.Trash);
    }

    /// <summary>ST4-12 Q1 — 로제몬 효과를 받은 레스트 상태 상대 디지몬도 어택해서 배틀할 수 있다</summary>
    [Fact]
    public void Qna_ST4_12_Q1_LockedSuspendedDigimonCanBeAttacked()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var rose = Stack(cards, 1, "ST4-02", "ST4-06", "ST4-10");
        state.Player.BattleArea.Add(rose);
        var attacker = new CardInstance(cards["ST1-07"], 1);
        state.Player.BattleArea.Add(attacker);
        var enemy = new CardInstance(cards["ST1-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST4-12"]);
        engine.EvolveDigimon(rose, cards["ST4-12"]);
        enemy.IsSuspended = true;

        Assert.True(engine.AttackOpponentDigimon(attacker, enemy).CanExecute);
        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
    }

    /// <summary>ST4-14 Q1 — 상대 디지몬이 ≪블로커≫로 레스트했을 때도 장한솔이 발휘한다</summary>
    [Fact]
    public void Qna_ST4_14_Q1_TriggersOnOpponentBlock()
    {
        var (state, engine, cards) = NewMainPhase(memory: 3);
        state.Player.Tamers.Add(cards["ST4-14"]);
        var digi = Stack(cards, 1, "ST4-03", "ST4-07");
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-07"], 1));     // 블로커
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(4, state.MemoryGauge);
        Assert.False(state.Player.HasActiveTamer(cards["ST4-14"]));
    }

    // ---------- STK-5 ----------

    /// <summary>ST5-04 · ST5-06 Q1 — 상대 턴 내내 상대 디지몬이 없어도 발휘한다</summary>
    [Theory]
    [InlineData("ST5-04", "ST5-07")]
    [InlineData("ST5-06", "ST5-09")]
    public void Qna_ST5_04_Q1_TriggersWithEmptyOpponentField(string source, string top)
    {
        var (state, engine, cards) = NewAiBattle(memory: 0);
        state.Player.BattleArea.Add(source == "ST5-06" ? Stack(cards, 1, "ST5-02", source, top) : Stack(cards, 1, source, top));
        int hand = state.Player.Hand.Count;

        engine.PassTurn();

        Assert.Equal(hand + 2, state.Player.Hand.Count);                               // 진화원 효과 1장 + 드로우 페이즈 1장
    }

    /// <summary>ST5-09 Q1 — [진화 시] ≪블로커≫를 자기 자신에게 줄 수 있다</summary>
    [Fact]
    public void Qna_ST5_09_Q1_CanGrantBlockerToItself()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var digi = Stack(cards, 1, "ST5-02", "ST5-07");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST5-09"]);

        engine.EvolveDigimon(digi, cards["ST5-09"]);
        engine.RecalculateFieldBuffs();

        Assert.True(digi.HasBlocker);
    }

    /// <summary>ST5-12 Q1 — [진화 시] ≪재기동≫을 자기 자신에게 줄 수 있다</summary>
    [Fact]
    public void Qna_ST5_12_Q1_CanGrantRebootToItself()
    {
        var (state, engine, cards) = NewAiBattle(memory: 5);
        var digi = Stack(cards, 1, "ST5-02", "ST5-07", "ST5-09");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST5-12"]);
        engine.EvolveDigimon(digi, cards["ST5-12"]);
        digi.IsSuspended = true;

        engine.PassTurn();

        Assert.Contains(state.ActionLogs, l => l.Contains("재기동") && l.Contains(cards["ST5-12"].Name));
    }

    /// <summary>ST5-14 Q1 — 블록한 디지몬이 아닌 다른 자신의 디지몬도 액티브로 할 수 있다</summary>
    [Fact]
    public void Qna_ST5_14_Q1_CanUnsuspendAnotherDigimon()
    {
        var (state, engine, cards) = NewAiBattle(memory: 0);
        state.Player.Tamers.Add(cards["ST5-14"]);
        state.Player.BattleArea.Add(new CardInstance(cards["ST5-08"], 1));            // 블로커 DP 5000
        var resting = new CardInstance(cards["ST5-07"], 1) { IsSuspended = true };
        state.Player.BattleArea.Add(resting);
        state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST2-08"], 0));     // DP 7000 어택 (블로커보다 강해서 AI가 어택한다)

        engine.PassTurn();

        Assert.False(resting.IsSuspended);
    }

    // ---------- STK-6 ----------

    /// <summary>ST6-01 Q1 — 효과로 덱이 0장이 되어도 다음 자신의 드로우 페이즈 전에는 지지 않는다</summary>
    [Fact]
    public void Qna_ST6_01_Q1_NoImmediateLossOnEmptyDeck()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.MainDeck.Clear();
        state.Player.MainDeck.Add(cards["ST6-02"]);
        var digi = Stack(cards, 1, "ST6-01", "ST6-02");
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-11"]);                        // 이기지 못해 소멸 → 2장 파기

        engine.AttackSecurityOrDirect(digi);

        Assert.Empty(state.Player.MainDeck);
        Assert.False(state.IsGameOver);
    }

    /// <summary>ST6-04 Q1 — 되돌릴 수 있는 것은 퍼플 · 사용 코스트 1 또는 7의 옵션 1장</summary>
    [Fact]
    public void Qna_ST6_04_Q1_OnlyPurpleCostOneOrSevenOption()
    {
        var (state, engine, cards) = NewMainPhase(memory: 8);
        state.Player.Trash.Add(cards["ST5-15"]);                                       // 블랙 · 코스트 4 → 불가
        state.Player.Trash.Add(cards["ST6-16"]);                                       // 퍼플 · 코스트 7 → 가능
        state.Player.Hand.Add(cards["ST6-04"]);

        engine.PlayCard(cards["ST6-04"]);

        Assert.Contains(cards["ST6-16"], state.Player.Hand);
        Assert.Contains(cards["ST5-15"], state.Player.Trash);
    }

    /// <summary>ST6-15 Q1 — 데스 클로의 효과를 모두 처리한 뒤에 소멸한 내 디지몬의 [소멸 시]를 발휘한다</summary>
    [Fact]
    public void Qna_ST6_15_Q1_OnDeletionAfterOptionResolves()
    {
        var (state, engine, cards) = NewMainPhase(memory: 5);
        var imp = new CardInstance(cards["BT2-068"], 1);                               // [소멸 시] 덱 3장 파기
        state.Player.BattleArea.Add(imp);
        var enemy = new CardInstance(cards["ST6-02"], 1);
        state.DummyOpponent.BattleArea.Add(enemy);
        state.Player.Hand.Add(cards["ST6-15"]);
        int logStart = state.ActionLogs.Count;

        engine.PlayCard(cards["ST6-15"], enemy);

        var logs = state.ActionLogs.Skip(logStart).ToList();
        int enemyGone = logs.FindIndex(l => l.Contains(cards["ST6-02"].Name) && l.Contains("소멸"));
        int millLog = logs.FindIndex(l => l.Contains("파기") && l.Contains("임프몬"));
        Assert.DoesNotContain(enemy, state.DummyOpponent.BattleArea);
        Assert.True(enemyGone >= 0 && millLog > enemyGone, string.Join(" | ", logs));
    }

    /// <summary>ST6-16 Q1 — 트래시에 Lv.3 · Lv.4 중 한쪽만 있어도 쓸 수 있고 있는 쪽만 등장</summary>
    [Fact]
    public void Qna_ST6_16_Q1_PlaysWhicheverExists()
    {
        var (state, engine, cards) = NewMainPhase(memory: 7);
        state.Player.BattleArea.Add(new CardInstance(cards["ST6-02"], 1));
        state.Player.Trash.Add(cards["ST6-05"]);                                       // 퍼플 Lv.3만
        state.Player.Hand.Add(cards["ST6-16"]);

        Assert.True(engine.PlayCard(cards["ST6-16"]).CanExecute);

        Assert.Contains(state.Player.BattleArea, d => d.TopCard.CardId == "ST6-05");
    }
}
