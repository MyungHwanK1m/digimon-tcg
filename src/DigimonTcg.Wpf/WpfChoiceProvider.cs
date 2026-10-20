using System.Collections.Generic;
using System.Windows;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Wpf;

/// <summary>
/// 카드 효과의 "~할 수 있다" 선택을 플레이어에게 묻는다 (≪방벽≫ · ≪회피≫ · ≪돌진≫ · ≪연계≫).
/// 대상 고르기 팝업은 효과 확장 4단계에서 만들고, 그 전까지는 추천 대상을 쓸지만 묻는다.
/// </summary>
public sealed class WpfChoiceProvider : IChoiceProvider
{
    private readonly Window _owner;

    public WpfChoiceProvider(Window owner) => _owner = owner;

    public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended)
    {
        string hint = recommended ? "\n\n(추천: 예)" : "\n\n(추천: 아니요)";
        var answer = MessageBox.Show(_owner, question + hint, "카드 효과", MessageBoxButton.YesNo, MessageBoxImage.Question,
            recommended ? MessageBoxResult.Yes : MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional)
    {
        if (recommended == null)
            return optional ? null : options.Count > 0 ? options[0] : null;

        var answer = MessageBox.Show(_owner,
            $"{question}\n\n추천: [{recommended.TopCard.Name}] (DP {recommended.GetCurrentDP():N0})\n이 디지몬으로 할까요?" + (optional ? "\n(아니요 = 고르지 않음)" : ""),
            "카드 효과", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        return answer == MessageBoxResult.Yes || !optional ? recommended : null;
    }
}
