using Dragonfly.Models;

namespace Dragonfly.Services;

/// <summary>
/// Upgrades a v1 (Dragonfly 1.0) data file to the v2 (1.1) schema. All v1.1 model additions are
/// additive with safe defaults, so this migration mostly backfills the new structured fields from
/// existing data and guarantees the new collections/settings exist.
/// </summary>
public static class MigrationV1ToV2
{
    public static void Apply(AppData data)
    {
        // Ensure new containers exist even on very old files.
        data.Settings ??= new AppSettings();
        data.BalanceHistory ??= [];
        data.BudgetCategories ??= [];
        data.BudgetSpend ??= [];

        // Backfill the richer repeat schedule from the legacy Recurrence enum so nothing changes
        // behaviourally, but the structured field is populated going forward.
        foreach (var b in data.Bills)
            b.Repeat ??= Schedule.From(b.Recurrence);

        foreach (var p in data.Pending)
            p.Repeat ??= Schedule.From(p.Recurrence);

        // Give existing accounts an explicit kind, a stable sort order, and a balance baseline date
        // of today so pre-existing paid bills don't retroactively deduct from the shown balance.
        var today = DateOnly.FromDateTime(DateTime.Today);
        int order = 0;
        foreach (var acc in data.Banks)
        {
            if (acc.Type == default) acc.Type = AccountType.Bank;
            if (acc.SortOrder == 0) acc.SortOrder = order;
            if (acc.LastUserBalanceDate == default) acc.LastUserBalanceDate = today;
            order++;
        }

        // Seed a stable manual order for bills based on their current natural order.
        order = 0;
        foreach (var bill in data.Bills)
            if (bill.SortOrder == 0) bill.SortOrder = order++;

        order = 0;
        foreach (var pend in data.Pending)
            if (pend.SortOrder == 0) pend.SortOrder = order++;

        // Mark month statuses that were clearly acted on, so the new autopay-as-paid behaviour
        // doesn't override a status the user deliberately created. Untouched Unpaid stays UserSet=false.
        foreach (var st in data.BillStatuses)
        {
            if (!st.UserSet)
                st.UserSet = st.Status != PayStatus.Unpaid
                    || st.AmountPaid != 0
                    || st.AmountOverride != null
                    || st.DueDayOverride != null
                    || !string.IsNullOrEmpty(st.Notes);
        }
    }
}
