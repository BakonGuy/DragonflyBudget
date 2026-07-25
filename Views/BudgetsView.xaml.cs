using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class BudgetsView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public BudgetsView()
    {
        InitializeComponent();
        S.MonthChanged += Refresh;
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();
    private void Add_Click(object sender, RoutedEventArgs e) => EditCategory(null);

    private void Refresh()
    {
        Body.Children.Clear();
        var cats = B.ActiveBudgets().ToList();
        string month = S.Month;

        decimal totalCap = cats.Sum(c => c.MonthlyCap);
        decimal totalSpent = cats.Sum(c => B.BudgetSpent(c.Id, month));
        decimal left = totalCap - totalSpent;

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Total budget", Fmt.Money(totalCap), $"{cats.Count} categor{(cats.Count == 1 ? "y" : "ies")}", accent: true));
        AddSpaced(stats, StatCard("Spent this month", Fmt.Money(totalSpent), ""));
        AddSpaced(stats, StatCard("Remaining", Fmt.Money(left), "", left < 0 ? Res("Bad") : Res("Good")));
        Body.Children.Add(stats);

        if (cats.Count == 0)
        {
            Body.Children.Add(Card(Empty("No budget categories yet. Add one — like Groceries with a $600 monthly cap — and log spend as you go.")));
            return;
        }

        var table = new Grid();
        foreach (var w in new[] { new GridLength(1, GridUnitType.Star), MoneyCol, MoneyCol, new GridLength(1, GridUnitType.Star), MoneyCol, GridLength.Auto })
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        AddHeader(table, "CATEGORY", 0);
        AddHeader(table, "SPENT", 1, right: true);
        AddHeader(table, "CAP", 2, right: true);
        AddHeader(table, "USAGE", 3);
        AddHeader(table, "LEFT", 4, right: true);
        AddHeader(table, "", 5);

        var markers = new List<(FrameworkElement, object)>();
        foreach (var cat in cats)
        {
            int row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var c = cat;
            decimal spent = B.BudgetSpent(c.Id, month);
            decimal remaining = c.MonthlyCap - spent;
            double pct = c.MonthlyCap > 0 ? Math.Clamp((double)spent / (double)c.MonthlyCap * 100.0, 0, 100) : (spent > 0 ? 100 : 0);

            var nameCol = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 12, 6, 12), VerticalAlignment = VerticalAlignment.Center };
            nameCol.Children.Add(DragReorder.Handle(c));
            nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(c.Name) ? "(unnamed)" : c.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            Place(table, nameCol, row, 0);
            markers.Add((nameCol, c));

            // Editable "spent so far" for the month.
            var spentBox = new TextBox
            {
                Text = Fmt.Money(spent),
                Style = St("InputNum"),
                MinWidth = 90,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            void CommitSpent()
            {
                var v = ParseMoney(spentBox.Text);
                B.GetBudgetSpend(c.Id, month).Spent = v;
                Save();
            }
            spentBox.LostFocus += (_, _) => CommitSpent();
            spentBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitSpent(); Keyboard.ClearFocus(); } };
            Place(table, spentBox, row, 1);

            Place(table, RightText(Fmt.Money(c.MonthlyCap), Res("TextDim")), row, 2);

            var track = new Border { Background = Res("Bg"), CornerRadius = new CornerRadius(2), Height = 8, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            var fill = new Border { Background = pct >= 100 ? Res("Bad") : pct > 80 ? Res("Warn") : Res("AccentStrong"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            track.Child = fill;
            var p = pct;
            track.Loaded += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
            track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
            Place(table, track, row, 3);

            Place(table, RightText(Fmt.Money(remaining), remaining < 0 ? Res("Bad") : Res("Good")), row, 4);

            var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
            acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => EditCategory(c), tooltip: "Edit")));
            Place(table, acts, row, 5);
        }

        DragReorder.AttachTable(table, markers, MoveCategory);
        Body.Children.Add(Card(table));
        Body.Children.Add(new TextBlock
        {
            Text = "Each month tracks its own spend. Update “Spent” as you go — the bar turns amber past 80% and red once you hit the cap.",
            Style = St("Faint"), Margin = new Thickness(2, 12, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
    }

    private void EditCategory(BudgetCategory? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var cat = existing;
        var dlg = new EditDialog(isNew ? "Add budget category" : "Edit budget category", win);

        var name = EditDialog.Text(cat?.Name ?? "", "e.g. Groceries");
        var cap = new MoneyTextBox(cat?.MonthlyCap ?? 0);

        dlg.Add("Name", name);
        dlg.Add("Monthly cap", cap, full: false);
        dlg.AddHint("The cap is what you aim to stay under each month. Spend is logged per month on the main list.");

        dlg.OnValidate(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; }
            return true;
        });

        if (!isNew)
            dlg.EnableDelete(() => { cat!.Archived = true; Save(); });

        if (dlg.ShowDialog() == true)
        {
            var t = isNew ? new BudgetCategory { SortOrder = B.Data.BudgetCategories.Count } : cat!;
            t.Name = name.Text.Trim();
            t.MonthlyCap = cap.Value;
            if (isNew) B.Data.BudgetCategories.Add(t);
            Save();
        }
    }

    private void MoveCategory(object dragged, object target, bool after)
    {
        var order = B.Data.BudgetCategories.Where(x => !x.Archived).OrderBy(x => x.SortOrder).ToList();
        DragReorder.Reorder(order, (BudgetCategory)dragged, (BudgetCategory)target, after, (c, i) => c.SortOrder = i);
        Save();
    }

    // ── helpers ──
    private static Border RightText(string text, Brush? brush = null) => new()
    {
        Padding = new Thickness(0, 0, 10, 0),
        Child = new TextBlock { Text = text, Foreground = brush ?? Res("Text"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center },
    };
    private static FrameworkElement Space(Button b) { b.Margin = new Thickness(4, 0, 0, 0); return b; }
    private static void AddSpaced(UniformGrid g, FrameworkElement el)
    {
        el.Margin = new Thickness(g.Children.Count == 0 ? 0 : 7, 0, 7, 0);
        g.Children.Add(el);
    }
    private static void AddHeader(Grid g, string text, int col, bool right = false)
    {
        if (g.RowDefinitions.Count == 0) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var b = new Border { BorderBrush = Res("BorderSoft"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 8, 10, 8) };
        b.Child = new TextBlock { Text = text, Foreground = Res("TextFaint"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        Grid.SetRow(b, 0); Grid.SetColumn(b, col); g.Children.Add(b);
    }
    private static void Place(Grid g, UIElement el, int row, int col)
    {
        Grid.SetRow(el, row); Grid.SetColumn(el, col); g.Children.Add(el);
    }
    private static decimal ParseMoney(string? v)
    {
        var t = (v ?? "").Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(t, out var d) ? d : 0;
    }
}
