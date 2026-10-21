using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using Xunit;

namespace DigimonTcg.Core.Tests;

/// <summary>효과 확장 6단계 — 영어 효과 문장을 공식 한글판 표기로 만드는지 (공식 한글이 없는 카드의 화면 표시)</summary>
[Collection("KoreanCardData")]   // KoreanCardData.Reload(전역)을 쓰는 테스트끼리 차례로
public class KoreanEffectRendererTests
{
    [Theory]
    // 공식 한글판 문구와 같은 문장이 나와야 하는 것들 (BT1-039 · BT1-092 · BT1-036 · BT1-023 · BT1-012 · BT1-085)
    [InlineData("[When Attacking] [Twice Per Turn] You may trash 3 cards in your hand to unsuspend this Digimon.",
                "【어택 시】 [턴에 2회] 자신의 패를 3장 파기하는 것으로, 이 디지몬을 액티브로 한다.")]
    [InlineData("[Main] ＜Draw 2＞ (Draw 2 cards from your deck.) Then 1 of your Digimon gets +2000 DP for the turn.",
                "【메인】 《2 드로우》 (자신의 덱에서 카드를 2장 드로우한다). 그 후, 이 턴 동안, 자신의 디지몬 1마리의 DP를 +2000 한다.")]
    [InlineData("[On Play] Unsuspend 1 of your Digimon.", "【등장 시】 자신의 디지몬 1마리를 액티브로 한다.")]
    [InlineData("[On Play] Delete 1 of your opponent's Digimon with ＜Blocker＞.", "【등장 시】 《블로커》를 가진 상대의 디지몬 1마리를 소멸시킨다.")]
    [InlineData("[Your Turn] When this Digimon is blocked, it gets +2000 DP.", "【자신의 턴】 이 디지몬이 블록당했을 때, 이 디지몬의 DP를 +2000 한다.")]
    [InlineData("[Start of Your Turn] If you have 2 or less memory, set it to 3.", "【자신의 턴 개시 시】 메모리가 2 이하일 때, 메모리를 3으로 한다.")]
    // 대상 꾸밈말 · 기간 · "~할 수 있다" · 비용
    [InlineData("[On Play] Delete 1 of your opponent's Digimon with 4000 DP or less.", "【등장 시】 DP 4000 이하인 상대의 디지몬 1마리를 소멸시킨다.")]
    [InlineData("[Main] Return up to 2 of your opponent's level 4 or lower Digimon to their hand.", "【메인】 Lv.4 이하인 상대의 디지몬 2마리까지를 패로 되돌린다.")]
    [InlineData("[Main] Until the end of your opponent's next turn, 1 of your opponent's Digimon can't attack or block.",
                "【메인】 다음 상대의 턴 종료 시까지, 상대의 디지몬 1마리는 어택과 블록을 할 수 없다.")]
    [InlineData("[On Deletion] You may delete 1 of your Digimon to gain 2 memory.", "【소멸 시】 자신의 디지몬 1마리를 소멸시키는 것으로, 메모리를 +2 한다.")]
    // 키워드만 있는 절 (공식 설명문)
    [InlineData("＜Jamming＞ (This Digimon can't be deleted in battles against Security Digimon.)", "《재밍》 (이 디지몬은 시큐리티 디지몬과의 배틀에서는 소멸하지 않는다)")]
    [InlineData("＜Security A. +1＞", "《시큐리티 어택 +1》 (이 디지몬이 체크하는 시큐리티의 매수 +1)")]
    public void RenderClause_ShouldFollowOfficialKoreanWording(string english, string korean)
    {
        Assert.Equal(korean, KoreanEffectRenderer.RenderClause(english));
    }

    [Fact]
    public void Particles_ShouldFollowTheFinalConsonant()
    {
        // 《관통》을 (받침 있음) · 《블로커》를 (받침 없음) · 1장과 · 《드로우》 …
        Assert.Equal("이 디지몬은 《관통》 (이 디지몬이 어택한 배틀에서 상대의 디지몬만을 소멸시켰을 때, 이 디지몬은 시큐리티를 체크한다)을 얻는다.",
            KoreanEffectRenderer.RenderClause("This Digimon gains ＜Piercing＞."));
        Assert.Contains("1장과 ", KoreanEffectRenderer.RenderClause(
            "[On Play] Reveal the top 3 cards of your deck. Add 1 level 5 Digimon card and 1 level 6 Digimon card among them to your hand. Trash the rest."));
    }

    [Fact]
    public void ItWithoutAnEarlierTarget_ShouldMeanThisDigimon()
    {
        Assert.Contains("이 디지몬의 DP를", KoreanEffectRenderer.RenderClause("[Your Turn] While this Digimon has ＜Reboot＞, it gets +1000 DP."));
        Assert.Contains("그 디지몬은 다음 액티브 페이즈에", KoreanEffectRenderer.RenderClause(
            "[On Play] Suspend 1 of your opponent's Digimon. It doesn't unsuspend during your opponent's next unsuspend phase."));
    }

    [Fact]
    public void LevelNOrM_ShouldStayOneCardPickInTheEngineAndTheRenderer()
    {
        // "1 level 4 or 5 Digimon card"를 "1 level 4" 또는 "5 Digimon cards"로 나누면 안 된다
        var card = new CardData
        {
            CardId = "KO-LV45", Name = "KO-LV45", Type = CardType.Digimon, Color = CardColor.Green, Level = 3, DP = 2000,
            EffectText = "[On Deletion] Reveal the top 3 cards of your deck. Add 1 level 4 or 5 Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order.",
        };
        Assert.All(KeywordDictionary.Analyze(card), c => Assert.Equal(ClauseSupport.Supported, c.Support));
        Assert.Contains("Lv.4 또는 Lv.5인 디지몬 카드 1장", KoreanEffectRenderer.Render(card, inherited: false));
    }

    [Fact]
    public void UntranslatedClause_ShouldShowTheEnglishWithAMark()
    {
        var card = new CardData
        {
            CardId = "KO-MIXED", Name = "KO-MIXED", Type = CardType.Digimon, Color = CardColor.Red, Level = 5, DP = 7000,
            EffectText = "[On Play] Gain 1 memory.\n[Your Turn] This Digimon is also treated as yellow.",
        };
        var lines = KoreanEffectRenderer.Render(card, inherited: false)!.Split('\n');
        Assert.Equal("【등장 시】 메모리를 +1 한다.", lines[0]);
        Assert.StartsWith(KoreanEffectRenderer.UntranslatedMark, lines[1]);
        Assert.Contains("also treated as yellow", lines[1]);
        Assert.Null(KoreanEffectRenderer.RenderClause("[Your Turn] This Digimon is also treated as yellow."));
    }

    [Fact]
    public void DisplayText_ShouldUseOfficialThenBuiltInThenGeneratedThenEnglish()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dtcg_kogen_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            KoreanCardData.Reload(Path.Combine(dir, "missing.json"));   // 공식 한글 데이터가 없는 PC
            var api = new CardData
            {
                CardId = "KO-DISPLAY", Name = "KO-DISPLAY", Type = CardType.Tamer, Color = CardColor.Red,
                EffectText = "[Start of Your Turn] If you have 2 or less memory, set it to 3.",
                InheritedEffectText = "[Security] Play this card without paying the cost.",
            };
            Assert.Equal("【자신의 턴 개시 시】 메모리가 2 이하일 때, 메모리를 3으로 한다.\n【시큐리티】 이 카드를 코스트를 지불하지 않고 등장시킨다.", api.DisplayEffectText);
            Assert.Equal(string.Empty, api.DisplayInheritedEffectText);   // 테이머 · 옵션은 [시큐리티]를 메인 쪽에

            // 내장 카드는 이미 한글 — 생성으로 덮지 않는다
            var builtIn = StarterDecks.GetAllCatalogCards().First(c => c.CardId == "ST1-16");
            Assert.Equal(builtIn.EffectText, builtIn.DisplayEffectText);

            // 번역할 수 없는 문장뿐이면 영어 원문
            var english = new CardData { CardId = "KO-ENGLISH", Name = "KO-ENGLISH", Type = CardType.Digimon, EffectText = "[Your Turn] This Digimon is also treated as yellow." };
            Assert.Equal(english.EffectText, english.DisplayEffectText);
        }
        finally
        {
            KoreanCardData.Reload();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
