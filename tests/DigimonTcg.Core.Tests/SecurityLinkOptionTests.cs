using System.Linq;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// 시큐리티에서 나온 옵션이 "link this card to ~"로 링크 카드가 되면 트래시에 남지 않는다 —
/// 대량 무작위 대전에서 BT24-092가 링크 · 트래시 두 곳에 남아 카드가 1장 늘었다 (카드 수 보존 위반)
/// </summary>
public class SecurityLinkOptionTests
{
    private static CardData Digimon(string id, int dp) => new()
    {
        CardId = id, Name = id, Type = CardType.Digimon, Color = CardColor.Yellow, Level = 5, DP = dp, PlayCost = 6,
    };

    [Fact]
    public void LinkedSecurityOption_IsNotAlsoTrashed()
    {
        var option = new CardData
        {
            CardId = "SL-OPT", Name = "SL-OPT", Type = CardType.Option, Color = CardColor.Yellow, PlayCost = 3,
            EffectText = "[Security] Activate this card's [Main] effects.\n[Main] 1 of your opponent's Digimon gets -6000 DP for the turn. Then, you may link this card to 1 of your Digimon on the field without paying the cost.",
        };
        KeywordDictionary.ApplyDataKeywords(option);
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, TurnSequence = 5, MemoryGauge = 3 };
        var attacker = new CardInstance(Digimon("SL-ATK", 12000), 1);
        state.Player.BattleArea.Add(attacker);
        var defender = new CardInstance(Digimon("SL-DEF", 5000), 1);
        state.DummyOpponent.BattleArea.Add(defender);
        state.DummyOpponent.SecurityStack.Add(option);
        for (int i = 0; i < 3; i++) state.DummyOpponent.SecurityStack.Add(Digimon("SL-SEC" + i, 1000));
        var engine = new GoldfishEngine(state);

        engine.AttackSecurityOrDirect(attacker);

        Assert.Contains(option, defender.LinkedCards);
        Assert.DoesNotContain(option, state.DummyOpponent.Trash);
    }

    [Fact]
    public void LinkedOptionFromHand_DoesNotRemoveAnotherCopy()
    {
        var option = new CardData
        {
            CardId = "SL-OPT2", Name = "SL-OPT2", Type = CardType.Option, Color = CardColor.Yellow, PlayCost = 1,
            EffectText = "[Main] Link this card to 1 of your Digimon on the field without paying the cost.",
        };
        KeywordDictionary.ApplyDataKeywords(option);
        var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.Main, TurnSequence = 5, MemoryGauge = 3 };
        var mine = new CardInstance(Digimon("SL-MINE", 5000), 1);
        state.Player.BattleArea.Add(mine);
        state.Player.Hand.Add(option);
        state.Player.Hand.Add(option);                                                  // 같은 카드 2장 (같은 CardData 객체)
        var engine = new GoldfishEngine(state);

        Assert.True(engine.PlayCard(option, mine).CanExecute);

        Assert.Single(mine.LinkedCards);
        Assert.Single(state.Player.Hand);                                               // 나머지 1장은 패에 남는다
        Assert.DoesNotContain(option, state.Player.Trash);
    }
}
