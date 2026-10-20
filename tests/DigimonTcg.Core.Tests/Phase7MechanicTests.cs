using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과 확장 7단계 — 특수 메커닉 · 상위 15개 밖 키워드</summary>
public class Phase7MechanicTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

    /// <summary>API 카드 (카드 번호는 테스트마다 달라야 한다 — 자동 스크립트가 번호로 캐시된다)</summary>
    private static CardData Api(string id, int level, int dp, string main = "", string inherited = "",
        CardColor color = CardColor.Red, CardType type = CardType.Digimon, int playCost = 3, int evoCost = 1)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = dp, PlayCost = playCost, EvoCost = evoCost,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main, InheritedEffectText = inherited,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 3, TurnSequence = 3 };
        return (state, new GoldfishEngine(state), Cards());
    }

    [Fact]
    public void ArmorPurge_ShouldTrashTheTopCardInsteadOfBeingDeleted()
    {
        var (state, engine, cards) = NewMainPhase();
        var under = Api("P7-AP-UNDER", 4, 5000);
        var top = Api("P7-AP", 5, 6000, main: "＜Armor Purge＞ (When this Digimon would be deleted, by trashing the top card of this Digimon, it isn't deleted.)");
        var mine = new CardInstance(under, 1);
        mine.Evolve(top);
        state.Player.BattleArea.Add(mine);
        state.DummyOpponent.BattleArea.Add(new CardInstance(cards["ST1-16"], 1));   // 아무 카드 (상대 필드가 비지 않게)
        var killer = Api("P7-AP-KILL", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] Delete 1 of your Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Contains(mine, state.Player.BattleArea);
        Assert.Equal("P7-AP-UNDER", mine.TopCard.CardId);
        Assert.Contains(top, state.Player.Trash);
    }

    [Fact]
    public void Fortitude_ShouldPlayTheCardAgainWhenItHadSources()
    {
        var (state, engine, cards) = NewMainPhase();
        var fort = Api("P7-FORT", 5, 7000, main: "＜Fortitude＞ (When this Digimon with digivolution cards is deleted, play this card without paying its memory cost.)");
        var inst = new CardInstance(Api("P7-FORT-UNDER", 4, 5000), 1);
        inst.Evolve(fort);
        state.DummyOpponent.BattleArea.Add(inst);
        var killer = Api("P7-FORT-KILL", 4, 5000, main: "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.DoesNotContain(inst, state.DummyOpponent.BattleArea);
        Assert.Contains(state.DummyOpponent.BattleArea, d => d.TopCard == fort && d.EvolutionSources.Count == 0);
        Assert.DoesNotContain(fort, state.DummyOpponent.Trash);
    }

    [Fact]
    public void Collision_ShouldMakeAnyActiveOpponentDigimonBlock()
    {
        var state = new GameState { CurrentStage = PracticeStage.Stage3_AiBattle };
        var attacker = new CardInstance(Api("P7-COLL", 5, 8000, main: "＜Collision＞ (During this Digimon's attack, all of your opponent's Digimon gain ＜Blocker＞, and your opponent blocks if possible.)"), 1);
        var plain = new CardInstance(Api("P7-COLL-DEF", 3, 2000), 1);   // ≪블로커≫ 없음
        state.DummyOpponent.BattleArea.Add(plain);
        state.DummyOpponent.SecurityStack.Add(Api("P7-COLL-SEC", 3, 1000));

        Assert.True(EffectRules.CanBlockAgainst(state, plain, attacker));
        Assert.Same(plain, EffectRules.ChooseOpponentBlocker(state, attacker, null));   // 지는 블록이라도 반드시 막는다
    }

    [Fact]
    public void Delay_ShouldStayInTheBattleAreaAndBeTrashedOnALaterTurnForItsEffect()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-02"], 1));   // 레드 (옵션 색 조건)
        var delay = Api("P7-DELAY", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] Gain 1 memory. Then, place this card in your battle area.\n[Main] ＜Delay＞ (Trash this card in your battle area to activate the effect below. You can't activate this effect the turn this card enters play.)\n・Gain 2 memory.");
        state.Player.Hand.Add(delay);

        Assert.True(engine.PlayCard(delay).CanExecute);
        Assert.Equal(4, state.MemoryGauge);
        Assert.Contains(state.Player.OptionsInPlay, p => p.Card == delay);
        Assert.DoesNotContain(delay, state.Player.Trash);
        Assert.DoesNotContain(engine.GetMainAbilities(state.Player), a => a.Card == delay);   // 놓은 턴에는 쓸 수 없다

        state.TurnSequence += 2;   // 다음 자신의 턴
        Assert.True(engine.ActivateMainAbility(null, delay).CanExecute);

        Assert.Equal(6, state.MemoryGauge);
        Assert.Empty(state.Player.OptionsInPlay);
        Assert.Contains(delay, state.Player.Trash);
    }

    [Fact]
    public void BlastDigivolve_ShouldDigivolveForFreeAtCounterTiming()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        var attacker = new CardInstance(Api("P7-BLAST-ATK", 5, 7000), 1);
        state.Player.BattleArea.Add(attacker);
        var aiBase = new CardInstance(Api("P7-BLAST-BASE", 4, 4000), 1);
        state.DummyOpponent.BattleArea.Add(aiBase);
        var blast = Api("P7-BLAST", 5, 9000, evoCost: 3,
            main: "[Hand] [Counter] ＜Blast Digivolve＞ (One of your Digimon may digivolve into this card without paying the cost.)");
        state.DummyOpponent.Hand.Add(blast);
        state.DummyOpponent.MainDeck.Add(cards["ST1-02"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);

        engine.AttackSecurityOrDirect(attacker);

        Assert.Equal("P7-BLAST", aiBase.TopCard.CardId);          // 어택 중 【카운터】로 코스트 없이 진화
        Assert.DoesNotContain(blast, state.DummyOpponent.Hand);
        Assert.Single(state.DummyOpponent.Hand);                  // 진화 보너스 드로우
    }

    [Fact]
    public void Save_ShouldPlaceTheDeletedCardUnderATamerAndLetItBePlayedFromThere()
    {
        var (state, engine, cards) = NewMainPhase();
        state.Player.Tamers.Add(Api("P7-SAVE-TAMER", 0, 0, type: CardType.Tamer));
        var saver = Api("P7-SAVE", 4, 5000, main: "[On Deletion] ＜Save＞ (You may place this card under one of your Tamers.)");
        var mine = new CardInstance(saver, 1);
        state.Player.BattleArea.Add(mine);
        var killer = Api("P7-SAVE-KILL", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Delete 1 of your Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Contains(saver, state.Player.UnderTamers);
        Assert.DoesNotContain(saver, state.Player.Trash);

        var caller = Api("P7-SAVE-CALL", 3, 3000, main: "[On Play] You may play 1 [P7-SAVE] from your hand or from under one of your Tamers without paying its memory cost.");
        state.Player.Hand.Add(caller);
        state.MemoryGauge = 5;
        engine.PlayCard(caller);

        Assert.Empty(state.Player.UnderTamers);
        Assert.Contains(state.Player.BattleArea, d => d.TopCard == saver);
    }

    [Fact]
    public void Iceclad_ShouldCompareDigivolutionCardCounts()
    {
        var (state, engine, cards) = NewMainPhase();
        var ice = new CardInstance(Api("P7-ICE-UNDER", 3, 2000), 1);
        ice.Evolve(Api("P7-ICE", 4, 3000, main: "＜Iceclad＞ (In battles against Digimon other than Security Digimon, this Digimon compares the number of digivolution cards instead of DP.)"));
        ice.Evolve(Api("P7-ICE2", 5, 4000, main: "＜Iceclad＞"));   // 진화원 2장
        state.Player.BattleArea.Add(ice);
        var big = new CardInstance(cards["ST1-02"], 1) { IsSuspended = true };   // DP 3000, 진화원 0장 — DP로는 지지 않지만 진화원 수로 진다
        big.Evolve(cards["ST1-11"]);   // 워그레이몬 DP 12000, 진화원 1장
        state.DummyOpponent.BattleArea.Add(big);

        engine.AttackOpponentDigimon(ice, big);

        Assert.Contains(ice, state.Player.BattleArea);          // 2장 > 1장
        Assert.DoesNotContain(big, state.DummyOpponent.BattleArea);
    }
}
