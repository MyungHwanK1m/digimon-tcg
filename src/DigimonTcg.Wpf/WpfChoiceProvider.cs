using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigimonTcg.Core.Engine.Effects;
using DigimonTcg.Core.Models;
using DigimonTcg.Wpf.Themes;

namespace DigimonTcg.Wpf;

/// <summary>
/// 카드 효과 처리 중 플레이어에게 묻는 창 (효과 확장 4단계):
/// "~할 수 있다" 확인, 효과 대상 고르기(카드 그림으로), 고르는 효과의 선택지. 언제나 추천(AI 평가) 답을 표시한다.
/// </summary>
public sealed class WpfChoiceProvider : IChoiceProvider
{
    private readonly Window _owner;
    private readonly Func<CardData, FrameworkElement>? _cardVisual;

    /// <param name="cardVisual">대상 고르기 창에 보여 줄 카드 그림 (없으면 이름 · DP만)</param>
    public WpfChoiceProvider(Window owner, Func<CardData, FrameworkElement>? cardVisual = null)
    {
        _owner = owner;
        _cardVisual = cardVisual;
    }

    public bool Confirm(GameState state, CardInstance? subject, string question, bool recommended)
    {
        bool? result = null;
        var (window, body) = CreateWindow("카드 효과", question);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(MakeButton(recommended ? "예 (추천)" : "예", recommended, () => { result = true; window.Close(); }));
        buttons.Children.Add(MakeButton(!recommended ? "아니요 (추천)" : "아니요", !recommended, () => { result = false; window.Close(); }));
        body.Children.Add(buttons);
        window.ShowDialog();
        return result ?? recommended;
    }

    public CardInstance? ChooseDigimon(GameState state, string question, IReadOnlyList<CardInstance> options, CardInstance? recommended, bool optional)
    {
        CardInstance? result = null;
        bool chosen = false;
        var (window, body) = CreateWindow("효과 대상 고르기", question);

        var cards = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), MaxWidth = 860 };
        foreach (var option in options)
        {
            bool mine = state.Player.BattleArea.Contains(option);
            var content = new StackPanel { Width = 120 };
            if (_cardVisual != null)
                content.Children.Add(_cardVisual(option.TopCard));
            content.Children.Add(new TextBlock
            {
                Text = $"{(mine ? "내" : "상대")} {option.TopCard.Name}\nLv.{option.TopCard.Level} · DP {option.GetCurrentDP():N0}{(option.IsSuspended ? " · 레스트" : "")}"
                       + (ReferenceEquals(option, recommended) ? "\n★ 추천" : ""),
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
            });
            var pick = option;
            var button = MakeButton(content, ReferenceEquals(option, recommended), () => { result = pick; chosen = true; window.Close(); });
            button.Margin = new Thickness(6);
            button.Padding = new Thickness(6);
            cards.Children.Add(button);
        }
        body.Children.Add(cards);

        if (optional)
        {
            var skip = MakeButton("고르지 않음", false, () => { result = null; chosen = true; window.Close(); });
            skip.HorizontalAlignment = HorizontalAlignment.Right;
            skip.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(skip);
        }
        window.ShowDialog();
        return chosen ? result : optional ? null : recommended;
    }

    public int ChooseOption(GameState state, string question, IReadOnlyList<string> options, int recommended)
    {
        int result = recommended;
        var (window, body) = CreateWindow("효과 고르기", question);
        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            var button = MakeButton(new TextBlock
            {
                Text = $"{i + 1}. {options[i]}" + (i == recommended ? "   ★ 추천" : ""),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                Foreground = Brushes.White,
            }, i == recommended, () => { result = index; window.Close(); });
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(button);
        }
        window.ShowDialog();
        return result;
    }

    // =====================================================================
    // 창 · 버튼
    // =====================================================================

    private (Window Window, StackPanel Body) CreateWindow(string title, string question)
    {
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock
        {
            Text = question,
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
        });
        var window = new Window
        {
            Owner = _owner,
            Title = title,
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 80),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x22)),
        };
        return (window, body);
    }

    private static Button MakeButton(object content, bool highlighted, Action onClick)
    {
        Brush accent;
        try { accent = Theme.GetBrush(highlighted ? "Neo.Brush.Gold" : "Neo.Brush.Cyan"); }
        catch { accent = highlighted ? Brushes.Gold : Brushes.SteelBlue; }

        var button = new Button
        {
            Content = content is string text ? new TextBlock { Text = text, Foreground = Brushes.White } : content,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 90,
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x30, 0x40, 0x60)),
            BorderBrush = accent,
            BorderThickness = new Thickness(highlighted ? 2 : 1),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
