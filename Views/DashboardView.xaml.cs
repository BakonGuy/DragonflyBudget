using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class DashboardView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public DashboardView()
    {
        InitializeComponent();
        S.MonthChanged += Refresh;
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();

    private void Refresh()
    {
        SubText.Text = $"Your money at a glance for {BudgetService.MonthLabel(S.Month)}";
        Body.Children.Clear();

        var s = B.Summarize(S.Month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(7);

        // ── Stat cards ──
        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Bank + Cash", Fmt.Money(s.TotalFunds), $"{Fmt.Money(s.BankTotal)} bank · {Fmt.Money(s.Cash)} cash", accent: true));
        AddSpaced(stats, StatCard("Bills this month", Fmt.Money(s.TotalMonthlyBills), $"{Fmt.Money(s.TotalUnpaid)} still unpaid ({s.UnpaidCount})"));
        AddSpaced(stats, StatCard("Due in next 7 days", Fmt.Money(s.DueSoon), $"{s.DueSoonCount} bill(s) · {Fmt.Money(s.AfterDueSoonPaid)} left after", s.DueSoon > 0 ? Res("Bad") : Res("Text")));
        AddSpaced(stats, StatCard("After all bills paid", Fmt.Money(s.AmountBehind),
            s.AmountBehind < 0 ? "You're short this much for the month" : "Cushion once every bill is covered",
            s.AmountBehind < 0 ? Res("Bad") : Res("Good")));
        Body.Children.Add(stats);

        // ── Two columns ──
        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(BuildAttention(today, soon));
        left.Children.Add(BuildPending());
        Grid.SetColumn(left, 0);
        cols.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(BuildBanks(s));
        right.Children.Add(BuildBreakdown(s));
        Grid.SetColumn(right, 2);
        cols.Children.Add(right);

        Body.Children.Add(cols);
    }

    private static void AddSpaced(UniformGrid g, FrameworkElement el)
    {
        el.Margin = new Thickness(g.Children.Count == 0 ? 0 : 7, 0, 7, 0);
        g.Children.Add(el);
    }

    // ── Needs attention ──
    private Border BuildAttention(DateOnly today, DateOnly soon)
    {
        var rows = B.BillsFor(S.Month).Where(b => b.Remaining > 0 && b.DueDate <= soon).ToList();
        var panel = new StackPanel();
        panel.Children.Add(SectionHead("⚡ Needs attention", null));

        if (rows.Count == 0)
        {
            panel.Children.Add(Empty("Nothing due in the next 7 days. 🎉"));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var r in rows)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var name = new WrapPanel { Margin = new Thickness(0, 9, 0, 9) };
                name.Children.Add(new TextBlock { Text = r.Bill.Name, VerticalAlignment = VerticalAlignment.Center });
                if (r.Bill.AutoPay) name.Children.Add(AccentBadge("Auto"));
                Place(table, name, row, 0);

                var due = new WrapPanel { Margin = new Thickness(0, 9, 0, 9), VerticalAlignment = VerticalAlignment.Center };
                due.Children.Add(new TextBlock { Text = r.DueDate.ToString("MMM d"), Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
                if (r.DueDate < today) due.Children.Add(Badge("Past due", "Bad", "Bad"));
                else if (r.DueDate == today) due.Children.Add(Badge("Today", "Warn", "Warn"));
                Place(table, due, row, 1);

                var amt = Money(r.Remaining);
                amt.HorizontalAlignment = HorizontalAlignment.Right;
                amt.VerticalAlignment = VerticalAlignment.Center;
                Place(table, amt, row, 2);

                var pay = Btn("Mark paid", "BtnSm", (_, _) => { r.Status.Status = PayStatus.Paid; r.Status.AmountPaid = r.Amount; Save(); });
                pay.Margin = new Thickness(10, 6, 0, 6);
                Place(table, pay, row, 3);
            }
            panel.Children.Add(table);
        }
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── Pending ──
    private Border BuildPending()
    {
        var rows = B.PendingFor(S.Month);
        var panel = new StackPanel();
        panel.Children.Add(SectionHead("⏳ Pending this month", null));

        if (rows.Count == 0)
        {
            panel.Children.Add(Empty("No pending deposits or withdrawals for this month."));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var r in rows)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                double op = r.Status.Cleared ? 0.45 : 1.0;

                var nm = new TextBlock { Text = r.Item.Name, Margin = new Thickness(0, 9, 0, 9), Opacity = op, VerticalAlignment = VerticalAlignment.Center };
                Place(table, nm, row, 0);

                string when = r.Item.ExpectedDate?.ToString("MMM d")
                              ?? (string.IsNullOrWhiteSpace(r.Item.Timeframe) ? "—" : r.Item.Timeframe);
                var whenTb = new TextBlock { Text = when, Foreground = Res("TextDim"), Opacity = op, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                Place(table, whenTb, row, 1);

                var amt = Money(r.Item.Amount, sign: true);
                amt.HorizontalAlignment = HorizontalAlignment.Right;
                amt.VerticalAlignment = VerticalAlignment.Center;
                amt.Opacity = op;
                Place(table, amt, row, 2);

                if (r.Status.Cleared)
                {
                    var b = Badge("Cleared", "Good", "Good");
                    b.HorizontalAlignment = HorizontalAlignment.Right;
                    var wrap = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    wrap.Children.Add(Btn("Undo", "BtnGhost", (_, _) => { r.Status.Cleared = false; Save(); }));
                    Place(table, wrap, row, 3);
                }
                else
                {
                    var clr = Btn("Cleared", "BtnSm", (_, _) => { r.Status.Cleared = true; Save(); });
                    clr.Margin = new Thickness(10, 6, 0, 6);
                    Place(table, clr, row, 3);
                }
            }
            panel.Children.Add(table);
        }
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── Banks editor ──
    private Border BuildBanks(MonthSummary s)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHead("🏦 Banks & cash", Btn("+ Account", "BtnSm", (_, _) =>
        {
            S.Budget.Data.Banks.Add(new BankAccount { Name = "", SortOrder = S.Budget.Data.Banks.Count });
            Save();
        })));

        var grid = new StackPanel();
        foreach (var bank in B.Data.Banks.Where(b => !b.Archived).OrderBy(b => b.SortOrder))
        {
            var bk = bank;
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = MakeEdit(bk.Name, "Account name", left: true, commit: t => { bk.Name = t; Save(); });
            Place(row, name, 0, 0);

            var bal = MakeEdit(bk.Balance.ToString("0.00"), null, left: false, commit: t => { bk.Balance = ParseMoney(t); Save(); });
            bal.Margin = new Thickness(8, 0, 0, 0);
            Place(row, bal, 0, 1);

            var del = Btn("✕", "BtnGhost", (_, _) => { bk.Archived = true; Save(); });
            del.Foreground = Res("Bad");
            del.Margin = new Thickness(4, 0, 0, 0);
            Place(row, del, 0, 2);

            grid.Children.Add(row);
        }

        // cash
        var cashRow = new Grid { Margin = new Thickness(0, 8, 0, 4) };
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        cashRow.Children.Add(new TextBlock { Text = "Cash on hand", Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
        var cash = MakeEdit(B.Data.CashOnHand.ToString("0.00"), null, left: false, commit: t => { B.Data.CashOnHand = ParseMoney(t); Save(); });
        cash.Margin = new Thickness(8, 0, 0, 0);
        Place(cashRow, cash, 0, 1);
        grid.Children.Add(cashRow);

        // total
        var totalRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        totalRow.Children.Add(new TextBlock { Text = "Total", FontWeight = FontWeights.Bold });
        var tot = new TextBlock { Text = Fmt.Money(s.TotalFunds), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 28, 0) };
        Place(totalRow, tot, 0, 1);
        grid.Children.Add(new Border { BorderBrush = Res("BorderSoft"), BorderThickness = new Thickness(0, 1, 0, 0), Child = totalRow, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(0, 6, 0, 0) });

        panel.Children.Add(grid);
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    private Border BuildBreakdown(MonthSummary s)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHead("📉 Month breakdown", null));
        panel.Children.Add(BreakRow("Bank + cash now", Fmt.Money(s.TotalFunds), Res("Text")));
        panel.Children.Add(BreakRow("Unpaid bills", Fmt.Money(-s.TotalUnpaid), Res("Bad")));
        panel.Children.Add(BreakRow("Pending deposits", Fmt.MoneySigned(s.PendingIn), Res("Good")));
        panel.Children.Add(BreakRow("Pending withdrawals", Fmt.Money(s.PendingOut), Res("Bad")));
        panel.Children.Add(BreakRow("Projected end of month", Fmt.Money(s.EndOfMonthProjection),
            s.EndOfMonthProjection < 0 ? Res("Bad") : Res("Good"), bold: true, topBorder: true));
        panel.Children.Add(new TextBlock { Text = "Assumes all bills get paid and all pending items happen.", Style = St("Faint"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        return Card(panel);
    }

    // ── small builders ──
    private static Grid BreakRow(string label, string value, Brush valueBrush, bool bold = false, bool topBorder = false)
    {
        var g = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = label, Foreground = Res("TextDim"), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontSize = bold ? 15 : 14 });
        var v = new TextBlock { Text = value, Foreground = valueBrush, FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        if (topBorder)
            return new Grid { Children = { new Border { BorderBrush = Res("BorderSoft"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0, 4, 0, 0), Child = g } } };
        return g;
    }

    private static Grid SectionHead(string title, UIElement? action)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = title, Style = St("H2") });
        if (action != null) { Grid.SetColumn(action, 1); g.Children.Add(action); }
        return g;
    }

    private TextBox MakeEdit(string value, string? placeholder, bool left, Action<string> commit)
    {
        var tb = new TextBox { Text = value, Style = St(left ? "Input" : "InputNum"), MinWidth = left ? 100 : 100 };
        if (left) { tb.HorizontalAlignment = HorizontalAlignment.Stretch; tb.TextAlignment = TextAlignment.Left; }
        tb.LostFocus += (_, _) => commit(tb.Text);
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { commit(tb.Text); Keyboard.ClearFocus(); } };
        return tb;
    }

    private static void Place(Grid g, UIElement el, int row, int col)
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        g.Children.Add(el);
    }

    private static decimal ParseMoney(string? v)
    {
        var t = (v ?? "").Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(t, out var d) ? d : 0;
    }
}
