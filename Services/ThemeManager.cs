using System.Windows;
using System.Windows.Media;
using Dragonfly.Models;

namespace Dragonfly.Services;

/// <summary>
/// Swappable colour schemes. The palette is a set of code-built, deliberately <b>unfrozen</b>
/// <see cref="SolidColorBrush"/> instances injected into the app resources <i>before</i> Theme.xaml's
/// styles load — so both the XAML styles and the code-behind bind to these same mutable brushes.
/// Switching a theme mutates each brush's Color in place and the whole UI repaints live. (Brushes
/// defined directly in a compiled XAML ResourceDictionary are auto-frozen and can't do this.)
/// </summary>
public static class ThemeManager
{
    /// <summary>Brush-resource key -> colour, per theme.</summary>
    private static readonly Dictionary<string, string> Purple = new()
    {
        ["Bg"] = "#14121C", ["Bg2"] = "#1A1725", ["Panel"] = "#201C2E", ["Panel2"] = "#282339",
        ["BorderBrush"] = "#35304A", ["BorderSoft"] = "#2B2740",
        ["Accent"] = "#A78BFA", ["AccentStrong"] = "#8B5CF6", ["AccentDim"] = "#8B5CF6", ["AccentSoft"] = "#A78BFA",
        ["Text"] = "#ECEAF4", ["TextDim"] = "#A29CBA", ["TextFaint"] = "#6F6890",
        ["Good"] = "#4ADE80", ["Bad"] = "#F87171", ["Warn"] = "#FBBF24",
        ["ScrollThumbBrush"] = "#4A4463", ["ScrollThumbHoverBrush"] = "#5E567F",
        ["RowHover"] = "#26FFFFFF",
    };

    private static readonly Dictionary<string, string> GreyOrange = new()
    {
        ["Bg"] = "#17171B", ["Bg2"] = "#1D1D22", ["Panel"] = "#232329", ["Panel2"] = "#2C2C34",
        ["BorderBrush"] = "#3C3C45", ["BorderSoft"] = "#2F2F38",
        ["Accent"] = "#FB923C", ["AccentStrong"] = "#F97316", ["AccentDim"] = "#F97316", ["AccentSoft"] = "#FB923C",
        ["Text"] = "#ECECEF", ["TextDim"] = "#A6A6B2", ["TextFaint"] = "#6E6E7A",
        ["Good"] = "#4ADE80", ["Bad"] = "#F87171", ["Warn"] = "#FBBF24",
        ["ScrollThumbBrush"] = "#4A4A55", ["ScrollThumbHoverBrush"] = "#5E5E6C",
        ["RowHover"] = "#26FFFFFF",
    };

    private static readonly Dictionary<string, string> PurpleLight = new()
    {
        ["Bg"] = "#F5F3FF", ["Bg2"] = "#EDE9FE", ["Panel"] = "#FFFFFF", ["Panel2"] = "#F5F3FF",
        ["BorderBrush"] = "#D4D0E0", ["BorderSoft"] = "#E5E0F0",
        ["Accent"] = "#7C3AED", ["AccentStrong"] = "#6D28D9", ["AccentDim"] = "#6D28D9", ["AccentSoft"] = "#7C3AED",
        ["Text"] = "#1E1B36", ["TextDim"] = "#6B6580", ["TextFaint"] = "#9D97B0",
        ["Good"] = "#16A34A", ["Bad"] = "#DC2626", ["Warn"] = "#D97706",
        ["ScrollThumbBrush"] = "#C4BED4", ["ScrollThumbHoverBrush"] = "#B0AAC4",
        ["RowHover"] = "#26000000",
    };

    private static readonly Dictionary<string, string> GreyOrangeLight = new()
    {
        ["Bg"] = "#F7F7F7", ["Bg2"] = "#EEEEEE", ["Panel"] = "#FFFFFF", ["Panel2"] = "#F5F5F5",
        ["BorderBrush"] = "#D4D4D4", ["BorderSoft"] = "#E5E5E5",
        ["Accent"] = "#EA580C", ["AccentStrong"] = "#D4530A", ["AccentDim"] = "#D4530A", ["AccentSoft"] = "#EA580C",
        ["Text"] = "#171717", ["TextDim"] = "#6B6B6B", ["TextFaint"] = "#9E9E9E",
        ["Good"] = "#16A34A", ["Bad"] = "#DC2626", ["Warn"] = "#D97706",
        ["ScrollThumbBrush"] = "#C4C4C4", ["ScrollThumbHoverBrush"] = "#B0B0B0",
        ["RowHover"] = "#26000000",
    };

    /// <summary>Brushes that keep a fixed opacity (the rest are fully opaque).</summary>
    private static readonly Dictionary<string, double> Opacity = new() { ["AccentDim"] = 0.18, ["AccentSoft"] = 0.35 };

    private static Dictionary<string, string> PaletteFor(AppTheme t) => t switch
    {
        AppTheme.GreyOrange => GreyOrange,
        AppTheme.PurpleLight => PurpleLight,
        AppTheme.GreyOrangeLight => GreyOrangeLight,
        _ => Purple,
    };

    public static AppTheme Current { get; private set; } = AppTheme.Purple;
    public static bool IsLight => Current is AppTheme.PurpleLight or AppTheme.GreyOrangeLight;
    public static Color AccentColor { get; private set; } = (Color)ColorConverter.ConvertFromString("#A78BFA");
    public static Color BgColor { get; private set; } = (Color)ColorConverter.ConvertFromString("#14121C");
    public static Color BorderColor { get; private set; } = (Color)ColorConverter.ConvertFromString("#2B2740");

    /// <summary>
    /// Build the palette ResourceDictionary for <paramref name="theme"/> and record its key colours.
    /// The brushes are coloured now (before the dictionary is sealed into app resources and its
    /// Freezables frozen), so the theme is fixed for the session; changing theme takes effect on the
    /// next launch. Merge this in <b>before</b> Theme.xaml so its styles resolve against these brushes.
    /// </summary>
    public static ResourceDictionary BuildPaletteDictionary(AppTheme theme)
    {
        var palette = PaletteFor(theme);
        var rd = new ResourceDictionary();
        foreach (var key in Purple.Keys)
        {
            var color = (Color)ColorConverter.ConvertFromString(palette[key]);
            rd[key] = new SolidColorBrush(color) { Opacity = Opacity.GetValueOrDefault(key, 1.0) };
        }

        Current = theme;
        AccentColor = (Color)ColorConverter.ConvertFromString(palette["Accent"]);
        BgColor = (Color)ColorConverter.ConvertFromString(palette["Bg"]);
        BorderColor = (Color)ColorConverter.ConvertFromString(palette["BorderSoft"]);
        return rd;
    }
}
