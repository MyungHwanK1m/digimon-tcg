using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Wpf.Themes;

/// <summary>
/// 코드 비하인드에서 CYBER NEO 테마 리소스(Themes/CyberNeo.xaml)를 조회하는 진입점.
/// </summary>
public static class Theme
{
    public static Brush GetBrush(string key) => (Brush)Application.Current.FindResource(key);

    public static Effect GetEffect(string key) => (Effect)Application.Current.FindResource(key);

    public static SolidColorBrush GetCardColorBrush(CardColor color) => (SolidColorBrush)GetBrush(color switch
    {
        CardColor.Red => "Neo.Brush.Card.Red",
        CardColor.Blue => "Neo.Brush.Card.Blue",
        CardColor.Yellow => "Neo.Brush.Card.Yellow",
        CardColor.Green => "Neo.Brush.Card.Green",
        CardColor.Black => "Neo.Brush.Card.Black",
        CardColor.Purple => "Neo.Brush.Card.Purple",
        CardColor.White => "Neo.Brush.Card.White",
        _ => "Neo.Brush.Card.Unknown"
    });
}

/// <summary>
/// 컨트롤 템플릿에 전달하는 테마 전용 첨부 속성.
/// </summary>
public static class ThemeAssist
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(ThemeAssist), new FrameworkPropertyMetadata(new CornerRadius(8)));

    public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);
}
