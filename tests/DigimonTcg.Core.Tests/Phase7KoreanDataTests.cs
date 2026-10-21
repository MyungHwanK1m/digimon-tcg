using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>
/// 효과 확장 7단계 — 공식 한글 문구를 실행 중에 읽는 메커닉 (디지크로스).
/// KoreanCardData.Reload는 전역이므로 Reload를 쓰는 테스트 클래스는 같은 컬렉션("KoreanCardData")으로 묶어 차례로 돈다.
/// </summary>
[Collection("KoreanCardData")]
public class Phase7KoreanDataTests
{
    private static Dictionary<string, CardData> Cards() => StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);

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
