using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;
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

        var pref = B.GetSort("bills", "date");
        var rows = BudgetService.SortBills(B.BillsFor(S.Month), pref);
        bool manual = pref.Key == "manual";
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

        Body.Children.Add(SortBar("bills", "date"));

        var table = new Grid();
        foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(120), MoneyCol, new GridLength(150), new GridLength(170), GridLength.Auto })
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });

        AddHeader(table, "BILL", 0);
        AddHeader(table, "DUE", 1);
        AddHeader(table, "AMOUNT", 2, right: true);
        AddHeader(table, "STATUS", 3);
        AddHeader(table, "ACCOUNT", 4);
        AddHeader(table, "", 5);

        var markers = new List<(FrameworkElement, object)>();
        foreach (var r in rows)
        {
            int row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bool done = r.Effective is PayStatus.Paid or PayStatus.Skipped;
            bool overdue = r.Remaining > 0 && r.DueDate < today;
            double op = done ? 0.5 : 1.0;

            if (overdue)
            {
                var bg = new Border { Background = new SolidColorBrush(((SolidColorBrush)Res("Bad")).Color) { Opacity = 0.06 } };
                Grid.SetRow(bg, row); Grid.SetColumnSpan(bg, 6); table.Children.Add(bg);
            }

            // name
            var name = new WrapPanel { Margin = new Thickness(10, 10, 6, 10), Opacity = op };
            if (manual) name.Children.Add(DragReorder.Handle(r.Bill));
            name.Children.Add(new TextBlock { Text = r.Bill.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            if (r.Bill.Recurrence == Recurrence.OneOff) name.Children.Add(Badge("One-off", "TextDim", "TextDim"));
            if (r.Bill.AutoPay) name.Children.Add(AccentBadge("Auto"));
            Place(table, name, row, 0);
            markers.Add((name, r.Bill));

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
            stCell.Children.Add(r.AutoPaid
                ? Badge("Auto-paid", "Accent", "Accent")
                : r.Effective switch
                {
                    PayStatus.Paid => Badge("Paid", "Good", "Good"),
                    PayStatus.Partial => Badge($"Partial · {Fmt.Money(r.Remaining)} left", "Warn", "Warn"),
                    PayStatus.Skipped => Badge("Skipped", "TextFaint", "TextFaint"),
                    _ => Badge("Unpaid", "TextDim", "TextDim"),
                });
            Place(table, stCell, row, 3);

            // payment
            // Account first, then the optional sub-info label.
            string pm = string.Join(" · ", new[] { r.Bill.AccountName, r.Bill.PaymentMethod }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            Place(table, new TextBlock { Text = pm, Foreground = Res("TextDim"), FontSize = 12.5, Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = op, TextTrimming = TextTrimming.CharacterEllipsis }, row, 4);

            // actions
            var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
            if (r.Remaining > 0)
            {
                acts.Children.Add(Space(Btn("Paid", "BtnSm", (_, _) => { r.Status.Status = PayStatus.Paid; r.Status.AmountPaid = r.Amount; r.Status.UserSet = true; Save(); })));
                acts.Children.Add(Space(Btn("Partial", "BtnGhost", (_, _) => PartialDialog(r))));
            }
            else if (r.AutoPaid)
            {
                // Autopay assumed it paid — let the user flag the rare failure.
                acts.Children.Add(Space(Btn("Mark unpaid", "BtnGhost", (_, _) => { r.Status.Status = PayStatus.Unpaid; r.Status.AmountPaid = 0; r.Status.PaidDate = null; r.Status.UserSet = true; Save(); })));
            }
            else
            {
                acts.Children.Add(Space(Btn("Undo", "BtnGhost", (_, _) => { r.Status.Status = PayStatus.Unpaid; r.Status.AmountPaid = 0; r.Status.PaidDate = null; r.Status.UserSet = true; Save(); })));
            }
            acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => EditBill(r.Bill), tooltip: "Edit")));
            Place(table, acts, row, 5);
        }

        if (manual) DragReorder.AttachTable(table, markers, MoveBill);
        // Persist widths for cols 1–4; no handle between DUE (left-aligned) and AMOUNT (right-aligned).
        ColumnResize.Apply(table, "bills", new[] { 1, 2, 3, 4 }, S.Settings, Save, handleCols: new[] { 1, 3, 4 });
        Body.Children.Add(Card(table));
    }

    // ── partial payment dialog ──
    private void PartialDialog(BillRow r)
    {
        var win = Window.GetWindow(this)!;
        var dlg = new EditDialog($"Record payment — {r.Bill.Name}", win);
        var paid = new MoneyTextBox(r.Status.AmountPaid);
        var over = new MoneyTextBox(r.Amount);
        var paidOn = new DatePicker
        {
            SelectedDate = (r.Status.PaidDate ?? r.DueDate).ToDateTime(TimeOnly.MinValue),
            Height = 36, FontSize = 14,
        };
        dlg.Add($"Amount paid so far (of {Fmt.Money(r.Amount)})", paid, full: false);
        dlg.Add("This month's amount (override)", over, full: false, rightColumn: true);
        dlg.Add("Paid on", paidOn, full: false);
        dlg.AddHint("Overriding the amount only changes this one month. The paid date defaults to the due date.");
        dlg.OnValidate(() => true);
        if (dlg.ShowDialog() == true)
        {
            var st = r.Status;
            decimal ov = over.Value;
            st.AmountOverride = ov == r.Bill.Amount ? null : ov;
            decimal amount = st.AmountOverride ?? r.Bill.Amount;
            st.AmountPaid = paid.Value;
            st.Status = st.AmountPaid <= 0 ? PayStatus.Unpaid : st.AmountPaid >= amount ? PayStatus.Paid : PayStatus.Partial;
            var pd = paidOn.SelectedDate.HasValue ? DateOnly.FromDateTime(paidOn.SelectedDate.Value) : (DateOnly?)null;
            st.PaidDate = pd == r.DueDate ? null : pd;   // null = use the due date
            st.UserSet = true;
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
        var amount = new MoneyTextBox(b?.Amount ?? 0);
        var dueDay = new DayPicker(b?.DueDay ?? 1);
        var recur = new RecurrenceEditor(b?.Repeat ?? (b != null ? Schedule.From(b.Recurrence) : Schedule.Monthly), "One-off (this month only)");
        var start = new MonthPicker(); start.Set(b?.StartMonth ?? S.Month);
        var end = new MonthPicker(allowEmpty: true); end.Set(b?.EndMonth);
        var autopay = EditDialog.Combo(new[] { "Manual", "Autopay" }, (b?.AutoPay ?? false) ? "Autopay" : "Manual");
        var method = EditDialog.EditableCombo(B.PaymentMethods(), b?.PaymentMethod ?? "", sort: false);
        var account = EditDialog.AccountCombo(B.TrackedAccountNames(), B.ExtraAccountNames(), b?.AccountName ?? "");
        var notes = EditDialog.Notes(b?.Notes ?? "");

        dlg.AddSection("Bill");
        dlg.Add("Name", name);
        dlg.Add("Amount", amount, full: false);
        dlg.Add("Due day of month", dueDay, full: false, rightColumn: true);

        dlg.AddSection("Schedule");
        dlg.Add("Repeats", recur);
        dlg.Add("Starts", start, full: false);
        dlg.Add("Ends (blank = indefinite)", end, full: false, rightColumn: true);

        dlg.AddSection("Payment");
        dlg.Add("Pay from account", account);
        dlg.Add("Sub info", method, full: false);
        dlg.Add("Autopay?", autopay, full: false, rightColumn: true);
        dlg.AddHint("“Pay from account”: pick a tracked account so its balance updates automatically when the bill is paid, or type any name. “Sub info” is an optional label like Checking, Savings, or Direct Deposit.");

        dlg.AddSection("Notes");
        dlg.Add("Anything to remember", notes);

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
            var sched = recur.Value;
            target.Name = name.Text.Trim();
            target.Amount = amount.Value;
            target.DueDay = dueDay.Day;
            target.Repeat = sched;
            target.Recurrence = sched.IsOneOff ? Recurrence.OneOff : Recurrence.Monthly;
            target.StartMonth = start.Value ?? S.Month;
            target.EndMonth = sched.IsOneOff ? null : end.Value;
            target.AutoPay = autopay.SelectedIndex == 1;
            target.PaymentMethod = (method.Text ?? "").Trim();
            target.AccountName = (account.Text ?? "").Trim();
            // Link to a real account by name (enables balance auto-deduction + history).
            target.AccountId = B.Data.Banks
                .FirstOrDefault(x => !x.Archived && string.Equals(x.Name, target.AccountName, StringComparison.OrdinalIgnoreCase))?.Id;
            target.Notes = notes.Text.Trim();
            if (isNew) { target.SortOrder = B.Data.Bills.Count; B.Data.Bills.Add(target); }
            Save();
        }
    }

    // ── sorting / manual order ──
    private static readonly (string, string)[] SortOptions =
    {
        ("date", "Due date"), ("amount", "Amount"), ("status", "Status"),
        ("name", "Name"), ("manual", "Manual"),
    };

    private FrameworkElement SortBar(string screen, string def)
    {
        var bar = new DockPanel { Margin = new Thickness(2, 0, 2, 10) };
        var sort = new SortControl(B.GetSort(screen, def), SortOptions, () => { Save(); Refresh(); });
        DockPanel.SetDock(sort, Dock.Right);
        bar.Children.Add(sort);
        bar.Children.Add(new TextBlock());   // filler so the control right-aligns
        return bar;
    }

    private void MoveBill(object dragged, object target, bool after)
    {
        var order = B.Data.Bills.OrderBy(x => x.SortOrder).ToList();
        DragReorder.Reorder(order, (Bill)dragged, (Bill)target, after, (b, i) => b.SortOrder = i);
        Save();
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
