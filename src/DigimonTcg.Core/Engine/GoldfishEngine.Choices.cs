using System;
using System.Collections.Generic;
using System.Linq;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

// 여러 장 고르기 헬퍼(AskDigimons · ChooseCards)를 테스트에서 직접 확인한다
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DigimonTcg.Core.Tests")]

namespace DigimonTcg.Core.Engine;

/// <summary>
/// 여러 장을 한 번에 고르는 선택 (선택 UI 보강 — 계획 문서 11절 "남은 것").
/// 플레이어의 효과이고 UI가 연결돼 있을 때만 묻고, 그 밖(AI · 테스트 · CLI)에는 추천 순서대로 고른다.
/// 플레이어가 고른 답이 규칙에 맞지 않으면(후보 밖 · 장수 틀림) 추천 답을 쓴다.
/// </summary>
public partial class GoldfishEngine
{
    /// <summary>
    /// 디지몬 count마리를 한 번에 고른다. ranked = 추천 순서의 후보, upTo면 0 ~ count마리.
    /// 묻지 않는 경우: AI · 테스트, 후보가 count 이하이고 반드시 골라야 할 때 (고를 것이 없음).
    /// </summary>
    internal List<CardInstance> AskDigimons(PlayerZones owner, IReadOnlyList<CardInstance> ranked, int count, bool upTo, string question)
    {
        var recommended = ranked.Take(Math.Max(0, count)).ToList();
        if (!ShouldAskPlayer(owner) || ranked.Count == 0 || count <= 0 || (ranked.Count <= count && !upTo))
            return recommended;

        int max = Math.Min(count, ranked.Count);
        int min = upTo ? 0 : max;
        var picked = PlayerChoices!.ChooseDigimons(State, question, ranked, min, max, recommended);
        return IsValidPick(picked, ranked, min, max) ? picked.ToList() : recommended;
    }

    /// <summary>
    /// 패 · 트래시 · 진화원 등의 카드 count장을 고른다. ranked = 추천 순서의 후보 (지금까지 엔진이 자동으로 고르던 정렬 그대로),
    /// upTo면 0 ~ count장. 같은 카드 여러 장은 같은 객체일 수 있으므로 플레이어에게는 번호로 묻는다.
    /// 돌려준 카드를 zone에서 뺄 때는 List.Remove(card)로 같은 카드 한 장씩 빼면 된다.
    /// </summary>
    internal List<CardData> ChooseCards(PlayerZones owner, CardChoiceZone zone, IReadOnlyList<CardData> ranked, int count, bool upTo, string question) =>
        ChooseCardIndexes(owner, zone, ranked, count, upTo, question).Select(i => ranked[i]).ToList();

    /// <summary>ChooseCards와 같지만 고른 번호를 돌려준다 — 여러 곳(패 · 트래시 …)에서 모은 후보라 고른 카드가 있던 곳을 알아야 할 때</summary>
    internal List<int> ChooseCardIndexes(PlayerZones owner, CardChoiceZone zone, IReadOnlyList<CardData> ranked, int count, bool upTo, string question)
    {
        var recommended = Enumerable.Range(0, Math.Min(Math.Max(0, count), ranked.Count)).ToList();
        if (!ShouldAskPlayer(owner) || ranked.Count == 0 || count <= 0 || (ranked.Count <= count && !upTo))
            return recommended;

        int max = Math.Min(count, ranked.Count);
        int min = upTo ? 0 : max;
        var picked = PlayerChoices!.ChooseCards(State, question, zone, ranked, min, max, recommended);
        bool valid = picked != null
            && picked.Count >= min && picked.Count <= max
            && picked.All(i => i >= 0 && i < ranked.Count)
            && picked.Distinct().Count() == picked.Count;
        return valid ? picked!.ToList() : recommended;
    }

    private bool ShouldAskPlayer(PlayerZones owner) => ReferenceEquals(owner, State.Player) && PlayerChoices != null;

    /// <summary>고른 답이 후보 안의 서로 다른 객체이고 장수가 min ~ max인지</summary>
    private static bool IsValidPick<T>(IReadOnlyList<T>? picked, IReadOnlyList<T> options, int min, int max) where T : class =>
        picked != null
        && picked.Count >= min && picked.Count <= max
        && picked.All(p => options.Any(o => ReferenceEquals(o, p)))
        && picked.Distinct(ReferenceEqualityComparer.Instance).Count() == picked.Count;
}
