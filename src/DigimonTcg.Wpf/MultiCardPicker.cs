using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigimonTcg.Core.Models;
using DigimonTcg.Wpf.Themes;

namespace DigimonTcg.Wpf;

/// <summary>
/// 카드를 여러 장(min ~ max장) 한 번에 고르는 창 — 효과 대상 디지몬 N마리, 패 · 트래시 · 진화원의 카드 고르기에 같이 쓴다.
/// 카드를 누르면 고르거나 풀고, 처음에는 추천(AI 평가) 카드가 골라져 있다. 창을 닫으면 null (= 추천 답을 쓴다).
/// </summary>
internal static class MultiCardPicker
{
    /// <param name="Key">돌려줄 값 (CardInstance · CardData — 같은 카드가 여러 장이어도 객체로 구분)</param>
    /// <param name="Caption">카드 아래 설명 (이름 · Lv · DP 등)</param>
    public sealed record Item(object Key, CardData Card, string Caption);

    public static List<object>? Show(
        Window owner, string title, string question, IReadOnlyList<Item> items,
        int min, int max, IReadOnlyCollection<object> recommended, Func<CardData, FrameworkElement>? cardVisual)
    {
        max = Math.Clamp(max, 0, items.Count);
        min = Math.Clamp(min, 0, max);
        var selected = new List<object>(recommended.Where(r => items.Any(i => ReferenceEquals(i.Key, r))).Take(max));
        List<object>? result = null;

        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock
        {
            Text = question,
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 760,
        });
        var counter = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0) };
        body.Children.Add(counter);

        var cards = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), MaxWidth = 900 };
        var borders = new Dictionary<object, Border>(ReferenceEqualityComparer.Instance);
        var confirm = MakeButton("확인", highlighted: true);

        void Refresh()
        {
            bool valid = selected.Count >= min && selected.Count <= max;
            counter.Text = min == max
                ? $"{selected.Count} / {max}장 골랐습니다 — {max}장을 고르세요."
                : $"{selected.Count}장 골랐습니다 — {min}~{max}장을 고를 수 있습니다.";
            counter.Foreground = Brush(valid ? "Neo.Brush.Text.Secondary" : "Neo.Brush.Rose", valid ? Brushes.LightGray : Brushes.Salmon);
            confirm.IsEnabled = valid;
            confirm.Opacity = valid ? 1.0 : 0.45;
            foreach (var (key, border) in borders)
            {
                bool on = selected.Any(s => ReferenceEquals(s, key));
                border.BorderBrush = on ? Brush("Neo.Brush.Gold", Brushes.Gold) : new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
                border.BorderThickness = new Thickness(on ? 3 : 1);
                border.Background = new SolidColorBrush(on ? Color.FromArgb(0x55, 0xF9, 0xE2, 0xAF) : Color.FromArgb(0x30, 0x30, 0x40, 0x60));
            }
        }

        foreach (var item in items)
        {
            var content = new StackPanel { Width = 116 };
            if (cardVisual != null)
                content.Children.Add(cardVisual(item.Card));
            bool isRecommended = recommended.Any(r => ReferenceEquals(r, item.Key));
            content.Children.Add(new TextBlock
            {
                Text = item.Caption + (isRecommended ? "\n★ 추천" : ""),
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5,
                Margin = new Thickness(0, 6, 0, 0),
            });
            var border = new Border
            {
                Child = content,
                Padding = new Thickness(6),
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(6),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var key = item.Key;
            border.MouseLeftButtonUp += (_, _) =>
            {
                int at = selected.FindIndex(s => ReferenceEquals(s, key));
                if (at >= 0)
                    selected.RemoveAt(at);
                else if (max == 1)
                {
                    selected.Clear();          // 1장만 고를 때는 바로 바꾼다
                    selected.Add(key);
                }
                else if (selected.Count < max)
                    selected.Add(key);
                Refresh();
            };
            borders[key] = border;
            cards.Children.Add(border);
        }
        body.Children.Add(cards);

        var window = new Window
        {
            Owner = owner,
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

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var useRecommended = MakeButton("추천대로", highlighted: false);
        useRecommended.Click += (_, _) =>
        {
            selected.Clear();
            selected.AddRange(recommended.Where(r => items.Any(i => ReferenceEquals(i.Key, r))).Take(max));
            Refresh();
        };
        buttons.Children.Add(useRecommended);
        if (min == 0)
        {
            var none = MakeButton("고르지 않음", highlighted: false);
            none.Click += (_, _) => { result = new List<object>(); window.Close(); };
            buttons.Children.Add(none);
        }
        confirm.Click += (_, _) => { result = selected.ToList(); window.Close(); };
        buttons.Children.Add(confirm);
        body.Children.Add(buttons);

        Refresh();
        window.ShowDialog();
        return result;
    }

    private static Brush Brush(string key, Brush fallback)
    {
        try { return Theme.GetBrush(key); }
        catch { return fallback; }
    }

    private static Button MakeButton(string text, bool highlighted) => new()
    {
        Content = new TextBlock { Text = text, Foreground = Brushes.White },
        Padding = new Thickness(14, 6, 14, 6),
        Margin = new Thickness(8, 0, 0, 0),
        MinWidth = 90,
        Background = new SolidColorBrush(Color.FromArgb(0x40, 0x30, 0x40, 0x60)),
        BorderBrush = Brush(highlighted ? "Neo.Brush.Gold" : "Neo.Brush.Cyan", highlighted ? Brushes.Gold : Brushes.SteelBlue),
        BorderThickness = new Thickness(highlighted ? 2 : 1),
        Cursor = System.Windows.Input.Cursors.Hand,
    };
}
