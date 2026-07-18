using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Services;

namespace Dragonfly.Views;

/// <summary>Small helpers for building consistent WPF elements in code-behind.</summary>
public static class UiKit
{
    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];
    public static Style St(string key) => (Style)Application.Current.Resources[key];

    public static Border Card(UIElement child, bool accent = false, Thickness? margin = null) => new()
    {
        Style = St(accent ? "CardAccent" : "Card"),
        Child = child,
        Margin = margin ?? new Thickness(0),
    };

    public static Border StatCard(string label, string value, string note, Brush? valueBrush = null, bool accent = false)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = label.ToUpper(), Style = St("StatLabel"), Margin = new Thickness(0, 0, 0, 6) });
        sp.Children.Add(new TextBlock { Text = value, Style = St("StatValue"), Foreground = valueBrush ?? Res("Text") });
        if (!string.IsNullOrEmpty(note))
            sp.Children.Add(new TextBlock { Text = note, Style = St("Faint"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        return Card(sp, accent);
    }

    public static Border Badge(string text, string bgKey, string fgKey)
    {
        return new Border
        {
            Style = St("Badge"),
            Background = new SolidColorBrush(((SolidColorBrush)Res(bgKey)).Color) { Opacity = 0.16 },
            Child = new TextBlock { Text = text, Foreground = Res(fgKey), FontSize = 11.5, FontWeight = FontWeights.SemiBold },
        };
    }

    public static Border AccentBadge(string text) => new()
    {
        Style = St("Badge"),
        Background = Res("AccentDim"),
        Child = new TextBlock { Text = text, Foreground = Res("Accent"), FontSize = 11.5, FontWeight = FontWeights.SemiBold },
    };

    public static TextBlock Money(decimal v, bool sign = false)
    {
        return new TextBlock
        {
            Text = sign ? Fmt.MoneySigned(v) : Fmt.Money(v),
            Foreground = v < 0 ? Res("Bad") : v > 0 ? Res("Good") : Res("Text"),
            FontWeight = FontWeights.SemiBold,
        };
    }

    public static TextBlock Empty(string text) => new()
    {
        Text = text,
        Foreground = Res("TextFaint"),
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(10, 18, 10, 18),
        FontSize = 13.5,
    };

    public static Button Btn(string text, string styleKey, RoutedEventHandler onClick)
    {
        var b = new Button { Content = text, Style = St(styleKey) };
        b.Click += onClick;
        return b;
    }
}
