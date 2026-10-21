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
    public void LinkByEffect_ShouldAttachTheCardAndFireGetsLinked()
    {
        var (state, engine, cards) = NewMainPhase();
        var linkable = Api("P7-LNK-CARD", 4, 4000);
        state.Player.Hand.Add(linkable);
        var host = Api("P7-LNK-HOST", 5, 7000,
            main: "[On Play] You may link 1 level 4 or lower Digimon card from your hand to this Digimon without paying the cost.\n[Your Turn] When this Digimon gets linked, gain 1 memory.");
        state.Player.Hand.Add(host);
        state.MemoryGauge = 5;

        engine.PlayCard(host);

        var inst = Assert.Single(state.Player.BattleArea);
        Assert.Contains(linkable, inst.LinkedCards);
        Assert.DoesNotContain(linkable, state.Player.Hand);
        Assert.Equal(5 - 3 + 1, state.MemoryGauge);
    }

    [Fact]
    public void LinkFromHand_ShouldCheckTheRequirementPayTheCostAndRunTheLinkEffect()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 4;
        var low = new CardInstance(Api("P7-LFH-LV2", 2, 1000), 1);
        var host = new CardInstance(Api("P7-LFH-HOST", 4, 5000), 1);
        state.Player.BattleArea.AddRange(new[] { low, host });
        var link = Api("P7-LFH-LINK", 3, 3000, inherited: "Link Requirements [Link] Lv.3 or higher: Cost 2 (Plug this card from the hand or battle area sideways into the specified Digimon in the battle area.)\nGain 1 memory.");
        state.Player.Hand.Add(link);

        Assert.DoesNotContain(low, engine.GetLinkTargets(link));
        Assert.False(engine.LinkFromHand(link, low).CanExecute);           // Lv.2 — 링크 조건 불충족
        Assert.True(engine.LinkFromHand(link, host).CanExecute);

        Assert.Contains(link, host.LinkedCards);
        Assert.Equal(4 - 2 + 1, state.MemoryGauge);                          // 코스트 2 → 【링크 시】 메모리 +1
    }

    [Fact]
    public void Link_ShouldTrashTheOldestWhenFullAndTrashLinksWhenTheDigimonLeaves()
    {
        var (state, engine, cards) = NewMainPhase();
        var host = new CardInstance(Api("P7-LNK2-HOST", 5, 7000), 1);
        state.Player.BattleArea.Add(host);
        var first = Api("P7-LNK2-A", 3, 1000);
        var second = Api("P7-LNK2-B", 3, 1000);

        engine.LinkCard(state.Player, host, first);
        engine.LinkCard(state.Player, host, second);
        Assert.Equal(new[] { second }, host.LinkedCards);                    // 최대 1장 — 먼저 것은 트래시
        Assert.Contains(first, state.Player.Trash);

        engine.DeleteFrom(state.Player, host);
        Assert.Contains(second, state.Player.Trash);
        Assert.Empty(host.LinkedCards);
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

    // ---- 육성 · 디지타마 ----

    [Fact]
    public void HatchAndMoveFromBreeding_ShouldFireHatchAndMoveTriggers()
    {
        var (state, engine, cards) = NewMainPhase();
        state.CurrentPhase = Phase.Breeding;
        state.MemoryGauge = 0;
        var tamer = Api("P7-BRD-TAMER", 0, 0, type: CardType.Tamer, playCost: 2,
            main: "[All Turns] When you hatch in the breeding area, gain 1 memory.\n[Your Turn] When one of your Digimon moves from the breeding area to the battle area, gain 1 memory.");
        state.Player.Tamers.Add(tamer);
        state.Player.DigitamaDeck.Push(Api("P7-BRD-EGG", 2, 0, type: CardType.Digitama));

        Assert.True(engine.HatchDigitama().CanExecute);
        Assert.NotNull(state.Player.BreedingArea);
        Assert.Equal(1, state.MemoryGauge);                                  // 부화 → +1

        // 다음 턴: Lv.3으로 자란 디지몬을 배틀 에어리어로
        state.Player.BreedingArea!.Evolve(Api("P7-BRD-LV3", 3, 3000));
        state.CurrentPhase = Phase.Breeding;
        state.BreedingActionTaken = false;
        Assert.True(engine.MoveFromBreedingArea().CanExecute);
        Assert.Null(state.Player.BreedingArea);
        Assert.Single(state.Player.BattleArea);
        Assert.Equal(2, state.MemoryGauge);                                  // 이동 → +1
    }

    [Fact]
    public void OpponentMovedFromBreeding_ShouldTriggerOnTheOtherSide()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 0;
        var watcher = new CardInstance(Api("P7-BRDO-W", 4, 5000,
            main: "When one of your opponent's level 3 Digimon is moved from their breeding area to their battle area, gain 2 memory."), 1);
        state.DummyOpponent.BattleArea.Add(watcher);
        state.Player.BreedingArea = new CardInstance(Api("P7-BRDO-LV3", 3, 3000), 1);

        engine.MoveBreedingToBattle(state.Player);

        Assert.Equal(-2, state.MemoryGauge);                                 // AI(상대 쪽)가 메모리 +2 = 플레이어 기준 -2
    }

    [Fact]
    public void PlayToBreedingArea_ShouldNotTriggerOnPlayAndNeedsAnEmptyArea()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 5;
        var baby = Api("P7-BRDP-BABY", 3, 2000, main: "[On Play] Gain 3 memory.");
        state.Player.Hand.Add(baby);
        var player = Api("P7-BRDP", 4, 5000,
            main: "[On Play] You may play 1 [P7-BRDP-BABY] from your hand to your empty breeding area without paying the cost.");
        state.Player.Hand.Add(player);

        engine.PlayCard(player);

        Assert.Same(baby, state.Player.BreedingArea?.TopCard);
        Assert.DoesNotContain(baby, state.Player.Hand);
        Assert.Equal(5 - 3, state.MemoryGauge);                               // 육성 에어리어 등장 — 【등장 시】 없음 (Q&A BT23-084)
    }

    [Fact]
    public void BreedingDigivolveFromTrash_AndDigiEggReturn_ShouldUseTheBreedingAreaAndEggDeck()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 3;
        state.Player.BreedingArea = new CardInstance(Api("P7-BRDE-LV3", 3, 3000), 1);
        var lv4 = Api("P7-BRDE-LV4", 4, 5000, main: "[When Digivolving] Gain 5 memory.");
        var egg = Api("P7-BRDE-EGG", 2, 0, type: CardType.Digitama);
        state.Player.Trash.AddRange(new[] { lv4, egg });
        state.Player.DigitamaDeck.Push(Api("P7-BRDE-TOP", 2, 0, type: CardType.Digitama));
        var opt = Api("P7-BRDE-OPT", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] 1 of your Digimon in the breeding area may digivolve into a level 4 Digimon card from your trash without paying the cost. Then, return 1 Digi-Egg card from your trash to the bottom of your Digi-Egg deck.");
        state.Player.Hand.Add(opt);

        engine.PlayCard(opt);

        Assert.Same(lv4, state.Player.BreedingArea!.TopCard);
        Assert.DoesNotContain(lv4, state.Player.Trash);
        Assert.Equal(3, state.MemoryGauge);                                  // 육성 에어리어 진화 — [진화 시] 없음
        Assert.Equal(2, state.Player.DigitamaDeck.Count);
        Assert.Same(egg, state.Player.DigitamaDeck.Last());                  // 디지타마 덱 맨 아래
    }

    [Fact]
    public void ReturningNonDigiEggCost_AndMoveToBreeding_ShouldWork()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 3;
        state.Player.Trash.AddRange(new[] { Api("P7-BRDC-A", 3, 1000), Api("P7-BRDC-B", 3, 1000), Api("P7-BRDC-EGG", 2, 0, type: CardType.Digitama) });
        state.Player.Trash.AddRange(new[] { Api("P7-BRDC-C", 3, 1000), Api("P7-BRDC-D", 3, 1000), Api("P7-BRDC-E", 3, 1000) });
        var opt = Api("P7-BRDC-OPT", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] By returning 2 non-Digi-Egg cards from your trash to the bottom of the deck, gain 2 memory.");
        state.Player.Hand.Add(opt);
        state.Player.Tamers.Add(Api("P7-BRDC-T", 0, 0, type: CardType.Tamer));   // 옵션 색 조건

        engine.PlayCard(opt);

        Assert.Equal(5, state.MemoryGauge);
        Assert.Equal(2, state.Player.MainDeck.Count);
        Assert.DoesNotContain(state.Player.MainDeck, c => c.Type == CardType.Digitama);

        var mover = new CardInstance(Api("P7-BRDC-MOVER", 4, 5000, main: "[End of Your Turn] This Digimon may move to the empty space in your breeding area."), 1);
        state.Player.BattleArea.Add(mover);
        Assert.True(engine.MoveBattleToBreeding(state.Player, mover));
        Assert.Same(mover, state.Player.BreedingArea);
        Assert.Empty(state.Player.BattleArea);
    }

    // ---- 앞면 시큐리티 ----

    [Fact]
    public void PlaceThisCardFaceUp_ShouldPutTheOptionFaceUpAtTheBottomAndFireThePlacedTrigger()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 3;
        var oldBottom = Api("P7-FU-OLD", 3, 1000);
        state.Player.SecurityStack.AddRange(new[] { Api("P7-FU-TOP", 3, 1000), oldBottom });
        state.Player.Tamers.Add(Api("P7-FU-TAMER", 0, 0, type: CardType.Tamer,
            main: "[All Turns] When cards are placed face up in your security stack, gain 1 memory."));
        var opt = Api("P7-FU-OPT", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] Add your bottom security card to the hand and place this card face up as the bottom security card.");
        state.Player.Hand.Add(opt);

        engine.PlayCard(opt);

        Assert.Contains(oldBottom, state.Player.Hand);
        var bottom = state.Player.SecurityStack.Last();
        Assert.Equal("P7-FU-OPT", bottom.CardId);
        Assert.True(GoldfishEngine.IsFaceUp(state.Player, bottom));
        Assert.False(GoldfishEngine.IsFaceUp(state.Player, state.Player.SecurityStack[0]));
        Assert.DoesNotContain(state.Player.Trash, c => c.CardId == "P7-FU-OPT");
        Assert.Equal(4, state.MemoryGauge);                                  // 앞면으로 놓였을 때 +1
    }

    [Fact]
    public void FlipAndCountFaceUp_ShouldOnlyFlipThatPositionEvenWithSharedCardObjects()
    {
        var (state, engine, cards) = NewMainPhase();
        var shared = Api("P7-FU2-SHARED", 3, 1000);
        state.DummyOpponent.SecurityStack.AddRange(new[] { shared, shared });   // 같은 객체 2장
        var flipper = Api("P7-FU2", 4, 5000, main: "[On Play] Flip your opponent's top face-down security card face up.");
        state.Player.Hand.Add(flipper);
        state.MemoryGauge = 5;

        engine.PlayCard(flipper);

        Assert.Single(GoldfishEngine.FaceUpCards(state.DummyOpponent));
        Assert.True(GoldfishEngine.IsFaceUp(state.DummyOpponent, state.DummyOpponent.SecurityStack[0]));
        Assert.False(GoldfishEngine.IsFaceUp(state.DummyOpponent, state.DummyOpponent.SecurityStack[1]));
    }

    [Fact]
    public void CheckingAFaceUpSecurityCard_ShouldFireTheCheckTriggerAndWasFaceUpCondition()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 3;
        var attacker = new CardInstance(Api("P7-FU3-ATK", 5, 9000,
            main: "[Your Turn] When this Digimon checks a face-up security card, gain 1 memory."), 1);
        state.Player.BattleArea.Add(attacker);
        var sec = Api("P7-FU3-SEC", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Security] If this card was face-up, gain 2 memory.");
        engine.PlaceSecurityFaceUp(state.DummyOpponent, sec, top: true);

        engine.AttackSecurityOrDirect(attacker);

        Assert.Empty(state.DummyOpponent.FaceUpSecurity.Where(c => state.DummyOpponent.SecurityStack.Contains(c)));
        Assert.Equal(3 + 1 - 2, state.MemoryGauge);                          // 체크 +1 (내 효과), 앞면이었으므로 상대(시큐리티 주인) 메모리 +2 → 플레이어 기준 -2
    }

    [Fact]
    public void FaceUpCosts_ShouldPlaceFromHandAndFlipFaceDown()
    {
        var (state, engine, cards) = NewMainPhase();
        state.MemoryGauge = 3;
        state.Player.Tamers.Add(Api("P7-FU4-T", 0, 0, type: CardType.Tamer));   // 옵션 색 조건
        var royal = Api("P7-FU4-ROYAL", 4, 5000);
        state.Player.Hand.Add(royal);
        state.Player.SecurityStack.Add(Api("P7-FU4-SEC", 3, 1000));
        var opt = Api("P7-FU4-OPT", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] By placing 1 [P7-FU4-ROYAL] from your hand face up as the bottom security card, gain 2 memory.");
        state.Player.Hand.Add(opt);

        engine.PlayCard(opt);

        Assert.Equal(5, state.MemoryGauge);
        Assert.Equal("P7-FU4-ROYAL", state.Player.SecurityStack.Last().CardId);
        Assert.Single(GoldfishEngine.FaceUpCards(state.Player));

        var opt2 = Api("P7-FU4-OPT2", 0, 0, type: CardType.Option, playCost: 0,
            main: "[Main] If you have a face-up security card, by flipping your top face-up security card face down, gain 1 memory.");
        state.Player.Hand.Add(opt2);
        engine.PlayCard(opt2);

        Assert.Equal(6, state.MemoryGauge);
        Assert.Empty(GoldfishEngine.FaceUpCards(state.Player));
    }

    // ---- 디지크로스 (조건은 공식 한글 문구에서 — 테스트는 지금 데이터에 테스트 카드를 더한 파일로) ----

    private static void WithKoreanCards(IEnumerable<KoreanCard> extra, System.Action run)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dtcg_p7ko_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var file = System.IO.Path.Combine(dir, "cards_ko.json");
        // 다른 테스트가 함께 돌아도 같은 데이터를 보도록 지금 데이터 + 테스트 카드
        System.IO.File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(KoreanCardData.All.Concat(extra).ToList()));
        try
        {
            KoreanCardData.Reload(file);
            run();
        }
        finally
        {
            KoreanCardData.Reload();
            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }

    private static KoreanCard Ko(string id, string name, string main = "") => new() { CardId = id, Name = name, MainText = main };

    [Fact]
    public void DigiXros_ShouldPlaceOneCardPerSlotFromHandAndReduceTheCost()
    {
        WithKoreanCards(new[]
        {
            Ko("P7-XR-HOST", "테스트메탈그레이몬", "디지크로스 -2:「테스트그레이몬」×「테스트메일버드라몬」 등장할 때, 패/배틀 에어리어에서 이 카드 아래에 놓을 수 있다. 1장마다 등장 코스트를 마이너스한다"),
            Ko("P7-XR-A", "테스트그레이몬"), Ko("P7-XR-A2", "테스트그레이몬"), Ko("P7-XR-B", "테스트메일버드라몬"),
        }, () =>
        {
            var (state, engine, cards) = NewMainPhase();
            state.MemoryGauge = 3;
            var a = Api("P7-XR-A", 4, 5000); var a2 = Api("P7-XR-A2", 4, 5000); var b = Api("P7-XR-B", 4, 4000);
            var host = Api("P7-XR-HOST", 5, 8000, playCost: 7, main: "[On Play] If DigiXrosing with 2 cards, gain 3 memory.");
            state.Player.Hand.AddRange(new[] { a, a2, b, host });

            Assert.True(GoldfishEngine.HasDigiXros(host));
            engine.PlayCard(host);

            var inst = Assert.Single(state.Player.BattleArea);
            Assert.Equal(2, inst.EvolutionSources.Count);                    // 칸마다 1장 — 테스트그레이몬은 1장만
            Assert.Contains(b, inst.EvolutionSources);
            Assert.Single(state.Player.Hand);                                // 남은 테스트그레이몬 1장
            Assert.Equal(3 - (7 - 2 * 2) + 3, state.MemoryGauge);           // 코스트 7 - 4 = 3, 2장 디지크로스 → +3
        });
    }

    [Fact]
    public void DigiXrosAlias_ShouldLetACardStandInForTheNamedCard()
    {
        WithKoreanCards(new[]
        {
            Ko("P7-XR2-HOST", "테스트호스트", "디지크로스 -1:「AliasTestmon」"),
            Ko("P7-XR2-ALIAS", "다른이름몬"),
        }, () =>
        {
            var (state, engine, cards) = NewMainPhase();
            state.MemoryGauge = 3;
            var alias = Api("P7-XR2-ALIAS", 3, 3000, main: "This card is also treated as [AliasTestmon] for a DigiXros.");
            var host = Api("P7-XR2-HOST", 4, 6000, playCost: 4);
            state.Player.Hand.AddRange(new[] { alias, host });

            engine.PlayCard(host);

            var inst = Assert.Single(state.Player.BattleArea);
            Assert.Contains(alias, inst.EvolutionSources);
            Assert.Equal(3 - 3, state.MemoryGauge);
        });
    }
}
