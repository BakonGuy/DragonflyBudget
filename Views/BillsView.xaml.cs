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
    private void AddLoan_Click(object sender, RoutedEventArgs e) => EditLoan(null);

    private void Refresh()
    {
        SubText.Text = $"Bills for {BudgetService.MonthLabel(S.Month)}. Every month keeps its own paid history.";
        Body.Children.Clear();

        var pref = B.GetSort("bills", "date");
        var rows = BudgetService.SortBills(B.BillsFor(S.Month), pref);
        bool manual = pref.Key == "manual";
        var s = B.Summarize(S.Month);

        // Bills left unpaid in earlier months. Not new records — each row still belongs to the month
        // it came from, so acting on one writes its own month's history, not this month's.
        var carried = B.CarriedOverBills(S.Month);

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Total monthly", Fmt.Money(s.TotalMonthlyBills), ""));
        AddSpaced(stats, StatCard("Total unpaid", Fmt.Money(s.TotalUnpaid), ""));
        if (s.CarriedOverUnpaid > 0)
            AddSpaced(stats, StatCard("Carried over", Fmt.Money(s.CarriedOverUnpaid),
                $"{s.CarriedOverCount} unpaid from earlier month(s)", Res("Bad")));
        if (s.LoanBalanceTotal > 0)
            AddSpaced(stats, StatCard("Loans left", Fmt.Money(s.LoanBalanceTotal),
                s.LoanPaymentsDue > 0 ? $"{Fmt.Money(s.LoanPaymentsDue)} due this month" : "paid this month"));
        AddSpaced(stats, StatCard("Due in 7 days", Fmt.Money(s.DueSoon), "", s.DueSoon > 0 ? Res("Bad") : Res("Text")));
        AddSpaced(stats, StatCard("Bank after due-soon paid", Fmt.Money(s.AfterDueSoonPaid), "", s.AfterDueSoonPaid < 0 ? Res("Bad") : Res("Good")));
        Body.Children.Add(stats);

        // A month with no bills can still have loans, so the empty state can't short-circuit the
        // whole screen — it only stands in for the bill sections.
        if (rows.Count == 0 && carried.Count == 0)
        {
            Body.Children.Add(Card(Empty("No bills for this month yet. Add one — recurring bills automatically show in every month they cover.")));
            BuildLoans();
            return;
        }

        Body.Children.Add(SortBar("bills", "date"));

        // The screen is stacked sections of the same kind of thing, not one table. A card-payment
        // bill is an ordinary bill in every way except which section it lands in, so it is filtered
        // out of Bills and listed under Credit Cards instead — never both.
        var billRows = rows.Where(r => !IsCardPayment(r)).ToList();
        var cardRows = rows.Where(IsCardPayment).ToList();

        var tables = new List<Grid>();

        // Above everything: what's late. One section for all of it, whatever section the bill would
        // sit in this month — an unpaid card payment from two months ago is exactly as late as an
        // unpaid electric bill.
        if (carried.Count > 0)
        {
            // The row carries the month it belongs to on its own status, so the label needs no
            // side table to look it up in.
            var t = BuildTable(carried.Select(c => c.Row).ToList(), manual: false,
                subLabel: r => $"{BudgetService.MonthLabel(r.Status.Month)} · "
                             + Late(BudgetService.MonthsBetween(r.Status.Month, S.Month)),
                alarm: true);
            tables.Add(t);
            AddSection(PackIconRemixIconKind.AlarmWarningFill, "Carried over", t);
        }

        if (billRows.Count > 0)
        {
            var t = BuildTable(billRows, manual);
            tables.Add(t);
            // The bills table only needs naming once there's another section to tell it apart from;
            // on its own it is the screen, and the page header already says so.
            if (carried.Count > 0 || cardRows.Count > 0)
                AddSection(PackIconRemixIconKind.BillFill, "Bills", t);
            else
                Body.Children.Add(Card(t));
        }
        if (cardRows.Count > 0)
        {
            var t = BuildTable(cardRows, manual: false);
            tables.Add(t);
            AddSection(PackIconRemixIconKind.BankCardFill, "Credit Cards", t);
        }

        // Loans are the one section whose columns genuinely differ, so they get their own table and
        // their own saved layout rather than joining the shared one.
        BuildLoans();

        // One saved layout for every section: same columns stacked down the screen have to line up,
        // and dragging a divider in either one moves it in both.
        // No handle at column 2: DUE is left-aligned and AMOUNT right-aligned, so that boundary is a
        // gap between two blocks of content rather than an edge either of them sits against.
        // SaveQuiet, not Save: a width change must not raise DataChanged and rebuild these tables —
        // that is what used to throw away the drag the moment it was dropped.
        // Managed (fixed, saved) columns are 2-4. Column 0 flexes, column 1 is Auto and column 5
        // sizes itself to the buttons, so none of those has a width worth storing.
        ColumnResize.Apply(tables, "bills", S.Settings, S.SaveQuiet, cols: new[] { 2, 3, 4 }, handleCols: new[] { 1, 3, 4 });
    }

    // ── Loans ──
    /// <summary>
    /// This month's slice of every active loan's amortization. Renders nothing at all when there are
    /// no loans — users without loans never see the section, which is why "+ Add loan" lives in the
    /// screen header instead of this section's.
    /// </summary>
    private void BuildLoans()
    {
        var loans = B.ActiveLoans()
            .Select(l => (Loan: l, Row: B.LoanRowFor(l, S.Month)))
            .Where(x => x.Row.Month != null)
            .ToList();
        if (loans.Count == 0) return;

        var table = new Grid();
        foreach (var w in new[]
        {
            Star(1),                 // LOAN — the one column that absorbs spare width
            GridLength.Auto,         // DUE
            new GridLength(130),     // PAYMENT — editable
            new GridLength(110),     // INTEREST
            new GridLength(110),     // PRINCIPAL
            new GridLength(130),     // BALANCE LEFT
            GridLength.Auto,         // actions
        })
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        table.ColumnDefinitions[0].MinWidth = 120;

        AddHeader(table, "LOAN", 0);
        AddHeader(table, "DUE", 1);
        AddHeader(table, "PAYMENT", 2, right: true);
        AddHeader(table, "INTEREST", 3, right: true);
        AddHeader(table, "PRINCIPAL", 4, right: true);
        AddHeader(table, "BALANCE LEFT", 5, right: true);
        AddHeader(table, "", 6);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var hover = new RowHover(table, 7);
        foreach (var (loan, slice) in loans)
        {
            var m = slice.Month!;
            var st = slice.Status;
            int row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var (y, mo) = BudgetService.ParseMonth(S.Month);
            var dueDate = new DateOnly(y, mo, Math.Clamp(loan.DueDay, 1, DateTime.DaysInMonth(y, mo)));
            bool overdue = !m.Paid && dueDate < today;
            double op = m.Paid ? 0.5 : 1.0;

            if (overdue)
            {
                var bg = new Border { Background = new SolidColorBrush(((SolidColorBrush)Res("Bad")).Color) { Opacity = 0.06 } };
                Grid.SetRow(bg, row); Grid.SetColumnSpan(bg, 7); table.Children.Add(bg);
            }
            hover.Add(row);

            bool stuck = BudgetService.NeverAmortizes(m);

            var nameRow = new WrapPanel();
            nameRow.Children.Add(new TextBlock { Text = loan.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            // A rising balance looks like a bug unless the reason is stated on the row itself.
            if (stuck) nameRow.Children.Add(Badge("Payment doesn't cover interest", "Bad", "Bad"));

            var name = new StackPanel { Margin = new Thickness(10, 10, 6, 10), Opacity = op };
            name.Children.Add(nameRow);
            name.Children.Add(new TextBlock
            {
                Text = loan.AprPercent > 0 ? $"{loan.AprPercent:0.##}% APR" : "interest-free",
                Foreground = Res("TextFaint"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0),
            });
            var nameLoan = loan;
            Place(table, RowHover.ClickToEdit(name, () => EditLoan(nameLoan)), row, 0);

            var due = new WrapPanel { Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = op };
            due.Children.Add(new TextBlock { Text = dueDate.ToString("MMM d"), Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
            if (overdue) due.Children.Add(Badge("Past due", "Bad", "Bad"));
            else if (m.Paid) due.Children.Add(Badge("Paid", "Good", "Good"));
            Place(table, due, row, 1);

            // Editable: typing an amount records that this month's payment was that much, which is
            // how an extra, larger or short payment is entered. Everything after it re-derives.
            var pay = new TextBox
            {
                Text = Fmt.Money(m.Payment),
                Style = St("InputNum"),
                MinWidth = 80,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 4, 10, 4),
            };
            var loanRef = loan; var stRef = st; decimal shown = m.Payment;
            void CommitPay()
            {
                decimal v = ParseMoney(pay.Text);
                if (v == shown) { pay.Text = Fmt.Money(shown); return; }   // nothing to write
                RecordLoanPayment(loanRef, stRef, v, dueDate);
            }
            pay.LostFocus += (_, _) => CommitPay();
            pay.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitPay(); Keyboard.ClearFocus(); } };
            Place(table, pay, row, 2);

            Place(table, RightText(Fmt.Money(m.Interest), Res("TextDim"), op), row, 3);
            // Negative principal means the debt grew this month — never show that in a quiet grey.
            Place(table, RightText(Fmt.Money(m.Principal), m.Principal < 0 ? Res("Bad") : Res("TextDim"), op), row, 4);
            Place(table, RightText(Fmt.Money(m.Closing),
                m.Closing <= 0 ? Res("Good") : m.Closing > m.Opening ? Res("Bad") : Res("Text"), op), row, 5);

            var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
            if (!m.Paid)
                acts.Children.Add(Space(Btn("Paid", "BtnSm", (_, _) => RecordLoanPayment(loanRef, stRef, loanRef.MonthlyPayment, dueDate))));
            else
                acts.Children.Add(Space(Btn("Undo", "BtnGhost", (_, _) =>
                {
                    stRef.Status = PayStatus.Unpaid; stRef.AmountPaid = 0; stRef.PaidDate = null; stRef.UserSet = true;
                    Save();
                })));
            acts.Children.Add(Space(IconButton(PackIconRemixIconKind.PriceTag3Fill, "BtnGhost",
                (_, _) => CorrectBalance(loanRef), tooltip: "Correct balance")));
            acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => EditLoan(loanRef), tooltip: "Edit")));
            Place(table, acts, row, 6);
        }

        hover.Attach();
        AddSection(PackIconRemixIconKind.BankFill, "Loans", table);
        ColumnResize.Apply(table, "bills.loans", S.Settings, S.SaveQuiet,
            cols: new[] { 2, 3, 4, 5 }, handleCols: new[] { 1, 3, 4, 5 });
    }

    /// <summary>
    /// Record what was paid on a loan this month. Any amount is allowed — that is how an extra or
    /// short payment is entered — and the schedule re-derives from it.
    /// </summary>
    private void RecordLoanPayment(Loan loan, LoanMonthStatus st, decimal amount, DateOnly dueDate)
    {
        st.AmountPaid = Math.Max(0, amount);
        st.Status = st.AmountPaid <= 0 ? PayStatus.Unpaid : PayStatus.Paid;
        st.PaidDate = st.AmountPaid <= 0 ? null : dueDate;
        st.UserSet = true;
        Save();
    }

    /// <summary>
    /// Re-anchor a loan to the balance the lender actually shows. This is the escape hatch that keeps
    /// the derived-balance model usable: rather than hunting back through months to find where the
    /// app and reality diverged, state today's balance and carry on from there.
    ///
    /// The anchor is the balance at the *start* of a month, which is what the loan stores — so
    /// correcting it is the same operation as setting the loan up, just done later.
    /// </summary>
    private void CorrectBalance(Loan loan)
    {
        var win = Window.GetWindow(this)!;
        var dlg = new EditDialog($"Correct balance — {loan.Name}", win);

        var amount = new MoneyTextBox(B.LoanSchedule(loan, S.Month).FirstOrDefault(x => x.Month == S.Month)?.Opening ?? loan.OpeningBalance);
        var month = new MonthPicker(); month.Set(S.Month);
        dlg.Add("Balance owed", amount, full: false);
        dlg.Add("As of the start of", month, full: false, rightColumn: true);
        var preview = dlg.AddHint("");

        // Say plainly what this month will look like afterwards, so "start of the month" isn't
        // something the user has to work out for themselves.
        void Preview()
        {
            string m = month.Value ?? S.Month;
            decimal bal = amount.Value;
            decimal interest = Math.Round(bal * loan.AprPercent / 100m / 12m, 2, MidpointRounding.AwayFromZero);
            var st = B.Data.LoanStatuses.FirstOrDefault(x => x.LoanId == loan.Id && x.Month == m);
            bool paid = st != null && st.Status is PayStatus.Paid or PayStatus.Partial;
            decimal pay = Math.Max(0, Math.Min(paid ? st!.AmountPaid : loan.MonthlyPayment, bal + interest));
            preview.Text = $"{BudgetService.MonthLabel(m)} starts at {Fmt.Money(bal)} and ends at "
                         + $"{Fmt.Money(bal + interest - pay)} after {Fmt.Money(pay)} paid "
                         + $"({Fmt.Money(interest)} of it interest). Months before this stop affecting "
                         + "the balance; their history is kept.";
        }
        amount.TextChanged += (_, _) => Preview();
        month.Changed += Preview;
        Preview();

        dlg.OnValidate(() => true);
        if (dlg.ShowDialog() == true)
        {
            loan.OpeningBalance = amount.Value;
            loan.OpeningMonth = month.Value ?? S.Month;
            Save();
        }
    }

    private static Border RightText(string text, Brush brush, double opacity) => new()
    {
        Padding = new Thickness(0, 0, 10, 0),
        Opacity = opacity,
        Child = new TextBlock { Text = text, Foreground = brush, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center },
    };

    /// <summary>Add/edit a loan.</summary>
    private void EditLoan(Loan? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var l = existing;
        var dlg = new EditDialog(isNew ? "Add loan" : "Edit loan", win);

        var name = EditDialog.Text(l?.Name ?? "", "e.g. Car loan");
        var opening = new MoneyTextBox(l?.OpeningBalance ?? 0);
        var openMonth = new MonthPicker(); openMonth.Set(l?.OpeningMonth ?? S.Month);
        var apr = EditDialog.Text((l?.AprPercent ?? 0).ToString("0.##"), "e.g. 6.5");
        var payment = new MoneyTextBox(l?.MonthlyPayment ?? 0);
        var dueDay = new DayPicker(l?.DueDay ?? 1);
        var account = EditDialog.Combo(BankNames(), B.FindAccount(l?.AccountId)?.Name ?? "");
        var notes = EditDialog.Notes(l?.Notes ?? "");

        dlg.AddSection("Loan");
        dlg.Add("Name", name);
        dlg.Add("Balance owed", opening, full: false);
        dlg.Add("As of the start of", openMonth, full: false, rightColumn: true);
        dlg.AddHint("Adding a loan you're already part-way through? Just enter what you owe now and "
                  + "leave the month as this one — Dragonfly works forward from there. None of the "
                  + "earlier history has to be entered.");

        dlg.AddSection("Terms");
        dlg.Add("Interest rate (APR %)", apr, full: false);
        dlg.Add("Required monthly payment", payment, full: false, rightColumn: true);
        dlg.Add("Payment Due Date", dueDay, full: false);
        dlg.Add("Pay from", account, full: false, rightColumn: true);
        var termsHint = dlg.AddHint("");

        // The payment entered here is what the loan *requires* each month; what actually went out in
        // a given month is the editable amount on that month's row. Saying so here, next to a live
        // payoff estimate, is what keeps the two from reading as the same number.
        void TermsPreview()
        {
            decimal bal = opening.Value, rate = ParseRate(apr.Text), pay = payment.Value;
            decimal monthlyInterest = Math.Round(bal * rate / 100m / 12m, 2, MidpointRounding.AwayFromZero);
            string tail = " What you actually pay in any month is set on that month's row — type over "
                        + "the payment to record more or less than this.";

            if (bal <= 0 || pay <= 0) { termsHint.Text = "The required payment is what's due each month." + tail; return; }
            if (pay <= monthlyInterest)
            {
                termsHint.Text = $"{Fmt.Money(pay)} doesn't cover the {Fmt.Money(monthlyInterest)} of interest this "
                               + $"balance accrues each month, so it would grow rather than shrink." + tail;
                return;
            }
            var p = BudgetService.CalcPayoff(bal, rate, pay);
            termsHint.Text = p.NeverPaysOff
                ? "This payment never clears the balance." + tail
                : $"Required each month: {Fmt.Money(pay)} clears this balance in {p.Months} month(s), "
                  + $"{Fmt.Money(p.TotalInterest)} of it interest." + tail;
        }
        opening.TextChanged += (_, _) => TermsPreview();
        apr.TextChanged += (_, _) => TermsPreview();
        payment.TextChanged += (_, _) => TermsPreview();
        TermsPreview();

        dlg.AddHint("Every month from the date above is worked out from these terms, so changing them "
                  + "re-derives the balance. If the balance ever drifts from what your lender says, "
                  + "use “Correct balance” on the loan's row.");

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
                B.Data.Loans.RemoveAll(x => x.Id == l!.Id);
                B.Data.LoanStatuses.RemoveAll(x => x.LoanId == l!.Id);
                Save();
            });

        if (dlg.ShowDialog() == true)
        {
            var target = isNew ? new Loan() : B.Data.Loans.First(x => x.Id == l!.Id);
            target.Name = name.Text.Trim();
            target.OpeningBalance = opening.Value;
            target.OpeningMonth = openMonth.Value ?? S.Month;
            target.AprPercent = ParseRate(apr.Text);
            target.MonthlyPayment = payment.Value;
            target.DueDay = dueDay.Day;
            target.AccountId = B.BankAccounts().FirstOrDefault(x => x.Name == (account.SelectedItem as string))?.Id;
            target.Notes = notes.Text.Trim();
            target.Archived = false;   // editing a settled loan brings it back
            if (isNew) { target.SortOrder = B.Data.Loans.Count; B.Data.Loans.Add(target); }
            Save();
        }
    }

    /// <summary>Bank accounts to fund a payment from, blank first so nothing is guessed.</summary>
    private List<string> BankNames()
    {
        var names = new List<string> { "" };
        names.AddRange(B.BankAccounts().Select(x => x.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        return names;
    }

    private static decimal ParseRate(string? v) =>
        decimal.TryParse((v ?? "").Replace("%", "").Trim(), out var d) ? d : 0;

    /// <summary>Add a named section to the body, spaced from whatever came before it.</summary>
    private void AddSection(PackIconRemixIconKind icon, string title, Grid table)
    {
        var panel = new StackPanel { Margin = new Thickness(0, Body.Children.Count > 2 ? 18 : 0, 0, 0) };
        panel.Children.Add(SectionHeader(icon, title));
        panel.Children.Add(Card(table));
        Body.Children.Add(panel);
    }

    /// <summary>"2 months late" — how far back a carried-over bill's own month is.</summary>
    private static string Late(int months) => months == 1 ? "1 month late" : $"{months} months late";

    /// <summary>A card payment sits in its own section; anything else is an ordinary bill.</summary>
    private bool IsCardPayment(BillRow r) =>
        r.Bill.PaysAccountId != null && B.FindAccount(r.Bill.PaysAccountId)?.Type == AccountType.CreditCard;

    /// <summary>
    /// One section's table. Every section has the same columns and the same actions — only the rows
    /// differ — so this is the single place a bill row is built.
    /// </summary>
    /// <param name="subLabel">Optional second line under the bill's name.</param>
    /// <param name="alarm">Style the name and amount as overdue money (bold, red).</param>
    private Grid BuildTable(List<BillRow> rows, bool manual, Func<BillRow, string>? subLabel = null, bool alarm = false)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(7);
        var table = new Grid();
        // Exactly one flexible column, and it is the first one. Every other column is a fixed pixel
        // width, so dragging a divider moves that divider and nothing else, and resizing the window
        // only ever changes BILL. Two flexible columns would mean each drag silently redistributed
        // width to the other one — a column sliding on its own, nowhere near the divider grabbed.
        // The fixed defaults are sized to the content they hold, so none of them carries slack.
        var colWidths = new[]
        {
            Star(1),                 // BILL — free text; the one column that absorbs spare width
            GridLength.Auto,         // DUE — exactly "Jul 26" plus an optional badge, never more
            new GridLength(130),     // AMOUNT — takes the rest of the DUE|AMOUNT region
            new GridLength(145),     // STATUS — a badge
            new GridLength(185),     // ACCOUNT — free text, ellipsized when tight
            GridLength.Auto,         // actions — the buttons' natural size
        };
        foreach (var w in colWidths)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        table.ColumnDefinitions[0].MinWidth = 120;   // the flex column still needs a floor

        AddHeader(table, "BILL", 0);
        AddHeader(table, "DUE", 1);
        AddHeader(table, "AMOUNT", 2, right: true);
        AddHeader(table, "STATUS", 3);
        AddHeader(table, "ACCOUNT", 4);
        AddHeader(table, "", 5);

        var markers = new List<(FrameworkElement, object)>();
        var hover = new RowHover(table, colWidths.Length);
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

            // Full-width highlight so it's obvious which row the buttons on the far right belong to.
            hover.Add(row);

            // name
            var nameRow = new WrapPanel();
            if (manual) nameRow.Children.Add(DragReorder.Handle(r.Bill));
            nameRow.Children.Add(new TextBlock
            {
                Text = r.Bill.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = alarm ? Res("Bad") : Res("Text"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (r.Bill.Recurrence == Recurrence.OneOff) nameRow.Children.Add(Badge("One-off", "TextDim", "TextDim"));
            if (r.Bill.AutoPay) nameRow.Children.Add(AccentBadge("Auto"));
            // A card payment with nothing to compute from renders a bare $0.00 on the 1st, which reads
            // as real data rather than missing information — and a $0 silently drops out of every
            // total. Both gaps say so on the row instead of looking like an answer.
            if (IsCardPayment(r))
            {
                if (r.Amount <= 0) nameRow.Children.Add(Badge("Set payment amount", "Warn", "Warn"));
                if (B.FindAccount(r.Bill.PaysAccountId) is { DueDay: <= 0 })
                    nameRow.Children.Add(Badge("Set due day", "Warn", "Warn"));
            }

            var name = new StackPanel { Margin = new Thickness(10, 10, 6, 10), Opacity = op };
            name.Children.Add(nameRow);
            // Which month this row actually belongs to, for rows that aren't from the viewed one.
            if (subLabel?.Invoke(r) is { Length: > 0 } sub)
                name.Children.Add(new TextBlock { Text = sub, Foreground = Res("Bad"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) });

            var clickRow = r;
            var nameCell = RowHover.ClickToEdit(name, () => EditBillOrCard(clickRow));
            Place(table, nameCell, row, 0);
            markers.Add((nameCell, r.Bill));

            // due
            var due = new WrapPanel { Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = op };
            due.Children.Add(new TextBlock { Text = r.DueDate.ToString("MMM d"), Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
            if (r.Remaining > 0 && r.DueDate < today) due.Children.Add(Badge("Past due", "Bad", "Bad"));
            else if (r.Remaining > 0 && r.DueDate <= soon) due.Children.Add(Badge("Soon", "Warn", "Warn"));
            Place(table, due, row, 1);

            // amount
            // Ellipsis, not clipping: a hard-clipped amount drops its leading digits and reads as a
            // completely different (much smaller) number. Trimming keeps the significant end and
            // shows plainly that the column is too narrow.
            var amtPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0), Opacity = op };
            amtPanel.Children.Add(new TextBlock
            {
                Text = Fmt.Money(r.Amount),
                FontWeight = alarm ? FontWeights.Bold : FontWeights.Normal,
                Foreground = alarm ? Res("Bad") : Res("Text"),
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = Fmt.Money(r.Amount),
            });
            if (r.Status.Status == PayStatus.Partial)
                amtPanel.Children.Add(new TextBlock { Text = $"{Fmt.Money(r.Status.AmountPaid)} paid", Foreground = Res("Warn"), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis });
            Place(table, amtPanel, row, 2);

            // status
            // 10px in from the divider on its left edge, matching the header padding and the other
            // left-aligned cells; 6px clear of the divider on its right.
            var stCell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) };
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
            // A card payment's name, amount and due day are the card's, so editing it has to go to
            // the card — the generic bill dialog would fight AccountDialog over the same fields.
            var billRow = r;
            acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost",
                (_, _) => EditBillOrCard(billRow), tooltip: "Edit")));
            Place(table, acts, row, 5);
        }

        hover.Attach();
        if (manual) DragReorder.AttachTable(table, markers, MoveBill);
        return table;
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
        // A card payment's amount is the card's minimum, recomputed from the balance every time it's
        // shown. Letting this month override it would desync the two with no way to tell which is
        // right, so the override is simply not offered — pay a different amount and it's a partial.
        bool cardPayment = IsCardPayment(r);
        dlg.Add($"Amount paid so far (of {Fmt.Money(r.Amount)})", paid, full: false);
        if (!cardPayment) dlg.Add("This month's amount (override)", over, full: false, rightColumn: true);
        dlg.Add("Paid on", paidOn, full: false);
        dlg.AddHint(cardPayment
            ? "This card payment's amount follows the card's minimum. The paid date defaults to the due date."
            : "Overriding the amount only changes this one month. The paid date defaults to the due date.");
        dlg.OnValidate(() => true);
        if (dlg.ShowDialog() == true)
        {
            var st = r.Status;
            decimal live = B.BillAmount(r.Bill);
            if (!cardPayment)
            {
                decimal ov = over.Value;
                st.AmountOverride = ov == live ? null : ov;
            }
            decimal amount = st.AmountOverride ?? live;
            st.AmountPaid = paid.Value;
            st.Status = st.AmountPaid <= 0 ? PayStatus.Unpaid : st.AmountPaid >= amount ? PayStatus.Paid : PayStatus.Partial;
            var pd = paidOn.SelectedDate.HasValue ? DateOnly.FromDateTime(paidOn.SelectedDate.Value) : (DateOnly?)null;
            st.PaidDate = pd == r.DueDate ? null : pd;   // null = use the due date
            st.UserSet = true;
            Save();
        }
    }

    /// <summary>Edit a bill — or, for a card payment, the card the bill is generated from.</summary>
    private void EditBillOrCard(BillRow r)
    {
        var card = IsCardPayment(r) ? B.FindAccount(r.Bill.PaysAccountId) : null;
        if (card == null) { EditBill(r.Bill); return; }
        AccountDialog.Show(Window.GetWindow(this)!, B, card, Save);
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
        b.Child = new TextBlock
        {
            Text = text, Foreground = Res("TextFaint"), FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
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
