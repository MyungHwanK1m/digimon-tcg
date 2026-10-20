using System.Collections.Generic;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Engine.Effects;

/// <summary>
/// 효과 처리 중 플레이어에게 묻는 선택 ("~할 수 있다" 사용 여부, 대상 고르기).
/// 엔진은 항상 추천 답(AI가 고를 답)을 함께 넘긴다. 등록하지 않으면(테스트 · CLI) 추천 답을 그대로 쓰고, AI 쪽 선택은 언제나 추천 답이다.
/// </summary>
public interface IChoiceProvider
{
    /// <summary>"~할 수 있다" 효과를 쓸지. subject = 효과를 가진 디지몬</summary>
    bool Confirm(GameState state, CardInstance? subject, string question, bool recommended);

    /// <summary>후보 중 하나를 고른다. optional이면 null(고르지 않음)도 가능</summary>
    CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional);
}
