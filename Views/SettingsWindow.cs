using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;
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
        BuildUpdates();

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

    // ── Updates ──
    private void BuildUpdates()
    {
        _body.Children.Add(SectionHead("Updates"));

        // Current version
        var versionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        versionRow.Children.Add(Icon(PackIconRemixIconKind.InformationFill, 16, Res("Accent")));
        versionRow.Children.Add(new TextBlock
        {
            Text = $"Current version: v{AppInfo.VersionString}",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 13.5,
            Foreground = Res("TextDim"),
        });
        _body.Children.Add(versionRow);

        // Auto-check toggle
        var autoCheck = new CheckBox
        {
            Style = St("Check"),
            Content = "Check for updates automatically",
            IsChecked = S.Settings.CheckForUpdates,
            Margin = new Thickness(0, 2, 0, 0),
        };
        autoCheck.Checked += (_, _) => ToggleAutoCheck(true);
        autoCheck.Unchecked += (_, _) => ToggleAutoCheck(false);
        _body.Children.Add(autoCheck);

        // Manual check
        var status = new TextBlock
        {
            Style = St("Faint"),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Button checkBtn = null!;
        StackPanel checkRow = null!;
        checkBtn = Btn("Check for updates", "BtnSm", async (_, _) =>
        {
            checkBtn.IsEnabled = false;
            checkBtn.Content = "Checking...";
            status.Text = "";

            var result = await new UpdateService().CheckAsync(manual: true);

            checkBtn.IsEnabled = true;
            checkBtn.Content = "Check for updates";

            switch (result.Outcome)
            {
                case UpdateOutcome.UpToDate:
                    status.Text = "You're on the latest version.";
                    status.Foreground = Res("Good");
                    break;

                case UpdateOutcome.Available when result.Info != null:
                    status.Text = $"Update available: v{result.Info.Version}";
                    status.Foreground = Res("Accent");
                    var updateNow = Btn("Update now", "BtnGhost", (_, _) =>
                    {
                        UpdateDialog.Show(Owner, result.Info, new UpdateService());
                    });
                    updateNow.Margin = new Thickness(8, 0, 0, 0);
                    updateNow.FontSize = 12;
                    checkRow.Children.Add(updateNow);
                    break;

                case UpdateOutcome.Throttled:
                    var secs = result.RetryAfter?.TotalSeconds ?? 0;
                    status.Text = $"Please wait {Math.Ceiling(secs)}s before checking again.";
                    status.Foreground = Res("Warn");
                    break;

                case UpdateOutcome.NoAsset when result.Info != null:
                    status.Text = "A new version exists but has no installer asset — ";
                    status.Foreground = Res("Warn");
                    var viewLink = Btn("View release", "BtnGhost", (_, _) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.Info.HtmlUrl) { UseShellExecute = true });
                        }
                        catch { }
                    });
                    viewLink.FontSize = 12;
                    viewLink.Margin = new Thickness(0, 0, 0, 0);
                    checkRow.Children.Add(viewLink);
                    break;

                case UpdateOutcome.Failed:
                    status.Text = result.Error ?? "Couldn't check for updates. Check your connection.";
                    status.Foreground = Res("Bad");
                    break;
            }
        });
        checkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        checkRow.Children.Add(checkBtn);
        checkRow.Children.Add(status);
        _body.Children.Add(checkRow);

        // Last checked
        var lastChecked = new TextBlock
        {
            Style = St("Faint"),
            Margin = new Thickness(0, 8, 0, 0),
        };
        UpdateLastCheckedLabel(lastChecked);
        App.State.DataChanged += () => Dispatcher.Invoke(() => UpdateLastCheckedLabel(lastChecked));
        _body.Children.Add(lastChecked);
    }

    private static void UpdateLastCheckedLabel(TextBlock label)
    {
        var last = App.State.Settings.LastUpdateCheckUtc;
        label.Text = last.HasValue ? $"Last checked: {last.Value.ToLocalTime():g}" : "";
        label.Visibility = last.HasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleAutoCheck(bool on)
    {
        if (S.Settings.CheckForUpdates == on) return;
        S.Settings.CheckForUpdates = on;
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
