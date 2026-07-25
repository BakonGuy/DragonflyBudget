namespace Dragonfly.Models;

public enum Recurrence { OneOff, Monthly }
public enum PayStatus { Unpaid, Partial, Paid, Skipped }
public enum AccountType { Bank, Cash, CreditCard }
public enum AppTheme { Purple, GreyOrange }
public enum BalanceSource { Manual, MarkPaid, Autopay }

/// <summary>
/// How often something repeats. Supersedes the legacy <see cref="Recurrence"/> enum:
/// <c>Unit == null</c> means one-off, otherwise it repeats every <see cref="Interval"/> units.
/// </summary>
public class Schedule
{
    public RepeatUnit Unit { get; set; } = RepeatUnit.Month;
    public int Interval { get; set; } = 1;

    public static Schedule Monthly => new() { Unit = RepeatUnit.Month, Interval = 1 };
    public static Schedule OneOff => new() { Unit = RepeatUnit.None, Interval = 0 };

    /// <summary>Build a schedule from the legacy two-value recurrence.</summary>
    public static Schedule From(Recurrence r) => r == Recurrence.Monthly ? Monthly : OneOff;

    public bool IsOneOff => Unit == RepeatUnit.None;
}

public enum RepeatUnit { None, Month, Year }

public class BankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Balance { get; set; }
    public int SortOrder { get; set; }
    public bool Archived { get; set; }

    // v1.1: account kind + credit-card fields.
    public AccountType Type { get; set; } = AccountType.Bank;
    public decimal CreditLimit { get; set; }         // credit cards only
    public bool ShowInRepayment { get; set; }        // surface this card on the Repayment screen

    // v1.1: <see cref="Balance"/> is the last value the user entered (the baseline). The displayed
    // balance is this minus linked bills paid since this date (see BudgetService.EffectiveBalance).
    public DateOnly LastUserBalanceDate { get; set; }

    // v1.1: credit-card repayment fields (used when ShowInRepayment is on).
    public decimal AprPercent { get; set; }
    public decimal MonthlyPayment { get; set; }
    public int GoalMonths { get; set; } = 12;
}

/// <summary>
/// One recorded balance for an account on a given day. At most one entry per account per day is
/// kept — the latest write wins, and a manual edit takes precedence over an automatic one.
/// </summary>
public class BalanceEntry
{
    public Guid AccountId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Balance { get; set; }
    public bool IsManual { get; set; }
    public BalanceSource Source { get; set; } = BalanceSource.Manual;
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

    // v1.1: richer repeat schedule (every N months / years). When null, falls back to Recurrence.
    public Schedule? Repeat { get; set; }
    public Guid? AccountId { get; set; }            // structured link to a BankAccount/card
    public int SortOrder { get; set; }              // manual ordering
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

    // v1.1: when the bill was actually paid. Null = treat as the due date.
    public DateOnly? PaidDate { get; set; }

    // v1.1: has the user explicitly set this month's status? Distinguishes an untouched Unpaid
    // (which autopay may auto-mark paid) from one the user deliberately left/marked Unpaid.
    public bool UserSet { get; set; }
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

    // v1.1: richer repeat schedule. When null, falls back to Recurrence.
    public Schedule? Repeat { get; set; }
    public int SortOrder { get; set; }
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

/// <summary>A flexible-spend budget (e.g. groceries): a monthly cap the user tracks against.</summary>
public class BudgetCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal MonthlyCap { get; set; }
    public int SortOrder { get; set; }
    public bool Archived { get; set; }
}

/// <summary>Per-month running spend logged against a <see cref="BudgetCategory"/>.</summary>
public class BudgetSpendEntry
{
    public Guid CategoryId { get; set; }
    public string Month { get; set; } = "";        // "yyyy-MM"
    public decimal Spent { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>Per-screen sort preference.</summary>
public class SortPref
{
    public string Key { get; set; } = "manual";    // manual | date | amount | status | name
    public bool Descending { get; set; }
}

/// <summary>User-configurable settings, persisted with the data file.</summary>
public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Purple;

    /// <summary>Autopay bills are treated as paid on/after their due date (user can still override).</summary>
    public bool AutopayCountsAsPaid { get; set; } = true;

    /// <summary>Per-screen sort choices, keyed by screen name ("bills", "pending", ...).</summary>
    public Dictionary<string, SortPref> Sorts { get; set; } = new();

    /// <summary>User-resized table column widths (px), keyed by screen name; aligned to resizable columns.</summary>
    public Dictionary<string, List<double>> ColumnWidths { get; set; } = new();

    // Updater
    public bool CheckForUpdates { get; set; } = true;
    public string? SkippedUpdateVersion { get; set; }

    // Main window bounds, remembered between sessions.
    public double WindowWidth { get; set; } = 1240;
    public double WindowHeight { get; set; } = 820;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }
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

    // v1.1 additions
    public AppSettings Settings { get; set; } = new();
    public List<BalanceEntry> BalanceHistory { get; set; } = [];
    public List<BudgetCategory> BudgetCategories { get; set; } = [];
    public List<BudgetSpendEntry> BudgetSpend { get; set; } = [];
}
