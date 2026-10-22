using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 엔진이 만든 문장(로그 · 상대 턴 리플레이 · 결과 메시지 · 선택 질문)의 영어 카드 이름을 공식 한글 이름으로 바꾼다.
/// 엔진은 판정에 영어 이름(CardData.Name)을 계속 쓰고, 화면에 나가는 문장만 바꾼다.
/// - 이름표: 같은 카드 번호의 영어 이름 ↔ 공식 한글 이름 (KoreanEffectRenderer.NameGlossary — 공식 한글 데이터가 없으면 비어 있어 아무것도 바꾸지 않는다)
/// - 영문자 · 숫자에 붙어 있으면 바꾸지 않는다 ("Agumon"은 "SnowAgumon" 안에서 바꾸지 않음), 같은 자리에서는 긴 이름부터 맞춘다
/// </summary>
public static class KoreanNameLocalizer
{
    // 첫 글자별 (영어 이름, 한글 이름) — 긴 이름 먼저
    private static readonly Lazy<(Dictionary<char, (string En, string Ko)[]> ByFirst, Dictionary<string, string> Map)> Table = new(Build);

    private static (Dictionary<char, (string, string)[]>, Dictionary<string, string>) Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (en, ko) in KoreanEffectRenderer.NameGlossary)
            if (en.Length >= 3 && ko.Length > 0 && !string.Equals(en, ko, StringComparison.Ordinal) && en.Any(char.IsAsciiLetter))
                map[en] = ko;
        var byFirst = map.GroupBy(kv => kv.Key[0])
            .ToDictionary(g => g.Key, g => g.OrderByDescending(kv => kv.Key.Length).Select(kv => (kv.Key, kv.Value)).ToArray());
        return (byFirst, map);
    }

    private static bool IsAlnum(char c) => char.IsAsciiLetterOrDigit(c);

    /// <summary>문장 속 영어 카드 이름을 공식 한글 이름으로 (바꿀 것이 없으면 그대로)</summary>
    public static string Localize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        var byFirst = Table.Value.ByFirst;
        if (byFirst.Count == 0)
            return text;
        StringBuilder? sb = null;
        int copied = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if ((i > 0 && IsAlnum(text[i - 1])) || !byFirst.TryGetValue(text[i], out var candidates))
                continue;
            foreach (var (en, ko) in candidates)
            {
                int end = i + en.Length;
                if (end > text.Length || string.CompareOrdinal(text, i, en, 0, en.Length) != 0 || (end < text.Length && IsAlnum(text[end])))
                    continue;
                sb ??= new StringBuilder(text.Length + 16);
                sb.Append(text, copied, i - copied).Append(ko);
                copied = end;
                i = end - 1;
                break;
            }
        }
        if (sb == null)
            return text;
        sb.Append(text, copied, text.Length - copied);
        return sb.ToString();
    }

    /// <summary>영어 카드 이름 하나 → 공식 한글 이름 (없으면 null) — 공식 한글이 없는 카드의 이름 표시에 쓸 수 있다</summary>
    public static string? KoreanNameFor(string englishName) =>
        Table.Value.Map.TryGetValue(englishName.Trim(), out var ko) ? ko : null;
}
