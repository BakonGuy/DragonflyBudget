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

        // With cards: bank + card panels sit side by side across the top. Without cards: the bank
        // panel drops into the left column (matching the attention width) so the breakdown rises up.
        var cardsPanel = BuildCreditCards(s);
        if (cardsPanel != null)
        {
            var accountsRow = new Grid();
            accountsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            accountsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            accountsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var banksTop = BuildBanks(s);
            Grid.SetColumn(banksTop, 0);
            Grid.SetColumn(cardsPanel, 2);
            accountsRow.Children.Add(banksTop);
            accountsRow.Children.Add(cardsPanel);
            Body.Children.Add(accountsRow);
        }

        // ── Two columns: bank (if no cards) + attention + pending on the left, breakdown on the right ──
        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        if (cardsPanel == null) left.Children.Add(BuildBanks(s));
        left.Children.Add(BuildAttention(today, soon));
        left.Children.Add(BuildPending());
        Grid.SetColumn(left, 0);
        cols.Children.Add(left);

        var right = new StackPanel();
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
        // Anything still owed from an earlier month comes first — it's later than anything due soon,
        // and it stays here until it's dealt with.
        var carried = B.CarriedOverBills(S.Month);
        var rows = carried.Select(c => c.Row)
            .Concat(B.BillsFor(S.Month).Where(b => b.Remaining > 0 && b.DueDate <= soon))
            .ToList();
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.ErrorWarningFill, "Needs attention"));

        if (rows.Count == 0)
        {
            panel.Children.Add(Empty("Nothing due in the next 7 days. 🎉"));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var r in rows)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var name = new WrapPanel { Margin = new Thickness(0, 9, 0, 9) };
                name.Children.Add(new TextBlock
                {
                    Text = r.Bill.Name,
                    Foreground = row < carried.Count ? Res("Bad") : Res("Text"),
                    FontWeight = row < carried.Count ? FontWeights.Bold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                if (r.Bill.AutoPay) name.Children.Add(AccentBadge("Auto"));
                Place(table, name, row, 0);

                // Carried-over rows are listed first, so their position identifies them. They show
                // how late they are rather than a bare "Past due" — a date alone from another month
                // reads as this month's.
                bool isCarried = row < carried.Count;
                int late = isCarried ? BudgetService.MonthsBetween(r.Status.Month, S.Month) : 0;

                var due = new WrapPanel { Margin = new Thickness(0, 9, 0, 9), VerticalAlignment = VerticalAlignment.Center };
                due.Children.Add(new TextBlock { Text = r.DueDate.ToString("MMM d"), Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
                if (isCarried) due.Children.Add(Badge(late == 1 ? "1 month late" : $"{late} months late", "Bad", "Bad"));
                else if (r.DueDate < today) due.Children.Add(Badge("Past due", "Bad", "Bad"));
                else if (r.DueDate == today) due.Children.Add(Badge("Today", "Warn", "Warn"));
                Place(table, due, row, 1);

                var amt = Money(r.Remaining);
                amt.HorizontalAlignment = HorizontalAlignment.Right;
                amt.VerticalAlignment = VerticalAlignment.Center;
                Place(table, amt, row, 2);

                var pay = Btn("Mark paid", "BtnSm", (_, _) => { r.Status.Status = PayStatus.Paid; r.Status.AmountPaid = r.Amount; r.Status.UserSet = true; Save(); });
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
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.TimeFill, "Pending this month"));

        if (rows.Count == 0)
        {
            panel.Children.Add(Empty("No pending deposits or withdrawals for this month."));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
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

    // ── Bank accounts (banks + cash = available funds) ──
    private Border BuildBanks(MonthSummary s)
    {
        var win = Window.GetWindow(this)!;
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.BankFill, "Bank Accounts"));

        if (!B.BankAccounts().Any() && B.Data.CashOnHand == 0)
        {
            panel.Children.Add(Empty("No accounts yet. Add them on the Accounts page."));
        }
        else
        {
            var grid = new StackPanel();
            foreach (var bank in B.BankAccounts())
                grid.Children.Add(AccountRow(win, bank));

            grid.Children.Add(Divider());
            var cashRow = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
            cashRow.Children.Add(new TextBlock { Text = "Cash on hand", Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
            var cash = MakeEdit(Fmt.Money(B.Data.CashOnHand), null, left: false, commit: t => { B.Data.CashOnHand = ParseMoney(t); Save(); }, formatMoney: true);
            cash.Margin = new Thickness(8, 0, 0, 0);
            Place(cashRow, cash, 0, 1);
            grid.Children.Add(cashRow);

            grid.Children.Add(Divider());
            grid.Children.Add(TotalLine("Available funds", Fmt.Money(s.TotalFunds), Res("Good")));
            panel.Children.Add(grid);
        }

        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── Credit cards (separate section) ──
    private Border? BuildCreditCards(MonthSummary s)
    {
        var cards = B.CreditCards().ToList();
        if (cards.Count == 0) return null;

        var win = Window.GetWindow(this)!;
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.BankCardFill, "Credit Cards"));

        var grid = new StackPanel();
        foreach (var card in cards)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            decimal bal = B.EffectiveBalance(card);
            var nameCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(card.Name) ? "(unnamed)" : card.Name, VerticalAlignment = VerticalAlignment.Center });
            if (card.CreditLimit > 0)
                nameCol.Children.Add(new TextBlock { Text = $"limit {Fmt.Money(card.CreditLimit)}", Foreground = Res("TextFaint"), FontSize = 11.5 });
            Place(row, nameCol, 0, 0);

            // Editable, exactly like a bank row: typing sets the baseline via SetBalance and stamps
            // LastUserBalanceDate, so linked payments before that date stop being replayed.
            var balTb = MakeEdit(Fmt.Money(bal), null, left: false,
                commit: t => { B.SetBalance(card, ParseMoney(t), BalanceSource.Manual); Save(); }, formatMoney: true);
            balTb.Foreground = bal > 0 ? Res("Bad") : Res("Text");
            balTb.FontWeight = FontWeights.SemiBold;
            balTb.Margin = new Thickness(8, 0, 0, 0);
            Place(row, balTb, 0, 1);

            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(IconButton(PackIconRemixIconKind.LineChartFill, "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, card).ShowDialog(), tooltip: "Balance history"));
            actions.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => AccountDialog.Show(win, B, card, Save), tooltip: "Edit")));
            Place(row, actions, 0, 2);

            grid.Children.Add(row);
        }

        grid.Children.Add(Divider());
        grid.Children.Add(TotalLine("Total owed on cards", Fmt.Money(s.CreditTotal), s.CreditTotal > 0 ? Res("Bad") : Res("Text")));
        panel.Children.Add(grid);
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    private Grid AccountRow(Window win, BankAccount acc)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(acc.Name) ? "(unnamed)" : acc.Name, VerticalAlignment = VerticalAlignment.Center });

        var bal = MakeEdit(Fmt.Money(B.EffectiveBalance(acc)), null, left: false, commit: t => { B.SetBalance(acc, ParseMoney(t), BalanceSource.Manual); Save(); }, formatMoney: true);
        bal.Margin = new Thickness(8, 0, 0, 0);
        Place(row, bal, 0, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(IconButton(PackIconRemixIconKind.LineChartFill, "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, acc).ShowDialog(), tooltip: "Balance history"));
        actions.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => AccountDialog.Show(win, B, acc, Save), tooltip: "Edit")));
        Place(row, actions, 0, 2);

        return row;
    }

    private Border TotalLine(string label, string value, Brush valueBrush)
    {
        var totalRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        totalRow.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Bold });
        var tot = new TextBlock { Text = value, FontWeight = FontWeights.Bold, Foreground = valueBrush, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 30, 0) };
        Place(totalRow, tot, 0, 1);
        return new Border { BorderBrush = Res("BorderSoft"), BorderThickness = new Thickness(0, 1, 0, 0), Child = totalRow, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(0, 6, 0, 0) };
    }

    private Border BuildBreakdown(MonthSummary s)
    {
        bool hasCards = s.CreditTotal != 0 || s.UnpaidFromCard != 0;
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.PieChartFill, "Month breakdown"));

        // ── Bank + cash side: only bills paid from bank/cash lower this.
        panel.Children.Add(Caption("Bank & cash", first: true));
        panel.Children.Add(BreakRow("Balance now", Fmt.Money(s.TotalFunds), Res("Text")));
        panel.Children.Add(BreakRow("Unpaid bills from bank/cash", Fmt.Money(-s.UnpaidFromBank), Res("Bad")));
        panel.Children.Add(BreakRow("Pending deposits", Fmt.MoneySigned(s.PendingIn), Res("Good")));
        panel.Children.Add(BreakRow("Pending withdrawals", Fmt.Money(s.PendingOut), Res("Bad")));
        panel.Children.Add(BreakRow("Projected balance", Fmt.Money(s.EndOfMonthProjection),
            s.EndOfMonthProjection < 0 ? Res("Bad") : Res("Good"), bold: true, topBorder: true));

        // ── Credit-card side: bills paid on a card raise what's owed (shown only when cards are in play).
        if (hasCards)
        {
            panel.Children.Add(Caption("Credit cards"));
            panel.Children.Add(BreakRow("Owed now", Fmt.Money(s.CreditTotal), s.CreditTotal > 0 ? Res("Bad") : Res("Text")));
            panel.Children.Add(BreakRow("Unpaid bills on cards", Fmt.MoneySigned(s.UnpaidFromCard), Res("Bad")));
            panel.Children.Add(BreakRow("Projected owed", Fmt.Money(s.ProjectedCardOwed),
                s.ProjectedCardOwed > 0 ? Res("Bad") : Res("Text"), bold: true, topBorder: true));
        }

        panel.Children.Add(new TextBlock
        {
            Text = "Bills paid from a bank or cash lower your balance; bills paid on a card raise what you owe. Assumes every bill is paid and all pending items happen.",
            Style = St("Faint"), Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        return Card(panel);
    }

    // ── small builders ──
    private static Border Divider() => new()
    {
        BorderBrush = Res("BorderSoft"),
        BorderThickness = new Thickness(0, 1, 0, 0),
        Margin = new Thickness(0, 6, 0, 6),
    };
    private static FrameworkElement Space(Button b) { b.Margin = new Thickness(4, 0, 0, 0); return b; }
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

    // Small accent sub-header used to label the parallel groups in the breakdown.
    private static TextBlock Caption(string text, bool first = false) => new()
    {
        Text = text.ToUpper(),
        Foreground = Res("Accent"),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, first ? 2 : 22, 0, 6),
    };

    private TextBox MakeEdit(string value, string? placeholder, bool left, Action<string> commit, bool formatMoney = false)
    {
        var tb = new TextBox { Text = value, Style = St(left ? "Input" : "InputNum"), MinWidth = 100 };
        if (left) { tb.HorizontalAlignment = HorizontalAlignment.Stretch; tb.TextAlignment = TextAlignment.Left; }
        tb.LostFocus += (_, _) => { if (formatMoney) tb.Text = Fmt.Money(ParseMoney(tb.Text)); commit(tb.Text); };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { if (formatMoney) tb.Text = Fmt.Money(ParseMoney(tb.Text)); commit(tb.Text); Keyboard.ClearFocus(); } };
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
