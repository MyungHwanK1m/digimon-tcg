using System.Collections.Generic;
using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>≪리커버리 +N (덱)≫ — 덱 위에서 N장을 시큐리티 맨 위에 놓는다 (내장 카드 · 실제 도감 카드 문장 · 조건 · 덱 부족 · AI)</summary>
public class RecoveryTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase(int memory = 10, int security = 3)
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = memory;   // 메모리는 최대 10 (세터에서 자름)
        var cards = Cards();
        for (int i = 0; i < security; i++) state.Player.SecurityStack.Add(cards["ST1-02"]);
        state.Player.MainDeck.Add(cards["ST3-12"]);                                    // 덱 맨 위 (리커버리로 시큐리티에 갈 카드)
        state.Player.MainDeck.Add(cards["ST3-13"]);
        for (int i = 0; i < 5; i++) state.Player.MainDeck.Add(cards["ST3-02"]);
        return (state, new GoldfishEngine(state), cards);
    }

    private static CardInstance Stack(Dictionary<string, CardData> cards, params string[] ids)
    {
        var inst = new CardInstance(cards[ids[0]], 1);
        foreach (var id in ids.Skip(1)) inst.Evolve(cards[id]);
        return inst;
    }

    /// <summary>실제 도감(data/cards_cache.json)의 카드 — 임시 폴더 사본으로 연다 (실제 캐시 파일은 건드리지 않음)</summary>
    private static CardData? RealCard(string id)
    {
        var src = Path.Combine(Path.GetDirectoryName(CardCatalogService.ResolveImagesDirectory())!, "cards_cache.json");
        if (!File.Exists(src)) return null;
        var dir = Path.Combine(Path.GetTempPath(), "dtcg_rec_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.Copy(src, Path.Combine(dir, "cards_cache.json"));
            return new CardCatalogService(dir).Catalog.FirstOrDefault(c => c.CardId == id);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---------- 내장 카드 ----------

    [Fact]
    public void Angewomon_WhenDigivolving_RecoversWhenSecurityThreeOrLess()
    {
        var (state, engine, cards) = NewMainPhase(security: 3);
        var digi = Stack(cards, "ST3-02", "ST3-06");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST3-09"]);

        engine.EvolveDigimon(digi, cards["ST3-09"]);

        Assert.Equal(4, state.Player.SecurityStack.Count);
        Assert.Same(cards["ST3-12"], state.Player.SecurityStack[0]);                  // 덱 맨 위 카드가 시큐리티 맨 위로
    }

    [Fact]
    public void Angewomon_WhenDigivolving_NoRecoveryWithFourSecurity()
    {
        var (state, engine, cards) = NewMainPhase(security: 4);
        var digi = Stack(cards, "ST3-02", "ST3-06");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["ST3-09"]);

        engine.EvolveDigimon(digi, cards["ST3-09"]);

        Assert.Equal(4, state.Player.SecurityStack.Count);
    }

    [Fact]
    public void MagnaAngemonBt1_OnPlay_RecoversOne()
    {
        var (state, engine, cards) = NewMainPhase(security: 5);
        state.Player.Hand.Add(cards["BT1-060"]);

        engine.PlayCard(cards["BT1-060"]);

        Assert.Equal(6, state.Player.SecurityStack.Count);                             // 조건 없음 — 5장이어도 +1
    }

    [Fact]
    public void MagnaAngemonBt1_Digivolving_IsNotOnPlay()
    {
        var (state, engine, cards) = NewMainPhase(security: 3);
        var digi = Stack(cards, "ST3-02", "ST3-06");
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(cards["BT1-060"]);

        engine.EvolveDigimon(digi, cards["BT1-060"]);

        Assert.Equal(3, state.Player.SecurityStack.Count);                             // [등장 시]는 진화로는 발휘하지 않음
    }

    [Theory]
    [InlineData(3, 5)]   // 3장 이하 → +2
    [InlineData(4, 4)]   // 4장 → 조건 불충족
    public void MagnadramonBt2_OnPlay_RecoversTwoWhenThreeOrLess(int security, int expected)
    {
        var (state, engine, cards) = NewMainPhase(memory: 20, security: security);
        state.Player.Hand.Add(cards["BT2-039"]);

        engine.PlayCard(cards["BT2-039"]);

        Assert.Equal(expected, state.Player.SecurityStack.Count);
    }

    [Fact]
    public void Recovery_WithShortDeck_PlacesWhatItCan_AndLosesOnlyAtDrawPhase()
    {
        var (state, engine, cards) = NewMainPhase(memory: 20, security: 1);
        state.Player.MainDeck.Clear();
        state.Player.MainDeck.Add(cards["ST3-12"]);                                    // 1장뿐
        state.Player.Hand.Add(cards["BT2-039"]);

        engine.PlayCard(cards["BT2-039"]);

        Assert.Equal(2, state.Player.SecurityStack.Count);                             // +2 중 덱에 있던 1장만
        Assert.Empty(state.Player.MainDeck);
        // 코스트 11 > 최대 메모리 10이라 턴이 끝나고, 다음 자신의 드로우 페이즈에서야 덱 0장으로 패배한다 (바로 지지 않음)
        Assert.True(state.IsGameOver);
        Assert.Contains("드로우 페이즈", state.GameOverReason);
    }

    // ---------- 실제 도감 카드 문장 (영어 효과 문장 파서) ----------

    /// <summary>BT1-107 [Main] ＜Recovery +1 (Deck)＞ (옵션)</summary>
    [Fact]
    public void RealOption_Bt1_107_MainRecoversOne()
    {
        var opt = RealCard("BT1-107");
        if (opt == null) return;                                                       // 도감 캐시가 없는 PC
        var (state, engine, cards) = NewMainPhase(security: 2);
        state.Player.BattleArea.Add(new CardInstance(cards["ST3-02"], 1));            // 옐로 (옵션 색 조건)
        state.Player.Hand.Add(opt);

        var res = engine.PlayCard(opt);

        Assert.True(res.CanExecute, res.Reason);
        Assert.Equal(3, state.Player.SecurityStack.Count);
    }

    /// <summary>BT10-040 [When Digivolving] If you have 2 or fewer security cards, ＜Recovery +1 (Deck)＞</summary>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    public void RealDigimon_Bt10_040_ConditionalRecovery(int security, int expected)
    {
        var card = RealCard("BT10-040");
        if (card == null) return;
        var (state, engine, cards) = NewMainPhase(security: security);
        var baseCard = new CardData
        {
            CardId = "REC-BASE-" + card.EvoRequirementLevel, Name = "Base", Type = CardType.Digimon,
            Color = card.EvoRequirementColor, Level = card.EvoRequirementLevel, DP = 3000,
        };
        var digi = new CardInstance(baseCard, 1);
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(card);

        var res = engine.EvolveDigimon(digi, card);

        Assert.True(res.CanExecute, res.Reason);
        Assert.Equal(expected, state.Player.SecurityStack.Count);
    }

    /// <summary>조건 표기 "N or fewer cards in your security stack" — 실제 카드 BT11-045 [진화 시]</summary>
    [Theory]
    [InlineData(5, 6)]
    [InlineData(6, 6)]
    public void RealDigimon_Bt11_045_CardsInSecurityStackCondition(int security, int expected)
    {
        var card = RealCard("BT11-045");
        if (card == null) return;
        var (state, engine, cards) = NewMainPhase(security: security);
        var digi = new CardInstance(new CardData
        {
            CardId = "REC-BASE45-" + card.EvoRequirementLevel, Name = "Base", Type = CardType.Digimon,
            Color = card.EvoRequirementColor, Level = card.EvoRequirementLevel, DP = 3000,
        }, 1);
        state.Player.BattleArea.Add(digi);
        state.Player.Hand.Add(card);

        Assert.True(engine.EvolveDigimon(digi, card).CanExecute);
        Assert.Equal(expected, state.Player.SecurityStack.Count);
    }

    /// <summary>원문 오타 "3 of fewer security cards" — 실제 카드 EX1-032 [어택 시] [턴에 1회]</summary>
    [Fact]
    public void RealDigimon_Ex1_032_TypoConditionStillWorks()
    {
        var card = RealCard("EX1-032");
        if (card == null) return;
        var (state, engine, cards) = NewMainPhase(security: 2);
        var digi = new CardInstance(card, 1);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);                        // DP 1000
        state.DummyOpponent.SecurityStack.Add(cards["ST3-04"]);

        engine.AttackSecurityOrDirect(digi);

        Assert.Equal(3, state.Player.SecurityStack.Count);
    }

    /// <summary>"＜Recovery +1 (Deck)＞ until there are 3 cards in your security stack" — 실제 카드 BT16-080 [소멸 시]</summary>
    [Fact]
    public void RealDigimon_Bt16_080_RecoverUntilThree()
    {
        var card = RealCard("BT16-080");
        if (card == null) return;
        var (state, engine, cards) = NewMainPhase(security: 0);
        var digi = new CardInstance(card, 1);
        state.Player.BattleArea.Add(digi);
        state.DummyOpponent.SecurityStack.Add(new CardData { CardId = "REC-SEC-20000", Name = "Big Wall", Type = CardType.Digimon, Level = 7, DP = 20000 });   // DP 13000 < 20000 → 소멸

        engine.AttackSecurityOrDirect(digi);

        Assert.DoesNotContain(digi, state.Player.BattleArea);
        Assert.Equal(3, state.Player.SecurityStack.Count);
    }

    // ---------- 상대 AI ----------

    [Fact]
    public void Ai_PlayingRecoveryCard_RecoversAiSecurity()
    {
        var (state, engine, cards) = NewMainPhase(memory: 0, security: 5);
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST2-02"]);
        for (int i = 0; i < 5; i++) state.DummyOpponent.MainDeck.Add(cards["ST3-02"]);
        var recoveryDigimon = new CardData
        {
            CardId = "REC-AI-001", Name = "Recovery Tester", Type = CardType.Digimon, Color = CardColor.Yellow, Level = 3, DP = 2000, PlayCost = 2,
            EvoRequirementLevel = 2, EvoRequirementColor = CardColor.Yellow,
            EffectText = "[On Play] ＜Recovery +1 (Deck)＞ (Place the top card of your deck on top of your security stack.)",
        };
        DigimonTcg.Core.Engine.Effects.KeywordDictionary.ApplyDataKeywords(recoveryDigimon);
        state.DummyOpponent.Hand.Add(recoveryDigimon);

        engine.PassTurn();                                                             // 상대(AI) 턴: 메모리 3 → 리커버리 디지몬 등장

        Assert.Contains(state.DummyOpponent.BattleArea, d => d.TopCard.CardId == "REC-AI-001");
        Assert.Equal(3, state.DummyOpponent.SecurityStack.Count);
    }
}
