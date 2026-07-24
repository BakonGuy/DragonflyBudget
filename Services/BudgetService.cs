using Dragonfly.Models;

namespace Dragonfly.Services;

public record BillRow(Bill Bill, BillMonthStatus Status, decimal Amount, DateOnly DueDate, PayStatus Effective, bool AutoPaid)
{
    public decimal Remaining => Effective switch
    {
        PayStatus.Paid or PayStatus.Skipped => 0,
        PayStatus.Partial => Math.Max(0, Amount - Status.AmountPaid),
        _ => Amount,
    };
}

public record PendingRow(PendingItem Item, PendingMonthStatus Status);

public record MonthSummary(
    decimal BankTotal, decimal Cash, decimal TotalFunds, decimal CreditTotal,
    decimal TotalMonthlyBills, decimal TotalUnpaid, decimal DueSoon, decimal AfterDueSoonPaid,
    decimal PendingIn, decimal PendingOut, decimal AmountBehind, decimal EndOfMonthProjection,
    int UnpaidCount, int DueSoonCount);

public class BudgetService(DataStore store)
{
    public DataStore Store => store;
    public AppData Data => store.Data;

    // ---- month helpers ----
    public static string MonthKey(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";
    public static string MonthKey(int year, int month) => $"{year:D4}-{month:D2}";
    public static (int Year, int Month) ParseMonth(string key)
    {
        var parts = key.Split('-');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }
    public static string AddMonths(string key, int delta)
    {
        var (y, m) = ParseMonth(key);
        var d = new DateOnly(y, m, 1).AddMonths(delta);
        return MonthKey(d);
    }
    /// <summary>Whole months from <paramref name="start"/> to <paramref name="month"/> (can be negative).</summary>
    public static int MonthsBetween(string start, string month)
    {
        var (sy, sm) = ParseMonth(start);
        var (my, mm) = ParseMonth(month);
        return (my - sy) * 12 + (mm - sm);
    }
    public static string MonthLabel(string key)
    {
        var (y, m) = ParseMonth(key);
        return new DateOnly(y, m, 1).ToString("MMMM yyyy");
    }
    public static string CurrentMonth() => MonthKey(DateOnly.FromDateTime(DateTime.Today));

    static bool InRange(string month, string start, string? end) =>
        string.Compare(month, start, StringComparison.Ordinal) >= 0 &&
        (end == null || string.Compare(month, end, StringComparison.Ordinal) <= 0);

    // ---- scheduling ----
    /// <summary>Resolve the effective repeat schedule, falling back to the legacy Recurrence enum.</summary>
    public static Schedule EffectiveSchedule(Schedule? repeat, Recurrence legacy) => repeat ?? Schedule.From(legacy);

    /// <summary>
    /// Does a repeating item anchored at <paramref name="startMonth"/> occur in <paramref name="month"/>?
    /// Honors the interval (every N months / years); one-off items match only their start month.
    /// </summary>
    static bool ScheduleHits(Schedule s, string startMonth, string? endMonth, string month)
    {
        if (s.IsOneOff) return startMonth == month;
        if (!InRange(month, startMonth, endMonth)) return false;
        int step = s.Unit == RepeatUnit.Year ? s.Interval * 12 : s.Interval;
        if (step <= 0) step = 1;
        int diff = MonthsBetween(startMonth, month);
        return diff >= 0 && diff % step == 0;
    }

    // ---- bills ----
    public bool BillAppliesTo(Bill b, string month)
    {
        if (b.Repeat != null) return ScheduleHits(b.Repeat, b.StartMonth, b.EndMonth, month);
        return b.Recurrence == Recurrence.OneOff ? b.StartMonth == month : InRange(month, b.StartMonth, b.EndMonth);
    }

    public BillMonthStatus GetBillStatus(Bill b, string month)
    {
        var s = Data.BillStatuses.FirstOrDefault(x => x.BillId == b.Id && x.Month == month);
        if (s == null)
        {
            s = new BillMonthStatus { BillId = b.Id, Month = month };
            Data.BillStatuses.Add(s);
        }
        return s;
    }

    public List<BillRow> BillsFor(string month)
    {
        var (y, m) = ParseMonth(month);
        int days = DateTime.DaysInMonth(y, m);
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Data.Bills.Where(b => BillAppliesTo(b, month))
            .Select(b =>
            {
                var s = GetBillStatus(b, month);
                var amt = s.AmountOverride ?? b.Amount;
                var day = Math.Clamp(s.DueDayOverride ?? b.DueDay, 1, days);
                var due = new DateOnly(y, m, day);
                var (eff, autoPaid) = EffectiveStatus(b, s, due, today);
                return new BillRow(b, s, amt, due, eff, autoPaid);
            })
            .OrderBy(r => r.DueDate).ThenBy(r => r.Bill.Name)
            .ToList();
    }

    /// <summary>
    /// The status to use for display and totals. When "autopay counts as paid" is on, an autopay
    /// bill the user hasn't touched is treated as Paid once its due date arrives.
    /// Returns the effective status and whether it was auto-marked (vs. the stored status).
    /// </summary>
    public (PayStatus Status, bool AutoPaid) EffectiveStatus(Bill b, BillMonthStatus s, DateOnly dueDate, DateOnly today)
    {
        if (b.AutoPay && Data.Settings.AutopayCountsAsPaid && !s.UserSet)
        {
            if (today >= dueDate) return (PayStatus.Paid, true);
            return (PayStatus.Unpaid, false);
        }
        return (s.Status, false);
    }

    // ---- pending ----
    public bool PendingAppliesTo(PendingItem p, string month)
    {
        var s = EffectiveSchedule(p.Repeat, p.Recurrence);
        if (s.IsOneOff)
        {
            var anchor = p.ExpectedDate.HasValue ? MonthKey(p.ExpectedDate.Value) : p.StartMonth;
            return anchor == month;
        }
        return ScheduleHits(s, p.StartMonth, p.EndMonth, month);
    }

    public PendingMonthStatus GetPendingStatus(PendingItem p, string month)
    {
        var s = Data.PendingStatuses.FirstOrDefault(x => x.ItemId == p.Id && x.Month == month);
        if (s == null)
        {
            s = new PendingMonthStatus { ItemId = p.Id, Month = month };
            Data.PendingStatuses.Add(s);
        }
        return s;
    }

    public List<PendingRow> PendingFor(string month) =>
        Data.Pending.Where(p => PendingAppliesTo(p, month))
            .Select(p => new PendingRow(p, GetPendingStatus(p, month)))
            .OrderBy(r => r.Item.ExpectedDate ?? DateOnly.MaxValue)
            .ToList();

    // ---- suggestions (existing values for editable dropdowns) ----
    public IEnumerable<string> PaymentMethods() =>
        Data.Bills.Select(b => b.PaymentMethod).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();

    public IEnumerable<string> AccountNames() =>
        Data.Banks.Where(a => !a.Archived).Select(a => a.Name)
            .Concat(Data.Bills.Select(b => b.AccountName))
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();

    // ---- accounts ----
    public IEnumerable<BankAccount> ActiveAccounts() => Data.Banks.Where(a => !a.Archived).OrderBy(a => a.SortOrder);
    public IEnumerable<BankAccount> BankAccounts() => ActiveAccounts().Where(a => a.Type != AccountType.CreditCard);
    public IEnumerable<BankAccount> CreditCards() => ActiveAccounts().Where(a => a.Type == AccountType.CreditCard);
    public BankAccount? FindAccount(Guid? id) => id == null ? null : Data.Banks.FirstOrDefault(a => a.Id == id);

    /// <summary>Set an account's balance and record it in the day's history (manual by default).</summary>
    public void SetBalance(BankAccount a, decimal newBalance, BalanceSource source = BalanceSource.Manual)
    {
        a.Balance = newBalance;
        if (source == BalanceSource.Manual) a.LastUserBalanceDate = DateOnly.FromDateTime(DateTime.Today);
        RecordBalance(a.Id, newBalance, DateOnly.FromDateTime(DateTime.Today), source);
    }

    /// <summary>Linked bill payments (date, amount) for an account — effectively-paid bills with a resolved paid date.</summary>
    public IEnumerable<(DateOnly Date, decimal Amount)> LinkedPayments(Guid accountId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var bill in Data.Bills.Where(b => b.AccountId == accountId))
        {
            foreach (var st in Data.BillStatuses.Where(s => s.BillId == bill.Id))
            {
                var (y, m) = ParseMonth(st.Month);
                int day = Math.Clamp(st.DueDayOverride ?? bill.DueDay, 1, DateTime.DaysInMonth(y, m));
                var due = new DateOnly(y, m, day);
                var (eff, _) = EffectiveStatus(bill, st, due, today);
                if (eff is not (PayStatus.Paid or PayStatus.Partial)) continue;
                decimal amt = eff == PayStatus.Partial ? st.AmountPaid : st.AmountOverride ?? bill.Amount;
                yield return (st.PaidDate ?? due, amt);
            }
        }
    }

    /// <summary>
    /// The balance to display: the user's last entered value minus (bank) or plus (card) any linked
    /// bills paid after that entry. Nothing is written — the stored balance stays the user's baseline.
    /// </summary>
    public decimal EffectiveBalance(BankAccount a)
    {
        decimal since = LinkedPayments(a.Id).Where(p => p.Date > a.LastUserBalanceDate).Sum(p => p.Amount);
        // A bill paid on a card increases what's owed; on a bank account it reduces the balance.
        // Cards are still kept separate from bank balances in the summary (own section, not funds).
        return a.Type == AccountType.CreditCard ? a.Balance + since : a.Balance - since;
    }

    public record BalancePoint(DateOnly Date, decimal Balance, bool IsManual);

    /// <summary>
    /// Effective balance over time for a chart: user entries (manual) reset the baseline and each
    /// linked payment steps it; same-day user entries take precedence over payments.
    /// </summary>
    public List<BalancePoint> BalanceSeries(BankAccount a)
    {
        var events = new List<(DateOnly Date, bool Manual, decimal Val)>();
        var manuals = Data.BalanceHistory.Where(e => e.AccountId == a.Id && e.IsManual).ToList();
        if (manuals.Count == 0)
            events.Add((a.LastUserBalanceDate, true, a.Balance));
        else
            events.AddRange(manuals.Select(e => (e.Date, true, e.Balance)));
        events.AddRange(LinkedPayments(a.Id).Select(p => (p.Date, false, p.Amount)));

        // Manual entries first on ties, so a same-day user value wins over that day's payments.
        var ordered = events.OrderBy(e => e.Date).ThenByDescending(e => e.Manual).ToList();

        var points = new List<BalancePoint>();
        decimal running = 0;
        DateOnly baseDate = DateOnly.MinValue;
        bool started = false;
        foreach (var ev in ordered)
        {
            if (ev.Manual)
            {
                running = ev.Val; baseDate = ev.Date; started = true;
                points.Add(new BalancePoint(ev.Date, running, true));
            }
            else
            {
                if (!started) continue;
                if (ev.Date <= baseDate) continue; // user value for that day takes precedence
                running += a.Type == AccountType.CreditCard ? ev.Val : -ev.Val;
                points.Add(new BalancePoint(ev.Date, running, false));
            }
        }
        return points;
    }

    // ---- balance history ----
    /// <summary>
    /// Record an account's balance for a day. Keeps at most one entry per account per day: the
    /// latest write wins, and a manual edit takes precedence over an automatic one on the same day.
    /// </summary>
    public void RecordBalance(Guid accountId, decimal balance, DateOnly date, BalanceSource source)
    {
        bool manual = source == BalanceSource.Manual;
        var existing = Data.BalanceHistory.FirstOrDefault(e => e.AccountId == accountId && e.Date == date);
        if (existing != null)
        {
            // An automatic update never overwrites a manual one recorded the same day.
            if (existing.IsManual && !manual) return;
            existing.Balance = balance;
            existing.IsManual = manual;
            existing.Source = source;
        }
        else
        {
            Data.BalanceHistory.Add(new BalanceEntry
            {
                AccountId = accountId, Date = date, Balance = balance, IsManual = manual, Source = source,
            });
        }
    }

    // ---- budget categories ----
    public decimal BudgetSpent(Guid categoryId, string month) =>
        Data.BudgetSpend.FirstOrDefault(e => e.CategoryId == categoryId && e.Month == month)?.Spent ?? 0m;

    public BudgetSpendEntry GetBudgetSpend(Guid categoryId, string month)
    {
        var e = Data.BudgetSpend.FirstOrDefault(x => x.CategoryId == categoryId && x.Month == month);
        if (e == null)
        {
            e = new BudgetSpendEntry { CategoryId = categoryId, Month = month };
            Data.BudgetSpend.Add(e);
        }
        return e;
    }

    // ---- summary ----
    public MonthSummary Summarize(string month)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soonCutoff = today.AddDays(7);
        var bills = BillsFor(month);
        var pending = PendingFor(month);

        decimal bank = BankAccounts().Sum(EffectiveBalance);
        decimal credit = CreditCards().Sum(EffectiveBalance);
        decimal funds = bank + Data.CashOnHand;
        decimal totalMonthly = bills.Where(b => b.Status.Status != PayStatus.Skipped).Sum(b => b.Amount);
        decimal totalUnpaid = bills.Sum(b => b.Remaining);
        var dueSoonRows = bills.Where(b => b.Remaining > 0 && b.DueDate <= soonCutoff).ToList();
        decimal dueSoon = dueSoonRows.Sum(b => b.Remaining);
        decimal pendingIn = pending.Where(p => !p.Status.Cleared && p.Item.Amount > 0).Sum(p => p.Item.Amount);
        decimal pendingOut = pending.Where(p => !p.Status.Cleared && p.Item.Amount < 0).Sum(p => p.Item.Amount);

        return new MonthSummary(
            BankTotal: bank, Cash: Data.CashOnHand, TotalFunds: funds, CreditTotal: credit,
            TotalMonthlyBills: totalMonthly, TotalUnpaid: totalUnpaid,
            DueSoon: dueSoon, AfterDueSoonPaid: funds - dueSoon,
            PendingIn: pendingIn, PendingOut: pendingOut,
            AmountBehind: funds - totalUnpaid,
            EndOfMonthProjection: funds - totalUnpaid + pendingIn + pendingOut,
            UnpaidCount: bills.Count(b => b.Remaining > 0),
            DueSoonCount: dueSoonRows.Count);
    }

    // ---- repayment math ----
    public record Payoff(int Months, decimal TotalInterest, decimal FinalPayment, bool NeverPaysOff);

    /// <summary>Months to pay off balance at APR with fixed monthly payment.</summary>
    public static Payoff CalcPayoff(decimal balance, decimal aprPercent, decimal payment)
    {
        if (balance <= 0) return new Payoff(0, 0, 0, false);
        decimal r = aprPercent / 100m / 12m;
        decimal interestOnly = balance * r;
        if (payment <= interestOnly && r > 0) return new Payoff(0, 0, 0, true);
        if (payment <= 0) return new Payoff(0, 0, 0, true);

        decimal bal = balance, totalInterest = 0, lastPay = payment;
        int months = 0;
        while (bal > 0 && months < 1200)
        {
            months++;
            decimal interest = Math.Round(bal * r, 2);
            totalInterest += interest;
            bal += interest;
            lastPay = Math.Min(payment, bal);
            bal -= lastPay;
        }
        return new Payoff(months, totalInterest, lastPay, false);
    }

    /// <summary>Required monthly payment to pay off in N months.</summary>
    public static decimal CalcPaymentForMonths(decimal balance, decimal aprPercent, int months)
    {
        if (balance <= 0 || months <= 0) return 0;
        decimal r = aprPercent / 100m / 12m;
        if (r == 0) return Math.Round(balance / months, 2);
        double rd = (double)r;
        double pmt = (double)balance * rd / (1 - Math.Pow(1 + rd, -months));
        return Math.Round((decimal)pmt, 2);
    }
}
