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
    public void WouldLeaveTheBattleArea_ShouldPreventLeavingByOpponentEffectsOnly()
    {
        var (state, engine, cards) = NewMainPhase();
        var guard = new CardInstance(Api("P7-LEAVE", 5, 7000,
            main: "[All Turns] When this Digimon would leave the battle area other than by your effects, by trashing the top card of your security stack, prevent it from leaving."), 1);
        state.DummyOpponent.BattleArea.Add(guard);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);
        state.DummyOpponent.SecurityStack.Add(cards["ST1-02"]);   // 시큐리티 3장 이상일 때 이 비용을 내는 것이 추천
        var killer = Api("P7-LEAVE-KILL", 4, 5000, main: "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Contains(guard, state.DummyOpponent.BattleArea);       // 상대(나)의 효과 — 막는다
        Assert.Equal(2, state.DummyOpponent.SecurityStack.Count);      // 비용: 시큐리티 위 1장 파기
    }

    [Fact]
    public void Fragment_ShouldTrashSourcesInsteadOfBeingDeleted()
    {
        var (state, engine, cards) = NewMainPhase();
        var frag = new CardInstance(Api("P7-FRAG-A", 3, 2000), 1);
        frag.Evolve(Api("P7-FRAG-B", 4, 4000));
        frag.Evolve(Api("P7-FRAG", 5, 6000, main: "＜Fragment (2)＞ (When this Digimon would be deleted, by trashing any 2 of its digivolution cards, it isn't deleted.)"));
        state.DummyOpponent.BattleArea.Add(frag);
        var killer = Api("P7-FRAG-KILL", 4, 5000, main: "[On Play] Delete 1 of your opponent's Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.Contains(frag, state.DummyOpponent.BattleArea);
        Assert.Empty(frag.EvolutionSources);
    }

    [Fact]
    public void Decode_ShouldPlayTheSpecifiedSourceWhenLeavingOutsideBattle()
    {
        var (state, engine, cards) = NewMainPhase();
        var source = Api("P7-DEC-SRC", 3, 3000);
        var dec = new CardInstance(source, 1);
        dec.Evolve(Api("P7-DEC", 4, 5000, main: "＜Decode ([P7-DEC-SRC])＞ (When this Digimon would leave the battle area other than in battle, you may play 1 [P7-DEC-SRC] from its digivolution cards without paying the cost.)"));
        state.Player.BattleArea.Add(dec);
        var killer = Api("P7-DEC-KILL", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Delete 1 of your Digimon.");
        state.Player.Hand.Add(killer);

        engine.PlayCard(killer);

        Assert.DoesNotContain(dec, state.Player.BattleArea);
        Assert.Contains(state.Player.BattleArea, d => d.TopCard == source);
    }

    [Fact]
    public void ZeroDpDeletion_ShouldActivateDecodeButNotFragment()
    {
        var (state, engine, cards) = NewMainPhase();
        var source = Api("P7-DPZ-SRC", 3, 3000);
        var dec = new CardInstance(source, 1);
        dec.Evolve(Api("P7-DPZ-FRAG", 4, 4000));
        dec.Evolve(Api("P7-DPZ", 5, 6000, main: "＜Decode ([P7-DPZ-SRC])＞\n＜Fragment (1)＞"));
        state.Player.BattleArea.Add(dec);
        var shrink = Api("P7-DPZ-OPT", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] 1 of your Digimon gets -10000 DP for the turn.");
        state.Player.Hand.Add(shrink);

        engine.PlayCard(shrink);

        Assert.DoesNotContain(dec, state.Player.BattleArea);                      // DP 0 소멸은 막지 못한다 (≪프래그먼트≫ 발휘 안 함)
        Assert.True(state.Player.BattleArea.Any(d => d.TopCard == source), string.Join(" | ", state.ActionLogs));   // ≪디코드≫는 발휘 (배틀 이외로 떠남)
    }

    [Fact]
    public void TimedDelay_TwoCopies_ShouldBothActivate()
    {
        var (state, engine, cards) = NewMainPhase();
        var delay = Api("P7-DELAY2", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Start of Your Turn] ＜Delay＞\n・Gain 1 memory.");
        state.Player.OptionsInPlay.Add(new PlacedOption { Card = delay, PlacedTurn = 0 });
        state.Player.OptionsInPlay.Add(new PlacedOption { Card = delay, PlacedTurn = 0 });

        engine.ExecuteActivePhase();

        Assert.Empty(state.Player.OptionsInPlay);
        Assert.Equal(2, state.Player.Trash.Count(c => c == delay));
        Assert.Equal(5, state.MemoryGauge);
    }

    [Fact]
    public void LastTamerLeaving_ShouldTrashCardsUnderTamers()
    {
        var (state, engine, cards) = NewMainPhase();
        var tamer = Api("P7-UT-TAMER", 0, 0, type: CardType.Tamer,
            main: "[Main] By returning this Tamer to the bottom of the deck, gain 1 memory.");
        state.Player.Tamers.Add(tamer);
        var saved = Api("P7-UT-SAVED", 4, 5000);
        state.Player.UnderTamers.Add(saved);

        Assert.True(engine.ActivateMainAbility(null, tamer).CanExecute);

        Assert.Empty(state.Player.UnderTamers);
        Assert.Contains(saved, state.Player.Trash);
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

    private const string DelayText = "＜Delay＞ (By trashing this card after the placing turn, activate the effect below.)\n・Gain 1 memory.";

    [Fact]
    public void Delay_IfCondition_ShouldBeCheckedBeforeTrashingTheCard()
    {
        var (state, engine, cards) = NewMainPhase();
        var delay = Api("P7-DIF", 0, 0, type: CardType.Option, playCost: 0,
            main: "[End of Opponent's Turn] If you have a Digimon with [P7-DIF-NAME] in its name, " + DelayText);
        state.Player.OptionsInPlay.Add(new PlacedOption { Card = delay, PlacedTurn = 1 });

        engine.RunEffects(EffectTrigger.EndOfOpponentsTurn, state.Player, null, delay, includeInherited: false);
        Assert.Single(state.Player.OptionsInPlay);          // 조건 불충족 — 파기하지 않는다
        Assert.Equal(3, state.MemoryGauge);

        state.Player.BattleArea.Add(new CardInstance(Api("P7-DIF-NAME", 4, 5000), 1));
        engine.RunEffects(EffectTrigger.EndOfOpponentsTurn, state.Player, null, delay, includeInherited: false);
        Assert.Empty(state.Player.OptionsInPlay);
        Assert.Contains(delay, state.Player.Trash);
        Assert.Equal(4, state.MemoryGauge);
    }

    [Fact]
    public void Delay_TurnPhaseTrigger_ShouldFireForAnOptionInTheBattleArea()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentStage = PracticeStage.Stage3_AiBattle;
        for (int i = 0; i < 3; i++)
        {
            state.Player.MainDeck.Add(cards["ST1-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST1-02"]);
            state.Player.SecurityStack.Add(cards["ST1-02"]);
        }
        var delay = Api("P7-DTURN", 0, 0, type: CardType.Option, playCost: 0, main: "[End of Opponent's Turn] " + DelayText);
        state.Player.OptionsInPlay.Add(new PlacedOption { Card = delay, PlacedTurn = 1 });

        engine.PassTurn();   // 상대 턴이 끝날 때 배틀 에어리어의 옵션에도 [상대의 턴 종료 시]

        Assert.Empty(state.Player.OptionsInPlay);
        Assert.Contains(delay, state.Player.Trash);
    }

    private const string StubbornText = "[All Turns] When this Digimon would be deleted, prevent that deletion.";

    [Fact]
    public void WouldBeDeleted_ShouldNotPreventDeletionByZeroDp()
    {
        var (state, engine, cards) = NewMainPhase();
        var stubborn = new CardInstance(Api("P7-ZDP", 4, 4000, main: StubbornText), 1);
        state.Player.BattleArea.Add(stubborn);

        Assert.True(engine.ApplyDpMinus(stubborn, 5000, "test"));   // DP 0 소멸은 룰 처리 — ≪회피≫처럼 막지 못한다
        Assert.DoesNotContain(stubborn, state.Player.BattleArea);

        var other = new CardInstance(Api("P7-ZDP-EFF", 4, 4000, main: StubbornText), 1);
        state.Player.BattleArea.Add(other);
        Assert.False(engine.DeleteFrom(state.Player, other));          // 효과로 소멸은 막는다
        Assert.Contains(other, state.Player.BattleArea);
    }

    [Fact]
    public void WouldBeDeleted_ShouldNotPreventDeletionAsACost()
    {
        var (state, engine, cards) = NewMainPhase();
        var scapegoat = new CardInstance(Api("P7-COST-SG", 5, 7000,
            main: "＜Scapegoat＞ (When this Digimon would be deleted other than by one of your effects, by deleting 1 of your other Digimon, prevent that deletion.)"), 1);
        var victim = new CardInstance(Api("P7-COST-V", 3, 2000, main: StubbornText), 1);
        state.DummyOpponent.BattleArea.Add(scapegoat);
        state.DummyOpponent.BattleArea.Add(victim);
        var killer = Api("P7-COST-KILL", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Delete 1 of your opponent's Digimon.");

        engine.RunEffects(EffectTrigger.Main, state.Player, null, killer, chosenTarget: scapegoat, includeInherited: false);

        Assert.Contains(scapegoat, state.DummyOpponent.BattleArea);     // ≪스케이프고트≫로 막고
        Assert.DoesNotContain(victim, state.DummyOpponent.BattleArea);  // 비용으로 소멸시킨 디지몬은 막지 못한다
    }

    /// <summary>ChooseCards에 정해 둔 번호를 답하는 선택기</summary>
    private sealed class PickIndex : IChoiceProvider
    {
        public int Index;
        public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended) => true;
        public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional) => recommended;
        public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended) => recommended;
        public IReadOnlyList<int> ChooseCards(GameState state, string question, CardChoiceZone zone, IReadOnlyList<CardData> options, int min, int max, IReadOnlyList<int> recommended) =>
            new[] { Index };
    }

    [Fact]
    public void PlayFromHandOrTrash_ShouldTakeTheChosenCopyFromItsOwnZone()
    {
        var (state, engine, cards) = NewMainPhase();
        engine.PlayerChoices = new PickIndex { Index = 1 };   // 후보: [0] 패의 1장, [1] 트래시의 1장 (같은 카드 객체)
        var copy = Api("P7-ZONE-AGU", 3, 2000);
        state.Player.Hand.Add(copy);
        state.Player.Trash.Add(copy);
        var caller = Api("P7-ZONE-OPT", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Play 1 [P7-ZONE-AGU] from your hand or trash without paying the cost.");

        engine.RunEffects(EffectTrigger.Main, state.Player, null, caller, includeInherited: false);

        Assert.Contains(copy, state.Player.Hand);           // 고른 트래시 쪽에서 나온다
        Assert.DoesNotContain(copy, state.Player.Trash);
        Assert.Contains(state.Player.BattleArea, d => d.TopCard == copy);
    }

    [Fact]
    public void PlaceFromHandOrTrash_ShouldTakeTheChosenCopyFromItsOwnZone()
    {
        var (state, engine, cards) = NewMainPhase();
        engine.PlayerChoices = new PickIndex { Index = 1 };
        state.Player.Tamers.Add(Api("P7-ZONE-TAMER", 0, 0, type: CardType.Tamer));
        var copy = Api("P7-ZONE-SAVE", 3, 2000);
        state.Player.Hand.Add(copy);
        state.Player.Trash.Add(copy);
        var target = new CardInstance(Api("P7-ZONE-TGT", 4, 5000), 1);
        state.Player.BattleArea.Add(target);
        var underTamer = Api("P7-ZONE-UT", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Place 1 [P7-ZONE-SAVE] from your hand or trash under one of your Tamers.");
        var underDigimon = Api("P7-ZONE-UD", 0, 0, type: CardType.Option, playCost: 0, main: "[Main] Place 1 [P7-ZONE-SAVE] from your hand or trash under 1 of your Digimon as its bottom digivolution card.");

        engine.RunEffects(EffectTrigger.Main, state.Player, null, underTamer, includeInherited: false);   // 테이머 아래
        Assert.Contains(copy, state.Player.Hand);
        Assert.DoesNotContain(copy, state.Player.Trash);
        Assert.Contains(copy, state.Player.UnderTamers);

        state.Player.Trash.Add(copy);
        engine.RunEffects(EffectTrigger.Main, state.Player, null, underDigimon, chosenTarget: target, includeInherited: false);   // 진화원 맨 아래
        Assert.Contains(copy, state.Player.Hand);
        Assert.DoesNotContain(copy, state.Player.Trash);
        Assert.Contains(copy, target.EvolutionSources);
    }
}
