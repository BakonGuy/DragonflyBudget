using Dragonfly.Models;

namespace Dragonfly.Services;

public record BillRow(Bill Bill, BillMonthStatus Status, decimal Amount, DateOnly DueDate)
{
    public decimal Remaining => Status.Status switch
    {
        PayStatus.Paid or PayStatus.Skipped => 0,
        PayStatus.Partial => Math.Max(0, Amount - Status.AmountPaid),
        _ => Amount,
    };
}

public record PendingRow(PendingItem Item, PendingMonthStatus Status);

public record MonthSummary(
    decimal BankTotal, decimal Cash, decimal TotalFunds,
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
    public static string MonthLabel(string key)
    {
        var (y, m) = ParseMonth(key);
        return new DateOnly(y, m, 1).ToString("MMMM yyyy");
    }
    public static string CurrentMonth() => MonthKey(DateOnly.FromDateTime(DateTime.Today));

    static bool InRange(string month, string start, string? end) =>
        string.Compare(month, start, StringComparison.Ordinal) >= 0 &&
        (end == null || string.Compare(month, end, StringComparison.Ordinal) <= 0);

    // ---- bills ----
    public bool BillAppliesTo(Bill b, string month) =>
        b.Recurrence == Recurrence.OneOff ? b.StartMonth == month : InRange(month, b.StartMonth, b.EndMonth);

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
        return Data.Bills.Where(b => BillAppliesTo(b, month))
            .Select(b =>
            {
                var s = GetBillStatus(b, month);
                var amt = s.AmountOverride ?? b.Amount;
                var day = Math.Clamp(s.DueDayOverride ?? b.DueDay, 1, days);
                return new BillRow(b, s, amt, new DateOnly(y, m, day));
            })
            .OrderBy(r => r.DueDate).ThenBy(r => r.Bill.Name)
            .ToList();
    }

    // ---- pending ----
    public bool PendingAppliesTo(PendingItem p, string month)
    {
        if (p.Recurrence == Recurrence.OneOff)
        {
            var anchor = p.ExpectedDate.HasValue ? MonthKey(p.ExpectedDate.Value) : p.StartMonth;
            return anchor == month;
        }
        return InRange(month, p.StartMonth, p.EndMonth);
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

    // ---- summary ----
    public MonthSummary Summarize(string month)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soonCutoff = today.AddDays(7);
        var bills = BillsFor(month);
        var pending = PendingFor(month);

        decimal bank = Data.Banks.Where(b => !b.Archived).Sum(b => b.Balance);
        decimal funds = bank + Data.CashOnHand;
        decimal totalMonthly = bills.Where(b => b.Status.Status != PayStatus.Skipped).Sum(b => b.Amount);
        decimal totalUnpaid = bills.Sum(b => b.Remaining);
        var dueSoonRows = bills.Where(b => b.Remaining > 0 && b.DueDate <= soonCutoff).ToList();
        decimal dueSoon = dueSoonRows.Sum(b => b.Remaining);
        decimal pendingIn = pending.Where(p => !p.Status.Cleared && p.Item.Amount > 0).Sum(p => p.Item.Amount);
        decimal pendingOut = pending.Where(p => !p.Status.Cleared && p.Item.Amount < 0).Sum(p => p.Item.Amount);

        return new MonthSummary(
            BankTotal: bank, Cash: Data.CashOnHand, TotalFunds: funds,
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
