using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Ai;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>AI 정책의 결정 (상태를 바꾸지 않는 판단만) — 개선 AI의 한 수 앞 평가 · 옵션 사용 · 기존 AI와 같은 결정</summary>
public class AiPolicyTests
{
    private static (GameState State, GoldfishEngine Engine, Dictionary<string, CardData> Cards) AiTurn()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.TurnEnd, TurnSequence = 6, CurrentStage = PracticeStage.Stage3_AiBattle };
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        for (int i = 0; i < 5; i++)
            state.Player.SecurityStack.Add(cards["ST2-02"]);
        return (state, new GoldfishEngine(state), cards);
    }

    private static CardInstance Field(PlayerZones zones, CardData card, bool suspended = false)
    {
        var inst = new CardInstance(card, currentTurn: 1) { IsSuspended = suspended };
        zones.BattleArea.Add(inst);
        return inst;
    }

    [Fact]
    public void Smart_ShouldGoForTheDirectAttack_WhenThePlayerHasNoSecurity()
    {
        var (state, engine, cards) = AiTurn();
        state.Player.SecurityStack.Clear();
        var attacker = Field(state.DummyOpponent, cards["ST1-02"]);   // DP 3000이어도 다이렉트 어택이면 승리

        var attack = new SmartAiPolicy().ChooseNextAttack(engine, memory: 2);

        Assert.Equal(new AiAttack(attacker, null), attack);
    }

    [Fact]
    public void Smart_ShouldNotAttackIntoABlockerThatWouldDeleteIt()
    {
        var (state, engine, cards) = AiTurn();
        Field(state.DummyOpponent, cards["ST1-02"]);                    // DP 3000
        Field(state.Player, cards["ST1-06"]);                           // 코어드라몬 ≪블로커≫ DP 6000 (액티브)

        Assert.Null(new SmartAiPolicy().ChooseNextAttack(engine, memory: 2));
        Assert.NotNull(new LegacyAiPolicy().ChooseNextAttack(engine, memory: 2));   // 기존 AI는 그대로 어택
    }

    [Fact]
    public void Smart_ShouldPreferAWinnableFightOverARiskySecurityAttack()
    {
        var (state, engine, cards) = AiTurn();
        var attacker = Field(state.DummyOpponent, cards["ST1-04"]);    // 드라코몬 DP 4000
        var restedWeak = Field(state.Player, cards["ST1-03"], suspended: true);   // 아구몬 DP 2000, 레스트

        var attack = new SmartAiPolicy().ChooseNextAttack(engine, memory: 2);

        Assert.Equal(new AiAttack(attacker, restedWeak), attack);
    }

    [Fact]
    public void Smart_ShouldUseARemovalOption_OnlyWhenItRemovesAKeyThreat()
    {
        var (state, engine, cards) = AiTurn();
        Field(state.DummyOpponent, cards["ST1-02"]);                    // 빨강 디지몬 DP 3000 (옵션 색 조건)
        state.DummyOpponent.Hand.Add(cards["ST1-15"]);                  // 기가 디스트로이어: DP 4000 이하 상대 2마리까지 소멸 (코스트 6)

        var policy = new SmartAiPolicy();
        Assert.Null(policy.ChooseOption(engine, memory: 6, beforeAttacks: true));   // 상대 필드가 비어 있음

        Field(state.Player, cards["ST1-03"]);                           // DP 2000 — 내 디지몬보다 약하다
        Assert.Null(policy.ChooseOption(engine, memory: 6, beforeAttacks: true));   // 꼭 없앨 상대가 아니면 메모리를 아낀다

        Field(state.Player, cards["ST1-04"]);                           // DP 4000 — 내 어느 디지몬보다 세다
        Assert.Equal(cards["ST1-15"], policy.ChooseOption(engine, memory: 6, beforeAttacks: true)?.Card);
        Assert.Null(policy.ChooseOption(engine, memory: 2, beforeAttacks: true));   // 상대에게 메모리를 넘기면서까지 쓰지 않는다
    }

    [Fact]
    public void Smart_WithFreeOptionUse_ShouldUseRemovalWheneverThereIsATarget()
    {
        var (state, engine, cards) = AiTurn();
        Field(state.DummyOpponent, cards["ST1-07"]);                    // DP 4000
        state.DummyOpponent.Hand.Add(cards["ST1-15"]);
        Field(state.Player, cards["ST1-03"]);
        Field(state.Player, cards["ST1-02"]);

        var free = new SmartAiPolicy(new SmartAiTuning { SituationalOptions = false });
        Assert.Equal(cards["ST1-15"], free.ChooseOption(engine, memory: 6, beforeAttacks: true)?.Card);
    }

    [Fact]
    public void Smart_ShouldRespectOptionColorRequirement()
    {
        var (state, engine, cards) = AiTurn();
        Field(state.DummyOpponent, cards["ST2-02"]);                    // 파란 디지몬만 있음
        state.DummyOpponent.Hand.Add(cards["ST1-15"]);                  // 빨강 옵션
        Field(state.Player, cards["ST1-03"]);

        Assert.Null(new SmartAiPolicy().ChooseOption(engine, memory: 8, beforeAttacks: true));
    }

    [Fact]
    public void Legacy_ShouldNeverUseOptions_AndAttackWithEveryoneByDp()
    {
        var (state, engine, cards) = AiTurn();
        var weak = Field(state.DummyOpponent, cards["ST1-02"]);
        var strong = Field(state.DummyOpponent, cards["ST1-07"]);
        state.DummyOpponent.Hand.Add(cards["ST1-15"]);
        Field(state.Player, cards["ST1-03"]);

        var legacy = new LegacyAiPolicy();
        Assert.Null(legacy.ChooseOption(engine, memory: 8, beforeAttacks: true));
        Assert.Equal(new AiAttack(strong, null), legacy.ChooseNextAttack(engine, memory: 0));
        strong.IsSuspended = true;
        Assert.Equal(new AiAttack(weak, null), legacy.ChooseNextAttack(engine, memory: 0));
    }

    [Fact]
    public void Smart_ShouldPreferEvolvingADigimonThatCanAttackThisTurn()
    {
        var (state, engine, cards) = AiTurn();
        var ready = Field(state.DummyOpponent, cards["ST1-03"]);        // 아구몬 Lv.3 (어택 가능)
        state.DummyOpponent.BreedingArea = new CardInstance(cards["ST1-01"], currentTurn: 2);   // 코로몬 Lv.2
        state.DummyOpponent.Hand.Add(cards["ST1-07"]);                  // 그레이몬 Lv.4 (아구몬 위)
        state.DummyOpponent.Hand.Add(cards["ST1-03"]);                  // 아구몬 Lv.3 (코로몬 위)

        var evo = new SmartAiPolicy().ChooseEvolution(engine, memory: 3, beforeAttacks: true);

        Assert.Equal(ready, evo?.Base);
        Assert.Equal(cards["ST1-07"], evo?.Card);
    }

    [Fact]
    public void Smart_ShouldNotTargetADigimonThatCannotBeAttacked()
    {
        var (state, engine, cards) = AiTurn();
        var attacker = Field(state.DummyOpponent, cards["ST1-04"]);    // 드라코몬 DP 4000
        var restedWeak = Field(state.Player, cards["ST1-03"], suspended: true);
        restedWeak.ActiveKeywords.Add(Keyword.CannotBeAttacked);       // "이 디지몬은 어택당하지 않는다"

        var attack = new SmartAiPolicy().ChooseNextAttack(engine, memory: 2);

        Assert.NotEqual(restedWeak, attack?.Target);
    }

    /// <summary>어택만 정해 둔 답을 내는 정책 (나머지는 기존 AI) — 엔진의 AI 어택 처리를 확인한다</summary>
    private sealed class ScriptedAttackPolicy : IAiPolicy
    {
        private readonly LegacyAiPolicy _legacy = new();
        public AiAttack? Next;
        public string Name => "정해 둔 어택";
        public int MaxMemoryHandOver => _legacy.MaxMemoryHandOver;
        public AiEvolution? ChooseEvolution(GoldfishEngine engine, int memory, bool beforeAttacks) => null;
        public AiOptionUse? ChooseOption(GoldfishEngine engine, int memory, bool beforeAttacks) => null;
        public bool ShouldUseMainAbility(GoldfishEngine engine, GoldfishEngine.MainAbility ability, int memory) => false;
        public CardData? ChooseTamerToPlay(GoldfishEngine engine, int memory) => null;
        public CardData? ChooseDigimonToPlay(GoldfishEngine engine, int memory) => null;

        public AiAttack? ChooseNextAttack(GoldfishEngine engine, int memory)
        {
            var next = Next;
            Next = null;
            return next;
        }
    }

    /// <summary>내 메인 페이즈 → 턴 종료하면 AI 턴이 정해 둔 어택 1번을 한다</summary>
    private static (GameState State, GoldfishEngine Engine, ScriptedAttackPolicy Policy, Dictionary<string, CardData> Cards) PlayerTurnBeforeAi()
    {
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, TurnSequence = 5, CurrentStage = PracticeStage.Stage3_AiBattle };
        var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
        var policy = new ScriptedAttackPolicy();
        for (int i = 0; i < 5; i++)
        {
            state.Player.MainDeck.Add(cards["ST1-02"]);
            state.DummyOpponent.MainDeck.Add(cards["ST1-02"]);
        }
        for (int i = 0; i < 3; i++)
            state.Player.SecurityStack.Add(cards["ST2-02"]);
        return (state, new GoldfishEngine(state) { AiPolicy = policy }, policy, cards);
    }

    [Fact]
    public void AiAttack_OnAnActiveDigimon_ShouldBecomeASecurityAttack()
    {
        var (state, engine, policy, cards) = PlayerTurnBeforeAi();
        var attacker = Field(state.DummyOpponent, cards["ST1-11"]);    // 워그레이몬 DP 12000
        var active = Field(state.Player, cards["ST1-03"]);             // 액티브 — 어택 대상이 될 수 없다
        policy.Next = new AiAttack(attacker, active);

        engine.PassTurn();

        Assert.Contains(active, state.Player.BattleArea);
        Assert.Equal(2, state.Player.SecurityStack.Count);
    }

    [Fact]
    public void AiAttack_OnADigimon_ShouldPassTheTargetToWhenAttackingEffects()
    {
        var (state, engine, policy, cards) = PlayerTurnBeforeAi();
        var striker = new CardData
        {
            CardId = "AIP-ATK-PLAYER", Name = "AIP-ATK-PLAYER", Type = CardType.Digimon, Color = CardColor.Red, Level = 6, DP = 12000,
            EffectText = "[All Turns] When this Digimon attacks a player, ＜Draw 1＞ (Draw 1 card from your deck.)",
        };
        var attacker = Field(state.DummyOpponent, striker);
        var rested = Field(state.Player, cards["ST1-03"], suspended: true);
        policy.Next = new AiAttack(attacker, rested);

        engine.PassTurn();

        Assert.DoesNotContain(rested, state.Player.BattleArea);        // 디지몬 어택 → 배틀
        Assert.Equal(4, state.DummyOpponent.MainDeck.Count);           // 드로우 페이즈 1장만 ("플레이어를 어택했을 때"가 아니다)
    }
}
