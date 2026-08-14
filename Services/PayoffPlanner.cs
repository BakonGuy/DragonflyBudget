using Dragonfly.Models;

namespace Dragonfly.Services;

public enum PlanStrategy
{
    /// <summary>Highest interest rate first — always the cheapest way out.</summary>
    Avalanche,
    /// <summary>Smallest balance first — clears individual debts soonest.</summary>
    Snowball,
}

/// <summary>
/// One debt, however it's tracked in the app. Credit cards, loans, repayment-calculator entries and
/// plain "things to pay off" all become one of these, so the plan's arithmetic is written once.
/// </summary>
public record PayoffTarget(
    Guid Id, string Kind, string Name, decimal Balance, decimal Apr,
    decimal FixedPayment, decimal MinPercent, decimal MinFloor, string Where)
{
    /// <summary>
    /// The least that must be paid at a given balance. A card's minimum shrinks as the balance does
    /// (it's a percentage), so it is recomputed rather than frozen at today's figure; everything
    /// else has a fixed monthly payment.
    /// </summary>
    public decimal MinimumFor(decimal bal)
    {
        if (bal <= 0) return 0;
        decimal pct = MinPercent > 0 ? bal * MinPercent / 100m : 0;
        decimal min = Math.Max(MinFloor, pct);
        // The percent/floor rule wins where it exists, because it tracks the balance down. Where it
        // doesn't, the fixed payment the user entered is the only figure we have — and treating that
        // as "no minimum" would leave the debt accruing interest while the plan pays it nothing.
        if (min <= 0) min = FixedPayment;
        return min <= 0 ? 0 : Math.Min(bal, min);
    }

    public decimal MinimumPayment => MinimumFor(Balance);

    /// <summary>Interest this debt costs right now, per month, at its current balance.</summary>
    public decimal MonthlyInterest => Math.Round(Balance * Apr / 100m / 12m, 2, MidpointRounding.AwayFromZero);

    /// <summary>True when nothing is required each month, so it only gets paid as the focus debt.</summary>
    public bool NoMinimum => MinimumPayment <= 0;
}

/// <summary>What a simulated plan produced.</summary>
public record PlanResult(
    int MonthsToDebtFree,
    decimal TotalInterest,
    Dictionary<Guid, int> PayoffMonth,        // months from now until each debt hits zero
    Dictionary<Guid, decimal> InterestByTarget,
    Dictionary<Guid, decimal> FirstMonthPayment,
    IReadOnlyList<PayoffTarget> Order,        // the priority order used
    bool Infeasible,
    decimal Shortfall);                       // how far short of the minimums the budget is

/// <summary>
/// Works out what to pay first. Deliberately a plain month-by-month simulation rather than a closed
/// form: the thing that makes both strategies work is that a cleared debt's payment rolls into the
/// next one, and that only falls out of actually walking the months.
/// </summary>
public static class PayoffPlanner
{
    private const int MaxMonths = 600;

    /// <summary>Priority order: which debt gets every spare dollar, and which comes after it.</summary>
    public static List<PayoffTarget> Rank(IEnumerable<PayoffTarget> targets, PlanStrategy strategy) =>
        strategy == PlanStrategy.Snowball
            // Smallest balance first — the fastest way to see one disappear.
            ? targets.OrderBy(t => t.Balance).ThenByDescending(t => t.Apr).ToList()
            // Highest rate first — the least interest paid overall. 0% debts fall to the end on
            // their own, which is exactly where they belong when nothing is accruing on them.
            : targets.OrderByDescending(t => t.Apr).ThenBy(t => t.Balance).ToList();

    public static PlanResult Simulate(IReadOnlyList<PayoffTarget> targets, decimal monthlyBudget, PlanStrategy strategy)
    {
        var order = Rank(targets, strategy);
        var payoffMonth = new Dictionary<Guid, int>();
        var interestBy = targets.ToDictionary(t => t.Id, _ => 0m);
        var firstPayment = targets.ToDictionary(t => t.Id, _ => 0m);
        var bal = targets.ToDictionary(t => t.Id, t => t.Balance);

        decimal minimumsNow = targets.Sum(t => t.MinimumPayment);
        if (monthlyBudget < minimumsNow)
            return new PlanResult(0, 0, payoffMonth, interestBy, firstPayment, order, true, minimumsNow - monthlyBudget);

        decimal totalInterest = 0;
        int month = 0;
        while (bal.Values.Any(v => v > 0.005m) && month < MaxMonths)
        {
            month++;
            decimal budget = monthlyBudget;

            // 1. Interest lands first, on everything still owing.
            foreach (var t in order)
            {
                if (bal[t.Id] <= 0) continue;
                decimal i = Math.Round(bal[t.Id] * t.Apr / 100m / 12m, 2, MidpointRounding.AwayFromZero);
                bal[t.Id] += i;
                interestBy[t.Id] += i;
                totalInterest += i;
            }

            // 2. Everything gets its minimum, so nothing falls behind while another is the focus.
            foreach (var t in order)
            {
                if (bal[t.Id] <= 0) continue;
                decimal pay = Math.Min(t.MinimumFor(bal[t.Id]), budget);
                if (pay <= 0) continue;
                bal[t.Id] -= pay;
                budget -= pay;
                if (month == 1) firstPayment[t.Id] += pay;
            }

            // 3. Everything left goes on the focus debt — and when one clears, the money that was
            //    going to it rolls onto the next. That rollover is the whole point of both
            //    strategies, so it is modelled rather than approximated: the budget is a fixed
            //    total, so a cleared debt's share is simply still here to spend.
            foreach (var t in order)
            {
                if (budget <= 0) break;
                if (bal[t.Id] <= 0) continue;
                decimal pay = Math.Min(bal[t.Id], budget);
                bal[t.Id] -= pay;
                budget -= pay;
                if (month == 1) firstPayment[t.Id] += pay;
                break;   // one focus debt at a time
            }

            foreach (var t in order)
                if (bal[t.Id] <= 0.005m && !payoffMonth.ContainsKey(t.Id))
                    payoffMonth[t.Id] = month;
        }

        // Ran to the cap with money still owed: at this budget it never clears.
        bool stuck = bal.Values.Any(v => v > 0.005m);
        return new PlanResult(month, totalInterest, payoffMonth, interestBy, firstPayment, order, stuck, 0);
    }
}
