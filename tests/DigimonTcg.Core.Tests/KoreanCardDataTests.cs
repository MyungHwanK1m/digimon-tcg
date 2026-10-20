using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>한국 공식 사이트 데이터(data/kr/cards_ko.json)는 화면 표시 · 검색에만 쓰이고, 없으면 원래 값이 나와야 한다</summary>
public class KoreanCardDataTests
{
    private const string SampleJson = """
    [
      { "CardId": "ST1-12", "Name": "테스트 신태일", "Type": "테이머", "Traits": ["테스트특징"],
        "MainText": "【자신의 턴】 자신의 디지몬 전부의 DP를 +1000 한다.", "SourceText": "【시큐리티】 이 카드를 코스트를 지불하지 않고 등장시킨다." },
      { "CardId": "ST1-03", "Name": "테스트 아구몬", "Type": "디지몬", "Traits": [],
        "MainText": "", "SourceText": "【자신의 턴】 이 디지몬의 DP를 +1000 한다." }
    ]
    """;

    [Fact]
    public void KoreanData_ShouldOnlyChangeDisplayAndSearch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dtcg_ko_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "cards_ko.json");
        File.WriteAllText(file, SampleJson);
        try
        {
            KoreanCardData.Reload(file);
            var cards = StarterDecks.GetAllCatalogCards().ToDictionary(c => c.CardId);
            var tai = cards["ST1-12"];

            // 표시: 한글 데이터 우선, 테이머는 [시큐리티] 효과까지 붙여서
            Assert.Equal("테스트 신태일", tai.DisplayName);
            Assert.Contains("【시큐리티】", tai.DisplayEffectText);
            Assert.Equal(string.Empty, tai.DisplayInheritedEffectText);
            Assert.Contains("테스트특징", tai.DisplayTraits);
            Assert.Contains("【자신의 턴】", cards["ST1-03"].DisplayInheritedEffectText);

            // 엔진이 보는 원래 값은 그대로
            Assert.Equal("신태일", tai.Name);
            Assert.DoesNotContain("테스트", tai.EffectText);

            // 한글 데이터가 없는 카드는 원래 값
            Assert.Equal(cards["ST1-02"].Name, cards["ST1-02"].DisplayName);

            // 검색: 화면에 보이는 한글 이름 · 특징으로도 찾는다
            var catalogDir = Path.Combine(dir, "catalog");
            var catalog = new CardCatalogService(catalogDir);
            Assert.Contains(catalog.SearchCards("테스트 신태일"), c => c.CardId == "ST1-12");
            Assert.Contains(catalog.SearchCards("테스트특징"), c => c.CardId == "ST1-12");
        }
        finally
        {
            KoreanCardData.Reload();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
