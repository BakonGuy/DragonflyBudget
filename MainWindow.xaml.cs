using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dragonfly.Views;

namespace Dragonfly;

public partial class MainWindow : Window
{
    private readonly List<(Button Btn, Func<UserControl> Factory)> _nav = new();
    private readonly UserControl?[] _views;
    private Button? _active;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowBounds();
        Closing += (_, _) => SaveWindowBounds();
        BuildNav();
        _views = new UserControl?[_nav.Count];
        Navigate(0);
        Icon = DragonflyIcon.MakeIcon();
        BrandIcon.Source = DragonflyIcon.BuildMediumImage(DragonflyIcon.Accent);
        VersionText.Text = $"v{AppVersion}";
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
        => new Views.SettingsWindow(this).ShowDialog();

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
        // RestoreBounds gives the normal (un-maximized) rectangle, so we always store a usable size.
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

    /// <summary>True if a decent chunk of the window would land on a visible monitor.</summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var vx = SystemParameters.VirtualScreenLeft;
        var vy = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;
        // Require the title-bar area to be within the virtual desktop.
        return left + width > vx + 80 && left < vx + vw - 80 &&
               top >= vy - 1 && top < vy + vh - 40;
    }

    private static string AppVersion
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            // <Version> flows to InformationalVersion (may carry a +git suffix); fall back to the file version.
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
                return info.Split('+')[0];
            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeTheme.ApplyDark(this);
    }

    private void BuildNav()
    {
        AddNav("◈", "Dashboard", () => new DashboardView());
        AddNav("🗓", "Bills", () => new BillsView());
        AddNav("⏳", "Pending", () => new PendingView());
        AddNav("📋", "Debts to Pay", () => new DebtsView());
        AddNav("﹪", "Repayment", () => new RepaymentView());
    }

    private void AddNav(string icon, string label, Func<UserControl> factory)
    {
        int index = _nav.Count;
        var btn = new Button
        {
            Content = $"{icon}     {label}",
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)FindResource("TextDim"),
            Template = (ControlTemplate)FindResource("NavBtnTemplate"),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.Click += (_, _) => Navigate(index);
        _nav.Add((btn, factory));
        NavPanel.Children.Add(btn);
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
