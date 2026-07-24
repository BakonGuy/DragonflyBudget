using System.Windows;
using System.Windows.Controls;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>App settings. Changes apply and persist immediately.</summary>
public class SettingsWindow : Window
{
    private AppState S => App.State;
    private readonly StackPanel _body = new();

    public SettingsWindow(Window owner)
    {
        Title = "Settings";
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = Res("Panel");

        var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };

        var head = new TextBlock { Text = "Settings", Style = St("H2"), Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var close = Btn("Close", "Btn", (_, _) => Close());
        var closeRow = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        closeRow.Children.Add(close);
        DockPanel.SetDock(closeRow, Dock.Bottom);
        root.Children.Add(closeRow);

        root.Children.Add(_body);
        Content = root;

        BuildAppearance();
        BuildBills();

        SourceInitialized += (_, _) => NativeTheme.ApplyDark(this);
    }

    // ── Appearance ──
    private void BuildAppearance()
    {
        _body.Children.Add(SectionHead("Appearance"));

        _body.Children.Add(FieldLabel("Theme"));
        var theme = new ComboBox { Style = St("Combo"), Margin = new Thickness(0, 0, 0, 4) };
        theme.Items.Add("Purple (default)");
        theme.Items.Add("Grey & Orange");
        theme.SelectedIndex = S.Settings.Theme == AppTheme.GreyOrange ? 1 : 0;

        var restartRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        restartRow.Children.Add(new TextBlock { Text = "Restart to apply the new theme.", Foreground = Res("Warn"), VerticalAlignment = VerticalAlignment.Center });
        var restartBtn = Btn("Restart now", "BtnSm", (_, _) => RestartApp());
        restartBtn.Margin = new Thickness(12, 0, 0, 0);
        restartRow.Children.Add(restartBtn);

        theme.SelectionChanged += (_, _) =>
        {
            var chosen = theme.SelectedIndex == 1 ? AppTheme.GreyOrange : AppTheme.Purple;
            if (chosen == S.Settings.Theme) return;
            S.Settings.Theme = chosen;
            S.Save();
            restartRow.Visibility = Visibility.Visible;
        };
        _body.Children.Add(theme);
        _body.Children.Add(Hint("Applies the next time Dragonfly starts."));
        _body.Children.Add(restartRow);
    }

    private void RestartApp()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe)) System.Diagnostics.Process.Start(exe);
        Application.Current.Shutdown();
    }

    // ── Bills ──
    private void BuildBills()
    {
        _body.Children.Add(SectionHead("Bills"));

        var autopay = new CheckBox
        {
            Style = St("Check"),
            Content = "Treat autopay bills as paid on their due date",
            IsChecked = S.Settings.AutopayCountsAsPaid,
            Margin = new Thickness(0, 2, 0, 0),
        };
        autopay.Checked += (_, _) => SetAutopay(true);
        autopay.Unchecked += (_, _) => SetAutopay(false);
        _body.Children.Add(autopay);
        _body.Children.Add(Hint("Autopay bills are assumed paid once the due date arrives, so you don't have to mark them. You can still mark one unpaid on the Bills screen for the rare month it fails."));
    }

    private void SetAutopay(bool on)
    {
        if (S.Settings.AutopayCountsAsPaid == on) return;
        S.Settings.AutopayCountsAsPaid = on;
        S.Save();
    }

    // ── small builders ──
    private static TextBlock SectionHead(string text) => new()
    {
        Text = text,
        Foreground = Res("Accent"),
        FontSize = 12.5,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 16, 0, 10),
    };

    private static TextBlock FieldLabel(string text) => new() { Text = text, Style = St("FieldLabel") };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        Style = St("Faint"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0),
    };
}
