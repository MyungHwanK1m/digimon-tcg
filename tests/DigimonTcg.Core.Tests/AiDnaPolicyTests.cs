using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Engine.Ai;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>개선 AI의 DNA 진화 판단 — 조그레스 조건은 공식 한글 데이터에서 읽으므로 KoreanCardData를 바꾸는 테스트와 같은 컬렉션</summary>
[Collection("KoreanCardData")]
public class AiDnaPolicyTests
{
    private static CardData Api(string id, int level, int dp, CardColor color) =>
        new() { CardId = id, Name = id, Type = CardType.Digimon, Color = color, Level = level, DP = dp, PlayCost = 5, EvoCost = 2, EvoRequirementLevel = level - 1, EvoRequirementColor = color };

    private static void WithKoreanCards(IEnumerable<KoreanCard> extra, System.Action run)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dtcg_aidna_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var file = System.IO.Path.Combine(dir, "cards_ko.json");
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

    [Fact]
    public void Smart_ShouldDnaDigivolveTwoDigimonIntoAStrongerOne_AndLegacyShouldNot()
    {
        WithKoreanCards(new[] { new KoreanCard { CardId = "AI-DNA-HOST", Name = "테스트조그레스몬", MainText = "〔조그레스〕블루 Lv.4+그린 Lv.4 : 코스트 0" } }, () =>
        {
            var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.TurnEnd, TurnSequence = 6, CurrentStage = PracticeStage.Stage3_AiBattle };
            var engine = new GoldfishEngine(state);
            var blue = new CardInstance(Api("AI-DNA-B", 4, 5000, CardColor.Blue), 1);
            var green = new CardInstance(Api("AI-DNA-G", 4, 4000, CardColor.Green), 1);
            state.DummyOpponent.BattleArea.Add(blue);
            state.DummyOpponent.BattleArea.Add(green);
            var host = Api("AI-DNA-HOST", 6, 12000, CardColor.Blue);
            state.DummyOpponent.Hand.Add(host);

            var pick = new SmartAiPolicy().ChooseDnaEvolution(engine, memory: 2, beforeAttacks: true);

            Assert.NotNull(pick);
            Assert.Equal(host, pick!.Value.Card);
            Assert.Equal(new[] { blue, green }.OrderBy(d => d.InstanceId), new[] { pick.Value.A, pick.Value.B }.OrderBy(d => d.InstanceId));
            Assert.Null(new LegacyAiPolicy().ChooseDnaEvolution(engine, memory: 2, beforeAttacks: true));
        });
    }

    [Fact]
    public void Smart_ShouldNotDnaDigivolveIntoSomethingWeaker()
    {
        WithKoreanCards(new[] { new KoreanCard { CardId = "AI-DNA2-HOST", Name = "테스트약한조그레스몬", MainText = "〔조그레스〕블루 Lv.4+그린 Lv.4 : 코스트 0" } }, () =>
        {
            var state = new GameState { CurrentTurn = 3, CurrentPhase = Phase.TurnEnd, TurnSequence = 6, CurrentStage = PracticeStage.Stage3_AiBattle };
            var engine = new GoldfishEngine(state);
            state.DummyOpponent.BattleArea.Add(new CardInstance(Api("AI-DNA2-B", 4, 7000, CardColor.Blue), 1));
            state.DummyOpponent.BattleArea.Add(new CardInstance(Api("AI-DNA2-G", 4, 7000, CardColor.Green), 1));
            state.DummyOpponent.Hand.Add(Api("AI-DNA2-HOST", 4, 3000, CardColor.Blue));   // 두 마리를 합쳐도 약해진다

            Assert.Null(new SmartAiPolicy().ChooseDnaEvolution(engine, memory: 2, beforeAttacks: true));
        });
    }
}
