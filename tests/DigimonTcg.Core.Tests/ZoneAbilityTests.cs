using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>패 · 트래시의 카드로 쓰는 [메인] 기동 효과: "[Hand] [Main] ~" · "[Trash] [Main] ~"</summary>
public class ZoneAbilityTests
{
    private const string HandText = "[Hand] [Main] By paying 1 cost and placing this card as the bottom digivolution card of 1 of your Digimon that's level 3 or has the [Legend-Arms] trait, that Digimon gets +4000 DP for the turn.";

    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, int level = 3, int dp = 3000)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = CardColor.Red, Level = level, DP = dp, PlayCost = 3, EvoCost = 1,
            EvoRequirementLevel = level - 1, EvoRequirementColor = CardColor.Red, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 5;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("ZA-FILL" + i));
            state.DummyOpponent.MainDeck.Add(Card("ZA-OFILL" + i));
        }
        return (state, new GoldfishEngine(state));
    }

    [Fact]
    public void HandMain_PlacesThisCardAndBuffsThatDigimon()
    {
        var (state, engine) = NewMainPhase();
        var lv3 = new CardInstance(Card("ZA-LV3"), 1);
        state.Player.BattleArea.Add(lv3);
        var weapon = Card("ZA-WEAPON", HandText, level: 4);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(weapon).Level);
        state.Player.Hand.Add(weapon);

        Assert.Contains(engine.GetHandMainAbilities(state.Player), a => ReferenceEquals(a.Card, weapon) && a.Zone == MainAbilityZone.Hand);
        Assert.True(engine.ActivateHandEffect(weapon).CanExecute);

        Assert.Contains(weapon, lv3.EvolutionSources);
        Assert.DoesNotContain(weapon, state.Player.Hand);
        Assert.Equal(3000 + 4000, lv3.GetCurrentDP());
        Assert.Equal(5 - 1, state.MemoryGauge);
        Assert.False(engine.ActivateHandEffect(weapon).CanExecute);                       // 이제 패에 없다
    }

    [Fact]
    public void HandMain_NoValidDigimon_FailsWithoutChanges()
    {
        var (state, engine) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(Card("ZA-LV5", level: 5, dp: 7000), 1));   // 레벨 3도 [Legend-Arms]도 아니다
        var weapon = Card("ZA-WEAPON2", HandText, level: 4);
        state.Player.Hand.Add(weapon);

        var result = engine.ActivateHandEffect(weapon);

        Assert.False(result.CanExecute);
        Assert.Contains(weapon, state.Player.Hand);
        Assert.Equal(5, state.MemoryGauge);
    }

    [Fact]
    public void TrashMain_FromTrash()
    {
        var (state, engine) = NewMainPhase();
        var target = new CardInstance(Card("ZA-TARGET"), 1);
        state.Player.BattleArea.Add(target);
        var relic = Card("ZA-RELIC", "[Trash] [Main] By paying 1 cost and placing this card as the bottom digivolution card of 1 of your Digimon, gain 3 memory.", level: 4);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(relic).Level);
        state.Player.Trash.Add(relic);

        Assert.Single(engine.GetTrashMainAbilities(state.Player));
        Assert.True(engine.ActivateTrashEffect(relic).CanExecute);

        Assert.Contains(relic, target.EvolutionSources);
        Assert.DoesNotContain(relic, state.Player.Trash);
        Assert.Equal(5 - 1 + 3, state.MemoryGauge);
    }

    [Fact]
    public void HandMain_PlaceFromTrashThenDigivolveIgnoringRequirements()
    {
        var (state, engine) = NewMainPhase();
        state.Player.Tamers.Add(Card("ZA-ARISA", type: CardType.Tamer, level: 0));
        state.Player.Tamers[0].Name = "Arisa Kinosaki";
        var shoemon = new CardInstance(Card("ZA-SHOE"), 1);
        shoemon.TopCard.Name = "Shoemon";
        state.Player.BattleArea.Add(shoemon);
        var material = Card("ZA-SHOESHOE");
        material.Name = "ShoeShoemon";
        state.Player.Trash.Add(material);
        var evo = Card("ZA-EVO", "[Hand] [Main] If you have [Arisa Kinosaki], by placing 1 [ShoeShoemon] from your trash as any of your [Shoemon]'s bottom digivolution card, it digivolves into this card for a digivolution cost of 3, ignoring digivolution requirements.", level: 5);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(evo).Level);
        state.Player.Hand.Add(evo);

        Assert.True(engine.ActivateHandEffect(evo).CanExecute);

        Assert.Same(evo, shoemon.TopCard);                                               // Lv.3 → Lv.5 (진화 조건 무시)
        Assert.Contains(material, shoemon.EvolutionSources);
        Assert.Equal(5 - 3, state.MemoryGauge);
    }

    [Fact]
    public void TrashMain_PlayWithReducedCost()
    {
        var (state, engine) = NewMainPhase();
        var card = Card("ZA-RISE", "[Trash] [Main] If you have 4 or fewer cards in your hand, play this card with the play cost reduced by 2.", level: 4);
        card.PlayCost = 5;
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(card).Level);
        state.Player.Trash.Add(card);

        Assert.True(engine.ActivateTrashEffect(card).CanExecute);

        Assert.Contains(state.Player.BattleArea, d => ReferenceEquals(d.TopCard, card));
        Assert.DoesNotContain(card, state.Player.Trash);
        Assert.Equal(5 - 3, state.MemoryGauge);
    }

    [Fact]
    public void NotInMainPhase_Fails()
    {
        var (state, engine) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(Card("ZA-LV3B"), 1));
        var weapon = Card("ZA-WEAPON3", HandText, level: 4);
        state.Player.Hand.Add(weapon);
        state.CurrentPhase = Phase.Breeding;

        Assert.False(engine.ActivateHandEffect(weapon).CanExecute);
        Assert.Contains(weapon, state.Player.Hand);
    }

    [Fact]
    public void HandAbility_ListedWithZoneForAi()
    {
        var (state, engine) = NewMainPhase();
        var weapon = Card("ZA-WEAPON4", HandText, level: 4);
        state.DummyOpponent.Hand.Add(weapon);
        state.DummyOpponent.Hand.Add(weapon);                                              // 같은 카드 2장 = 2번 쓸 수 있다

        var abilities = engine.GetHandMainAbilities(state.DummyOpponent);

        Assert.Single(abilities);                                                         // 카드 종류마다 1줄
        Assert.Equal(MainAbilityZone.Hand, abilities[0].Zone);
        Assert.Null(abilities[0].Digimon);
    }
}
