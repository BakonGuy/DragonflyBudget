using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class BillsView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public BillsView()
    {
        InitializeComponent();
        S.MonthChanged += Refresh;
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();
    private void Add_Click(object sender, RoutedEventArgs e) => EditBill(null);

    private void Refresh()
    {
        SubText.Text = $"Bills for {BudgetService.MonthLabel(S.Month)}. Every month keeps its own paid history.";
        Body.Children.Clear();

        var rows = B.BillsFor(S.Month);
        var s = B.Summarize(S.Month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(7);

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Total monthly", Fmt.Money(s.TotalMonthlyBills), ""));
        AddSpaced(stats, StatCard("Total unpaid", Fmt.Money(s.TotalUnpaid), ""));
        AddSpaced(stats, StatCard("Due in 7 days", Fmt.Money(s.DueSoon), "", s.DueSoon > 0 ? Res("Bad") : Res("Text")));
        AddSpaced(stats, StatCard("Bank after due-soon paid", Fmt.Money(s.AfterDueSoonPaid), "", s.AfterDueSoonPaid < 0 ? Res("Bad") : Res("Good")));
        Body.Children.Add(stats);

        if (rows.Count == 0)
        {
            Body.Children.Add(Card(Empty("No bills for this month yet. Add one — recurring bills automatically show in every month they cover.")));
            return;
        }

        var table = new Grid();
        foreach (var w in new[] { new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(110), GridLength.Auto, new GridLength(150), GridLength.Auto })
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });

        AddHeader(table, "BILL", 0);
        AddHeader(table, "DUE", 1);
        AddHeader(table, "AMOUNT", 2, right: true);
        AddHeader(table, "STATUS", 3);
        AddHeader(table, "PAYMENT", 4);
        AddHeader(table, "", 5);

        foreach (var r in rows)
        {
            int row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bool done = r.Status.Status is PayStatus.Paid or PayStatus.Skipped;
            bool overdue = r.Remaining > 0 && r.DueDate < today;
            double op = done ? 0.5 : 1.0;

            if (overdue)
            {
                var bg = new Border { Background = new SolidColorBrush(((SolidColorBrush)Res("Bad")).Color) { Opacity = 0.06 } };
                Grid.SetRow(bg, row); Grid.SetColumnSpan(bg, 6); table.Children.Add(bg);
            }

            // name
            var name = new WrapPanel { Margin = new Thickness(10, 10, 6, 10), Opacity = op };
            name.Children.Add(new TextBlock { Text = r.Bill.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            if (r.Bill.Recurrence == Recurrence.OneOff) name.Children.Add(Badge("One-off", "TextDim", "TextDim"));
            if (r.Bill.AutoPay) name.Children.Add(AccentBadge("Auto"));
            Place(table, name, row, 0);

            // due
            var due = new WrapPanel { Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = op };
            due.Children.Add(new TextBlock { Text = r.DueDate.ToString("MMM d"), Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
            if (r.Remaining > 0 && r.DueDate < today) due.Children.Add(Badge("Past due", "Bad", "Bad"));
            else if (r.Remaining > 0 && r.DueDate <= soon) due.Children.Add(Badge("Soon", "Warn", "Warn"));
            Place(table, due, row, 1);

            // amount
            var amtPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Opacity = op };
            amtPanel.Children.Add(new TextBlock { Text = Fmt.Money(r.Amount), HorizontalAlignment = HorizontalAlignment.Right });
            if (r.Status.Status == PayStatus.Partial)
                amtPanel.Children.Add(new TextBlock { Text = $"{Fmt.Money(r.Status.AmountPaid)} paid", Foreground = Res("Warn"), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right });
            Place(table, amtPanel, row, 2);

            // status
            var stCell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            stCell.Children.Add(r.Status.Status switch
            {
                PayStatus.Paid => Badge("Paid", "Good", "Good"),
                PayStatus.Partial => Badge($"Partial · {Fmt.Money(r.Remaining)} left", "Warn", "Warn"),
                PayStatus.Skipped => Badge("Skipped", "TextFaint", "TextFaint"),
                _ => Badge("Unpaid", "TextDim", "TextDim"),
            });
            Place(table, stCell, row, 3);

            // payment
            string pm = r.Bill.PaymentMethod + (string.IsNullOrWhiteSpace(r.Bill.AccountName) ? "" : $" · {r.Bill.AccountName}");
            Place(table, new TextBlock { Text = pm, Foreground = Res("TextDim"), FontSize = 12.5, Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = op, TextTrimming = TextTrimming.CharacterEllipsis }, row, 4);

            // actions
            var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
            if (r.Remaining > 0)
            {
                acts.Children.Add(Space(Btn("Paid", "BtnSm", (_, _) => { r.Status.Status = PayStatus.Paid; r.Status.AmountPaid = r.Amount; Save(); })));
                acts.Children.Add(Space(Btn("Partial", "BtnGhost", (_, _) => PartialDialog(r))));
            }
            else
            {
                acts.Children.Add(Space(Btn("Undo", "BtnGhost", (_, _) => { r.Status.Status = PayStatus.Unpaid; r.Status.AmountPaid = 0; Save(); })));
            }
            acts.Children.Add(Space(Btn("✎", "BtnGhost", (_, _) => EditBill(r.Bill))));
            Place(table, acts, row, 5);
        }

        Body.Children.Add(Card(table));
    }

    // ── partial payment dialog ──
    private void PartialDialog(BillRow r)
    {
        var win = Window.GetWindow(this)!;
        var dlg = new EditDialog($"Partial payment — {r.Bill.Name}", win);
        var paid = EditDialog.Text(r.Status.AmountPaid.ToString("0.00"));
        var over = EditDialog.Text(r.Amount.ToString("0.00"));
        dlg.Add($"Amount paid so far (of {Fmt.Money(r.Amount)})", paid);
        dlg.Add("This month's amount (override)", over);
        dlg.AddHint("Overriding only changes this one month — e.g. a higher electric bill.");
        dlg.OnValidate(() => true);
        if (dlg.ShowDialog() == true)
        {
            var st = r.Status;
            decimal ov = ParseMoney(over.Text);
            st.AmountOverride = ov == r.Bill.Amount ? null : ov;
            decimal amount = st.AmountOverride ?? r.Bill.Amount;
            st.AmountPaid = ParseMoney(paid.Text);
            st.Status = st.AmountPaid <= 0 ? PayStatus.Unpaid : st.AmountPaid >= amount ? PayStatus.Paid : PayStatus.Partial;
            Save();
        }
    }

    // ── bill edit dialog ──
    private void EditBill(Bill? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var b = existing;
        var dlg = new EditDialog(isNew ? "Add bill" : "Edit bill", win);

        var name = EditDialog.Text(b?.Name ?? "", "e.g. Electric");
        var amount = EditDialog.Text((b?.Amount ?? 0).ToString("0.00"));
        var dueDay = EditDialog.Text((b?.DueDay ?? 1).ToString());
        var recur = EditDialog.Combo(new[] { "Repeats monthly", "One-off (this month only)" },
            (b?.Recurrence ?? Recurrence.Monthly) == Recurrence.OneOff ? "One-off (this month only)" : "Repeats monthly");
        var start = new MonthPicker(); start.Set(b?.StartMonth ?? S.Month);
        var end = new MonthPicker(allowEmpty: true); end.Set(b?.EndMonth);
        var autopay = EditDialog.Combo(new[] { "Manual", "Autopay" }, (b?.AutoPay ?? false) ? "Autopay" : "Manual");
        var method = EditDialog.Text(b?.PaymentMethod ?? "", "e.g. PayPal, Direct");
        var account = EditDialog.Text(b?.AccountName ?? "", "e.g. Capital One");
        var notes = EditDialog.Notes(b?.Notes ?? "");

        dlg.Add("Name", name);
        dlg.Add("Amount", amount, full: false);
        dlg.Add("Due day of month (1–31)", dueDay, full: false, rightColumn: true);
        dlg.Add("Type", recur, full: false);
        dlg.Add("Starts", start, full: false, rightColumn: true);
        dlg.Add("Ends (leave blank = indefinite)", end, full: false);
        dlg.Add("Autopay?", autopay, full: false, rightColumn: true);
        dlg.Add("Payment method", method, full: false);
        dlg.Add("From account", account, full: false, rightColumn: true);
        dlg.Add("Notes", notes);

        dlg.OnValidate(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; }
            return true;
        });

        if (!isNew)
            dlg.EnableDelete(() =>
            {
                B.Data.Bills.RemoveAll(x => x.Id == b!.Id);
                B.Data.BillStatuses.RemoveAll(x => x.BillId == b!.Id);
                Save();
            });

        if (dlg.ShowDialog() == true)
        {
            var target = isNew ? new Bill() : B.Data.Bills.First(x => x.Id == b!.Id);
            target.Name = name.Text.Trim();
            target.Amount = ParseMoney(amount.Text);
            target.DueDay = int.TryParse(dueDay.Text, out var dd) ? Math.Clamp(dd, 1, 31) : 1;
            target.Recurrence = recur.SelectedIndex == 1 ? Recurrence.OneOff : Recurrence.Monthly;
            target.StartMonth = start.Value ?? S.Month;
            target.EndMonth = target.Recurrence == Recurrence.OneOff ? null : end.Value;
            target.AutoPay = autopay.SelectedIndex == 1;
            target.PaymentMethod = method.Text.Trim();
            target.AccountName = account.Text.Trim();
            target.Notes = notes.Text.Trim();
            if (isNew) B.Data.Bills.Add(target);
            Save();
        }
    }

    // ── helpers ──
    private static void AddSpaced(UniformGrid g, FrameworkElement el)
    {
        el.Margin = new Thickness(g.Children.Count == 0 ? 0 : 7, 0, 7, 0);
        g.Children.Add(el);
    }
    private static FrameworkElement Space(Button b) { b.Margin = new Thickness(6, 0, 0, 0); return b; }
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
