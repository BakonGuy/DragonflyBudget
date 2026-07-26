using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using static Dragonfly.Views.UiKit;
using WpfColor = System.Windows.Media.Color;

namespace Dragonfly.Views;

/// <summary>Balance-over-time chart for an account: user updates and automatic bill payments in different colours.</summary>
public class BalanceHistoryWindow : Window
{
    public BalanceHistoryWindow(Window owner, BudgetService b, BankAccount acc)
    {
        Title = $"Balance history — {acc.Name}";
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 760; Height = 480;
        Background = Res("Panel");
        Foreground = Res("Text");

        var pts = b.BalanceSeries(acc);

        var root = new DockPanel { Margin = new Thickness(20) };
        var head = new TextBlock { Text = $"{acc.Name} — balance over time", Style = St("H2"), Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        legend.Children.Add(Bullet(ThemeManager.AccentColor, "your updates"));
        legend.Children.Add(Bullet((WpfColor)ColorConverter.ConvertFromString("#FBBF24"), "automatic (bills paid)"));
        DockPanel.SetDock(legend, Dock.Top);
        root.Children.Add(legend);

        if (pts.Count == 0)
        {
            root.Children.Add(Empty("No balance history yet. Update this account's balance, or mark linked bills paid, to build a history."));
            Content = root;
            SourceInitialized += (_, _) => NativeTheme.Apply(this);
            return;
        }

        var manualColor = ToSk(ThemeManager.AccentColor);
        var autoColor = ToSk((WpfColor)ColorConverter.ConvertFromString("#FBBF24"));
        var lineColor = ToSk(((SolidColorBrush)Res("TextDim")).Color);

        var line = new LineSeries<double>
        {
            Values = pts.Select(p => (double)p.Balance).ToArray(),
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(lineColor, 2),
            LineSmoothness = 0,
        };
        var manual = new ScatterSeries<ObservablePoint>
        {
            Values = pts.Select((p, i) => p.IsManual ? new ObservablePoint(i, (double)p.Balance) : null!).Where(x => x != null).ToArray(),
            GeometrySize = 12,
            Fill = new SolidColorPaint(manualColor),
            Stroke = null,
            Name = "Your updates",
        };
        var auto = new ScatterSeries<ObservablePoint>
        {
            Values = pts.Select((p, i) => !p.IsManual ? new ObservablePoint(i, (double)p.Balance) : null!).Where(x => x != null).ToArray(),
            GeometrySize = 10,
            Fill = new SolidColorPaint(autoColor),
            Stroke = null,
            Name = "Bills paid",
        };

        var axisPaint = new SolidColorPaint(ToSk(((SolidColorBrush)Res("TextFaint")).Color));
        var chart = new LiveChartsCore.SkiaSharpView.WPF.CartesianChart
        {
            Background = Res("Bg"),
            Series = new ISeries[] { line, manual, auto },
            XAxes = new[]
            {
                new Axis
                {
                    Labels = pts.Select(p => p.Date.ToString("MMM d")).ToArray(),
                    LabelsPaint = axisPaint,
                    TextSize = 12,
                    SeparatorsPaint = new SolidColorPaint(ToSk(((SolidColorBrush)Res("BorderSoft")).Color)) { StrokeThickness = 1 },
                },
            },
            YAxes = new[]
            {
                new Axis
                {
                    Labeler = v => "$" + v.ToString("N0"),
                    LabelsPaint = axisPaint,
                    TextSize = 12,
                    SeparatorsPaint = new SolidColorPaint(ToSk(((SolidColorBrush)Res("BorderSoft")).Color)) { StrokeThickness = 1 },
                },
            },
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden,
        };
        root.Children.Add(chart);

        Content = root;
        SourceInitialized += (_, _) => NativeTheme.Apply(this);
    }

    private static SKColor ToSk(WpfColor c) => new(c.R, c.G, c.B, c.A);

    private static StackPanel Bullet(WpfColor color, string label)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
        sp.Children.Add(new System.Windows.Shapes.Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        sp.Children.Add(new TextBlock { Text = label, Foreground = Res("TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }
}
