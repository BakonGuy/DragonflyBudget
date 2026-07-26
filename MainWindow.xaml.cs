using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Services;
using Dragonfly.Views;
using MahApps.Metro.IconPacks;

namespace Dragonfly;

public partial class MainWindow : Window
{
    private readonly List<(Button Btn, Func<UserControl> Factory)> _nav = new();
    private readonly UserControl?[] _views;
    private readonly UpdateService _updater = new();
    private Button? _active;

    internal UpdateInfo? PendingUpdate { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        AppInfo.ForceUpdate = Environment.GetCommandLineArgs().Any(a =>
            a.Equals("--force-update", StringComparison.OrdinalIgnoreCase));

        RestoreWindowBounds();
        Closing += (_, _) => SaveWindowBounds();
        BuildNav();
        _views = new UserControl?[_nav.Count];
        Navigate(0);
        Icon = DragonflyIcon.MakeIcon();
        BrandIcon.Source = DragonflyIcon.BuildMediumImage(DragonflyIcon.Accent);
        VersionText.Text = $"v{AppInfo.VersionString}";
        SettingsBtn.Content = NavContent(PackIconRemixIconKind.SettingsFill, "Settings");

        // Background auto-update check after window loads
        Loaded += async (_, _) =>
        {
            await Task.Delay(2000);
            await CheckForUpdateAsync();
        };
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
        => new Views.SettingsWindow(this).ShowDialog();

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var result = await _updater.CheckAsync(manual: false);
            if (result.Outcome == UpdateOutcome.Available && result.Info != null)
            {
                PendingUpdate = result.Info;
                ShowUpdateBanner(result.Info);
            }
        }
        catch
        {
            // Swallow — never crash on auto-check
        }
    }

    internal void ShowUpdateBanner(UpdateInfo info)
    {
        UpdateBanner.Content = new StackPanel
        {
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new TextBlock { Text = "⬇", FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = $"Update to v{info.Version}", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center },
                }},
                new TextBlock { Text = "Click to install", Foreground = (Brush)FindResource("TextFaint"), FontSize = 11, Margin = new Thickness(18, 2, 0, 0) },
            }
        };
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void UpdateBanner_Click(object sender, RoutedEventArgs e)
    {
        if (PendingUpdate == null) return;
        UpdateDialog.Show(this, PendingUpdate, _updater);
        var s = App.State.Settings;
        if (s.SkippedUpdateVersion == PendingUpdate.Tag)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            PendingUpdate = null;
        }
    }

    // ── Remember window size/position between sessions ──
    private void RestoreWindowBounds()
    {
        var s = App.State.Settings;
        Width = s.WindowWidth;
        Height = s.WindowHeight;

        if (s.WindowLeft is double left && s.WindowTop is double top && IsOnScreen(left, top, Width, Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowBounds()
    {
        var s = App.State.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (b.Width > 0 && b.Height > 0)
        {
            s.WindowWidth = b.Width;
            s.WindowHeight = b.Height;
            s.WindowLeft = b.Left;
            s.WindowTop = b.Top;
        }
        App.State.Save();
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var vx = SystemParameters.VirtualScreenLeft;
        var vy = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;
        return left + width > vx + 80 && left < vx + vw - 80 &&
               top >= vy - 1 && top < vy + vh - 40;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeTheme.Apply(this);
    }

    private void BuildNav()
    {
        AddNav(PackIconRemixIconKind.DashboardFill, "Dashboard", () => new DashboardView());
        AddNav(PackIconRemixIconKind.BankFill, "Accounts", () => new AccountsView());
        AddNav(PackIconRemixIconKind.BillFill, "Bills", () => new BillsView());
        AddNav(PackIconRemixIconKind.TimeFill, "Pending", () => new PendingView());
        AddNav(PackIconRemixIconKind.ShoppingBasketFill, "Budgets", () => new BudgetsView());
        AddNav(PackIconRemixIconKind.FileList3Fill, "Debts to Pay", () => new DebtsView());
        AddNav(PackIconRemixIconKind.PercentFill, "Repayment", () => new RepaymentView());
    }

    private void AddNav(PackIconRemixIconKind icon, string label, Func<UserControl> factory)
    {
        int index = _nav.Count;
        var btn = new Button
        {
            Content = NavContent(icon, label),
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)FindResource("TextDim"),
            Template = (ControlTemplate)FindResource("NavBtnTemplate"),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        btn.Click += (_, _) => Navigate(index);
        _nav.Add((btn, factory));
        NavPanel.Children.Add(btn);
    }

    private static StackPanel NavContent(PackIconRemixIconKind icon, string label)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new PackIconRemixIcon { Kind = icon, Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
        return sp;
    }

    private void Navigate(int index)
    {
        _views[index] ??= _nav[index].Factory();
        ContentHost.Content = _views[index];

        if (_active != null)
        {
            _active.Tag = null;
            _active.Foreground = (Brush)FindResource("TextDim");
        }
        _active = _nav[index].Btn;
        _active.Tag = "active";
        _active.Foreground = (Brush)FindResource("Accent");
    }
}
