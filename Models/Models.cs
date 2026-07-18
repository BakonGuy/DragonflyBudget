namespace Dragonfly.Models;

public enum Recurrence { OneOff, Monthly }
public enum PayStatus { Unpaid, Partial, Paid, Skipped }

public class BankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Balance { get; set; }
    public int SortOrder { get; set; }
    public bool Archived { get; set; }
}

public class Bill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int DueDay { get; set; } = 1;           // day of month 1-31 (clamped to month length)
    public Recurrence Recurrence { get; set; } = Recurrence.Monthly;
    public string StartMonth { get; set; } = "";   // "yyyy-MM" first month this bill applies
    public string? EndMonth { get; set; }          // null = indefinite; inclusive
    public bool AutoPay { get; set; }
    public string PaymentMethod { get; set; } = ""; // e.g. PayPal, Direct
    public string AccountName { get; set; } = "";   // which bank/card it pulls from
    public string Notes { get; set; } = "";
}

// Per-month state of a bill. Created lazily; never deleted, so history is kept.
public class BillMonthStatus
{
    public Guid BillId { get; set; }
    public string Month { get; set; } = "";        // "yyyy-MM"
    public PayStatus Status { get; set; } = PayStatus.Unpaid;
    public decimal AmountPaid { get; set; }
    public decimal? AmountOverride { get; set; }   // this month's amount differs
    public int? DueDayOverride { get; set; }
    public string Notes { get; set; } = "";
}

public class PendingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }            // + deposit, - withdrawal
    public Recurrence Recurrence { get; set; } = Recurrence.OneOff;
    public DateOnly? ExpectedDate { get; set; }    // for one-off (or anchor for recurring day)
    public string Timeframe { get; set; } = "";    // free text: "next two weeks", "early June"
    public string StartMonth { get; set; } = "";
    public string? EndMonth { get; set; }
    public string Notes { get; set; } = "";
}

public class PendingMonthStatus
{
    public Guid ItemId { get; set; }
    public string Month { get; set; } = "";
    public bool Cleared { get; set; }              // it happened (money moved)
    public string Notes { get; set; } = "";
}

public class Debt // simple "things to pay off" (no interest)
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal TotalOwed { get; set; }
    public decimal AmountPaid { get; set; }
    public string Notes { get; set; } = "";
    public bool Archived { get; set; }
    public decimal AmountLeft => TotalOwed - AmountPaid;
}

public class InterestDebt // repayment calculator entries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal AprPercent { get; set; }        // e.g. 25.05
    public decimal MonthlyPayment { get; set; }
    public int GoalMonths { get; set; } = 12;
    public string Notes { get; set; } = "";
    public bool Archived { get; set; }
}

public class AppData
{
    public int Version { get; set; } = 1;
    public decimal CashOnHand { get; set; }
    public List<BankAccount> Banks { get; set; } = [];
    public List<Bill> Bills { get; set; } = [];
    public List<BillMonthStatus> BillStatuses { get; set; } = [];
    public List<PendingItem> Pending { get; set; } = [];
    public List<PendingMonthStatus> PendingStatuses { get; set; } = [];
    public List<Debt> Debts { get; set; } = [];
    public List<InterestDebt> InterestDebts { get; set; } = [];
}
