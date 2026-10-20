using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>공식 룰: 옵션 카드는 같은 색의 디지몬 · 테이머가 자신의 필드에 있어야 [메인]으로 사용할 수 있다</summary>
public class OptionColorRuleTests
{
    private static (GameState State, System.Collections.Generic.Dictionary<string, CardData> Cards) NewMainPhase()
    {
        var state = new GameState { CurrentTurn = 2, CurrentPhase = Phase.Main, MemoryGauge = 5, TurnSequence = 3 };
        return (state, StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId));
    }

    [Fact]
    public void Option_WithoutSameColorOnField_ShouldBeRejected()
    {
        var (state, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-03"], 1));   // 레드 아구몬만 있음

        var res = ActionValidator.ValidatePlay(state, cards["ST2-13"]);       // 블루 해머 스파크
        Assert.False(res.CanExecute);
        Assert.Contains("블루", res.Reason);
    }

    [Fact]
    public void Option_WithSameColorDigimonTamerOrBreeding_ShouldBeAllowed()
    {
        var (state, cards) = NewMainPhase();
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-03"], 1));
        Assert.True(ActionValidator.ValidatePlay(state, cards["ST1-13"]).CanExecute);   // 레드 섀도 윙

        // 테이머의 색도 인정
        var (s2, _) = NewMainPhase();
        s2.Player.Tamers.Add(cards["ST2-12"]);
        Assert.True(ActionValidator.ValidatePlay(s2, cards["ST2-13"]).CanExecute);

        // 육성 에어리어의 디지몬(디지타마 포함)도 인정
        var (s3, _) = NewMainPhase();
        s3.Player.BreedingArea = new CardInstance(cards["ST3-01"], 1);
        Assert.True(ActionValidator.ValidatePlay(s3, cards["ST3-16"]).CanExecute);
    }

    [Fact]
    public void Option_WithEmptyField_ShouldBeRejected()
    {
        var (state, cards) = NewMainPhase();
        Assert.False(ActionValidator.ValidatePlay(state, cards["ST1-16"]).CanExecute);
    }

    [Fact]
    public void MultiColorOption_ShouldNeedEveryColor()
    {
        var (state, cards) = NewMainPhase();
        var multi = new CardData { CardId = "TEST-01", Name = "테스트 옵션", Type = CardType.Option, Color = CardColor.Red, SecondaryColor = CardColor.Blue, PlayCost = 1 };
        state.Player.BattleArea.Add(new CardInstance(cards["ST1-03"], 1));      // 레드만
        Assert.False(ActionValidator.ValidatePlay(state, multi).CanExecute);

        state.Player.Tamers.Add(cards["ST2-12"]);                               // 블루 테이머 추가
        Assert.True(ActionValidator.ValidatePlay(state, multi).CanExecute);
    }
}
