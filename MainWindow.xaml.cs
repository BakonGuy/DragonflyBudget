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
        BuildNav();
        _views = new UserControl?[_nav.Count];
        Navigate(0);
        Icon = DragonflyIcon.MakeIcon();
        BrandIcon.Source = DragonflyIcon.BuildMediumImage(DragonflyIcon.Accent);
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
