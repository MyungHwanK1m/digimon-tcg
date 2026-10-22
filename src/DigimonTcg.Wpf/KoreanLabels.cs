using System.Linq;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Wpf;

/// <summary>화면에 보이는 카드 색 · 종류의 공식 한글 표기 (영어 enum 이름을 그대로 보여 주지 않는다)</summary>
internal static class KoreanLabels
{
    public static string Color(CardColor color) => color switch
    {
        CardColor.Red => "레드",
        CardColor.Blue => "블루",
        CardColor.Yellow => "옐로",
        CardColor.Green => "그린",
        CardColor.Black => "블랙",
        CardColor.Purple => "퍼플",
        CardColor.White => "화이트",
        _ => color.ToString()
    };

    /// <summary>여러 색 카드는 "레드/블루"</summary>
    public static string Colors(CardData card) => string.Join("/", card.AllColors.Select(Color));

    public static string Type(CardType type) => type switch
    {
        CardType.Digimon => "디지몬",
        CardType.Tamer => "테이머",
        CardType.Option => "옵션",
        CardType.Digitama => "디지타마",
        _ => type.ToString()
    };
}
