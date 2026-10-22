using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>"~ for its digivolution cost. When it would digivolve by this effect, reduce the cost by N." · "You may digivolve 1 of your other Digimon into ~"</summary>
public class DigivolveCostReductionTests
{
    private static CardData Card(string id, string main = "", CardType type = CardType.Digimon, int level = 3, int evoCost = 3, CardColor color = CardColor.Red)
    {
        var card = new CardData
        {
            CardId = id, Name = id, Type = type, Color = color, Level = level, DP = level * 1000, PlayCost = 3, EvoCost = evoCost,
            EvoRequirementLevel = level - 1, EvoRequirementColor = color, EffectText = main,
        };
        KeywordDictionary.ApplyDataKeywords(card);
        return card;
    }

    private static (GameState State, GoldfishEngine Engine, CardInstance Base) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, TurnSequence = 3 };
        state.MemoryGauge = 8;
        for (int i = 0; i < 6; i++)
        {
            state.Player.MainDeck.Add(Card("DC-FILL" + i, color: CardColor.White));
            state.DummyOpponent.MainDeck.Add(Card("DC-OFILL" + i, color: CardColor.White));
        }
        var baseDigimon = new CardInstance(Card("DC-BASE", level: 3), 1);
        state.Player.BattleArea.Add(baseDigimon);
        return (state, new GoldfishEngine(state), baseDigimon);
    }

    [Fact]
    public void OptionDigivolve_WithFollowingCostReduction()
    {
        var (state, engine, baseDigimon) = NewMainPhase();
        var evo = Card("DC-EVO", level: 4, evoCost: 3);
        state.Player.Hand.Add(evo);
        var opt = Card("DC-OPT", "[Main] 1 of your Digimon may digivolve into a red Digimon card in your hand for its digivolution cost. When it would digivolve by this effect, reduce the cost by 2.", CardType.Option, level: 0);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(opt).Level);
        state.Player.Hand.Add(opt);

        engine.PlayCard(opt);

        Assert.Same(evo, baseDigimon.TopCard);
        Assert.Equal(8 - 3 - (3 - 2), state.MemoryGauge);                               // 옵션 3 + 진화 코스트 3-2
    }

    [Fact]
    public void OnPlay_DigivolveOtherDigimon_SubjectAfterVerb()
    {
        var (state, engine, baseDigimon) = NewMainPhase();
        var greymon = Card("DC-Greymon", level: 4, evoCost: 2);
        greymon.Name = "Greymon";
        state.Player.Hand.Add(greymon);
        var digi = Card("DC-PLAYER", "[On Play] You may digivolve 1 of your other Digimon into a level 4 or lower Digimon card with [Greymon] in its name in your hand for its digivolution cost. When digivolving with this effect, reduce the digivolution cost by 1.", level: 3);
        Assert.Equal(EffectSupportLevel.Full, EffectSupport.For(digi).Level);
        state.Player.Hand.Add(digi);

        engine.PlayCard(digi);

        Assert.Same(greymon, baseDigimon.TopCard);                                       // 등장한 디지몬이 아니라 다른 디지몬이 진화
        Assert.Equal(8 - 3 - (2 - 1), state.MemoryGauge);
        Assert.Contains(state.Player.BattleArea, d => d.TopCard.CardId == "DC-PLAYER");
    }
}
