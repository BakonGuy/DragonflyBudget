using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;

namespace Dragonfly.Views;

/// <summary>Small helpers for building consistent WPF elements in code-behind.</summary>
public static class UiKit
{
    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];
    public static Style St(string key) => (Style)Application.Current.Resources[key];

    // ── RemixIcon (filled) helpers ──
    /// <summary>A themed RemixIcon glyph. Leave <paramref name="brush"/> null to inherit the parent's foreground.</summary>
    public static PackIconRemixIcon Icon(PackIconRemixIconKind kind, double size = 16, Brush? brush = null)
    {
        var ic = new PackIconRemixIcon { Kind = kind, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        if (brush != null) ic.Foreground = brush;
        return ic;
    }

    /// <summary>A chromeless/ghost button whose content is a single icon (inherits the button's foreground).</summary>
    public static Button IconButton(PackIconRemixIconKind kind, string styleKey, RoutedEventHandler onClick, double size = 15, string? tooltip = null)
    {
        var b = new Button { Style = St(styleKey), Content = Icon(kind, size), ToolTip = tooltip };
        b.Click += onClick;
        return b;
    }

    /// <summary>A section header: accent icon + H2 title, with an optional right-aligned action.</summary>
    public static Grid SectionHeader(PackIconRemixIconKind kind, string title, UIElement? action = null)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var head = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        head.Children.Add(Icon(kind, 18, Res("Accent")));
        head.Children.Add(new TextBlock { Text = title, Style = St("H2"), Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        g.Children.Add(head);
        if (action != null) { Grid.SetColumn(action, 1); g.Children.Add(action); }
        return g;
    }

    /// <summary>
    /// Shared width for right-aligned money columns — wide enough that large values (into the tens of
    /// millions) never clip. Use this for every amount/balance column so they stay consistent.
    /// </summary>
    public static GridLength MoneyCol => new(150);

    /// <summary>A proportional column weight. Tables use these so they always fill the viewport
    /// exactly — no horizontal overflow, and every column rescales when the window does.</summary>
    public static GridLength Star(double weight) => new(weight, GridUnitType.Star);

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
