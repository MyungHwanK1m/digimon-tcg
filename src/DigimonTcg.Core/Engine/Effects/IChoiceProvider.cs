using System.Collections.Generic;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 효과 처리 중 플레이어에게 묻는 선택 ("~할 수 있다" 사용 여부, 대상 고르기, 고르는 효과의 선택지).
/// 엔진은 항상 추천 답(AI 평가 함수 AiEvaluator가 고를 답)을 함께 넘긴다.
/// 등록하지 않으면(테스트 · CLI) 추천 답을 그대로 쓰고, AI 쪽 선택은 언제나 추천 답이다.
/// </summary>
public interface IChoiceProvider
{
    /// <summary>"~할 수 있다" 효과를 쓸지. subject = 효과를 가진 디지몬</summary>
    bool Confirm(GameState state, CardInstance? subject, string question, bool recommended);

    /// <summary>후보 중 하나를 고른다 (후보는 추천 순서). optional이면 null(고르지 않음)도 가능</summary>
    CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional);

    /// <summary>고르는 효과("Activate 1 of the effects below")의 선택지 중 하나 — 반환: 고른 번호</summary>
    int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended);

    /// <summary>
    /// 디지몬을 min ~ max마리 한 번에 고른다 (효과 대상 N마리). recommended = 추천 답 (AI 평가 순서).
    /// 기본 구현은 추천 답 그대로 — 구현하지 않은 선택기(테스트 · CLI)는 지금처럼 동작한다.
    /// </summary>
    IReadOnlyList<CardInstance> ChooseDigimons(GameState state, string question, IReadOnlyList<CardInstance> options,
        int min, int max, IReadOnlyList<CardInstance> recommended) => recommended;

    /// <summary>
    /// 패 · 트래시 · 진화원 등에 있는 카드를 min ~ max장 고른다 (버리기 · 회수 · 패나 트래시에서 등장 · 진화원 아래에 놓기).
    /// 같은 카드 여러 장은 같은 CardData 객체일 수 있으므로 options의 번호(index)로 주고받는다.
    /// recommended = 추천하는 번호들. 기본 구현은 추천 답 그대로.
    /// </summary>
    IReadOnlyList<int> ChooseCards(GameState state, string question, CardChoiceZone zone, IReadOnlyList<CardData> options,
        int min, int max, IReadOnlyList<int> recommended) => recommended;
}

/// <summary>ChooseCards가 고르는 카드가 있는 곳 (창 제목 · 안내 문구용)</summary>
public enum CardChoiceZone { Hand, Trash, HandOrTrash, DigivolutionCards, Other }
