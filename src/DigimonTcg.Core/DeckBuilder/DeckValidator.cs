using System.Collections.Generic;
using System.Linq;
using System.Text;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.DeckBuilder;

public record RatioItemStatus(
    string Label,
    int CurrentCount,
    int RecommendedMin,
    int RecommendedMax,
    string StatusText // "적정", "부족", "초과"
);

public record DeckRatioSummary(
    int DigitamaCount,
    int Lv3Count,
    int Lv4Count,
    int Lv5Count,
    int Lv6PlusCount,
    int TamerOptionCount,
    IReadOnlyList<RatioItemStatus> Items
);

public static class DeckValidator
{
    public const int RequiredMainDeckSize = 50;
    public const int MaxDigitamaDeckSize = 5;
    public const int MaxCopiesPerCard = 4;

    public static (bool IsValid, List<string> Errors) ValidateDeck(
        IReadOnlyList<CardData> mainDeck,
        IReadOnlyList<CardData> digitamaDeck)
    {
        var errors = new List<string>();

        if (mainDeck.Count != RequiredMainDeckSize)
            errors.Add($"메인 덱은 정확히 {RequiredMainDeckSize}장이어야 합니다. (현재 {mainDeck.Count}장)");

        if (digitamaDeck.Count > MaxDigitamaDeckSize)
            errors.Add($"디지타마 덱은 최대 {MaxDigitamaDeckSize}장까지 구성할 수 있습니다. (현재 {digitamaDeck.Count}장)");

        if (digitamaDeck.Any(c => c.Type != CardType.Digitama || c.Level != 2))
            errors.Add("디지타마 덱에는 Lv.2 디지타마 카드만 넣을 수 있습니다.");

        if (mainDeck.Any(c => c.Type == CardType.Digitama))
            errors.Add("메인 덱에는 디지타마 카드를 넣을 수 없습니다.");

        var allCards = mainDeck.Concat(digitamaDeck);
        foreach (var group in allCards.GroupBy(c => c.CardId))
        {
            if (group.Count() > MaxCopiesPerCard)
                errors.Add($"동일 카드 번호({group.Key})는 최대 {MaxCopiesPerCard}장까지만 투입할 수 있습니다. (현재 {group.Count()}장)");
        }

        return (errors.Count == 0, errors);
    }

    public static DeckRatioSummary CalculateRatios(
        IReadOnlyList<CardData> mainDeck,
        IReadOnlyList<CardData> digitamaDeck)
    {
        int digitama = digitamaDeck.Count;
        int lv3 = mainDeck.Count(c => c.Type == CardType.Digimon && c.Level == 3);
        int lv4 = mainDeck.Count(c => c.Type == CardType.Digimon && c.Level == 4);
        int lv5 = mainDeck.Count(c => c.Type == CardType.Digimon && c.Level == 5);
        int lv6Plus = mainDeck.Count(c => c.Type == CardType.Digimon && c.Level >= 6);
        int tamerOpt = mainDeck.Count(c => c.Type is CardType.Tamer or CardType.Option);

        var items = new List<RatioItemStatus>
        {
            BuildItem("Lv.2 디지타마", digitama, 4, 5),
            BuildItem("Lv.3 성장기", lv3, 12, 14),
            BuildItem("Lv.4 성숙기", lv4, 10, 12),
            BuildItem("Lv.5 완전체", lv5, 8, 10),
            BuildItem("Lv.6~7 궁극체", lv6Plus, 4, 6),
            BuildItem("테이머/옵션", tamerOpt, 6, 8)
        };

        return new DeckRatioSummary(digitama, lv3, lv4, lv5, lv6Plus, tamerOpt, items);
    }

    private static RatioItemStatus BuildItem(string label, int count, int min, int max)
    {
        string status = count < min ? "부족" : count > max ? "초과" : "적정";
        return new RatioItemStatus(label, count, min, max, status);
    }

    public static string FormatGoldenRatioHudText(DeckRatioSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[ 덱 구성 현황 - 황금 비율 HUD ]");
        foreach (var item in summary.Items)
        {
            int bars = System.Math.Clamp(item.CurrentCount, 0, 16);
            string barStr = new string('█', bars).PadRight(14, '░');
            sb.AppendLine($"{item.Label,-12} : [{barStr}] {item.CurrentCount,2} / {item.RecommendedMin}~{item.RecommendedMax} ({item.StatusText})");
        }
        return sb.ToString();
    }
}
