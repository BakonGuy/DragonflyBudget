using System.Windows;
using System.Windows.Controls;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>Add/edit a bank account or credit card.</summary>
public static class AccountDialog
{
    public static bool Show(Window owner, BudgetService b, BankAccount? existing, Action save, AccountType? defaultType = null)
    {
        bool isNew = existing == null;
        var a = existing;
        var dlg = new EditDialog(isNew ? "Add account" : "Edit account", owner);

        var name = EditDialog.Text(a?.Name ?? "", "e.g. Capital One");
        var typeDefault = a?.Type ?? defaultType ?? AccountType.Bank;
        var type = EditDialog.Combo(new[] { "Bank / cash account", "Credit card" },
            typeDefault == AccountType.CreditCard ? "Credit card" : "Bank / cash account");
        var balance = new MoneyTextBox(a?.Balance ?? 0, allowNegative: true);
        var limit = new MoneyTextBox(a?.CreditLimit ?? 0);
        var apr = EditDialog.Text((a?.AprPercent ?? 0).ToString("0.##"), "e.g. 24.99");
        var payment = new MoneyTextBox(a?.MonthlyPayment ?? 0);
        var dueDay = new DayPicker(a?.DueDay ?? 1);
        var minPct = EditDialog.Text((a?.MinPaymentPercent ?? 0).ToString("0.##"), "e.g. 2");
        var minFloor = new MoneyTextBox(a?.MinPaymentFloor ?? 0);
        var showRepay = new CheckBox
        {
            Style = St("Check"),
            Content = "Show this card on the Repayment screen",
            IsChecked = a?.ShowInRepayment ?? true,
        };

        // ── the card's payment, tracked as a bill ──
        var existingBill = a == null ? null : b.Data.Bills.FirstOrDefault(x => x.PaysAccountId == a.Id);
        // On by default. Only a card whose payment bill was deliberately retired starts unticked.
        var trackBill = new CheckBox
        {
            Style = St("Check"),
            Content = "Track this card's payment as a bill",
            IsChecked = existingBill == null || existingBill.EndMonth == null,
        };
        // Blank first, and blank by default: guessing a funding account would quietly attach the
        // payment to whichever bank happened to be first in the list.
        var bankNames = new List<string> { "" };
        bankNames.AddRange(b.BankAccounts().Select(x => x.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        var payFrom = EditDialog.Combo(bankNames, b.FindAccount(existingBill?.AccountId)?.Name ?? "");

        dlg.Add("Name", name);
        dlg.Add("Type", type, full: false);
        dlg.Add("Current balance", balance, full: false, rightColumn: true);

        // Credit-card-only fields; shown/hidden with the type selection.
        var limitField = dlg.AddTracked("Credit limit", limit, full: false);
        var aprField = dlg.AddTracked("Interest rate (APR %)", apr, full: false, rightColumn: true);
        var payField = dlg.AddTracked("Your monthly payment", payment, full: false);
        var showField = dlg.AddTracked("On repayment screen?", showRepay, full: false, rightColumn: true);
        var dueField = dlg.AddTracked("Payment Due Date", dueDay, full: false);
        var minPctField = dlg.AddTracked("Minimum payment %", minPct, full: false, rightColumn: true);
        var minFloorField = dlg.AddTracked("Minimum payment floor", minFloor, full: false);
        var minHint = dlg.AddHint("");
        var trackField = dlg.AddTracked("Payment bill", trackBill, full: false);
        var payFromField = dlg.AddTracked("Pay from", payFrom, full: false, rightColumn: true);
        var billHint = dlg.AddHint(
            "The payment shows up in the Credit Cards section of the Bills screen. Marking it paid "
            + "takes the money out of the account above and takes the same amount off this card.");

        // The minimum is derived, never stored — show what today's balance and terms work out to.
        void SyncMin()
        {
            decimal bal = balance.Value;
            if (bal <= 0) { minHint.Text = "No minimum payment due — this card isn't carrying a balance."; return; }
            decimal pct = ParseApr(minPct.Text);
            decimal min = Math.Round(Math.Min(bal, Math.Max(minFloor.Value, pct > 0 ? bal * pct / 100m : 0)), 2, MidpointRounding.AwayFromZero);
            minHint.Text = min <= 0
                ? "Enter a percentage and/or a floor to work out this card's minimum payment."
                : $"At today's balance that's {Fmt.Money(min)}/mo — the greater of {pct:0.##}% and {Fmt.Money(minFloor.Value)}.";
        }
        balance.TextChanged += (_, _) => SyncMin();
        minPct.TextChanged += (_, _) => SyncMin();
        minFloor.TextChanged += (_, _) => SyncMin();

        void SyncType()
        {
            bool card = type.SelectedIndex == 1;
            var vis = card ? Visibility.Visible : Visibility.Collapsed;
            limitField.Visibility = vis;
            aprField.Visibility = vis;
            payField.Visibility = vis;
            showField.Visibility = vis;
            dueField.Visibility = vis;
            minPctField.Visibility = vis;
            minFloorField.Visibility = vis;
            minHint.Visibility = vis;
            trackField.Visibility = vis;
            payFromField.Visibility = vis;
            billHint.Visibility = vis;
            if (card) { SyncMin(); SyncBill(); }
        }
        // "Pay from" only means anything while the payment is being tracked.
        void SyncBill()
        {
            bool on = trackBill.IsChecked == true;
            payFromField.IsEnabled = on;
            billHint.Opacity = on ? 1 : 0.5;
        }
        trackBill.Checked += (_, _) => SyncBill();
        trackBill.Unchecked += (_, _) => SyncBill();
        type.SelectionChanged += (_, _) => SyncType();
        SyncType();

        dlg.AddHint("A credit card's balance is what you currently owe. Cards are tracked separately from your available bank + cash funds.");

        dlg.OnValidate(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; }
            return true;
        });

        if (!isNew)
            dlg.EnableDelete(() => { a!.Archived = true; save(); });

        if (dlg.ShowDialog() == true)
        {
            var target = isNew ? new BankAccount { SortOrder = b.Data.Banks.Count } : a!;
            bool isCard = type.SelectedIndex == 1;
            target.Name = name.Text.Trim();
            target.Type = isCard ? AccountType.CreditCard : AccountType.Bank;
            target.CreditLimit = isCard ? limit.Value : 0;
            target.AprPercent = isCard ? ParseApr(apr.Text) : 0;
            target.MonthlyPayment = isCard ? payment.Value : 0;
            target.ShowInRepayment = isCard && showRepay.IsChecked == true;
            target.DueDay = isCard ? dueDay.Day : 1;
            target.MinPaymentPercent = isCard ? ParseApr(minPct.Text) : 0;
            target.MinPaymentFloor = isCard ? minFloor.Value : 0;
            // Record the balance through the service so history captures the manual entry.
            if (isNew) b.Data.Banks.Add(target);
            b.SetBalance(target, balance.Value, BalanceSource.Manual);
            SyncPaymentBill(b, target, isCard, trackBill.IsChecked == true, payFrom.SelectedItem as string);
            save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Create, update or retire the bill that represents this card's monthly payment. There is at
    /// most one per card, found by its <see cref="Bill.PaysAccountId"/> — never a second.
    ///
    /// The bill's Amount is deliberately left at 0, which BudgetService reads as "the card's
    /// minimum, computed now". Storing the number instead would leave it wrong from the next charge
    /// onward.
    /// </summary>
    private static void SyncPaymentBill(BudgetService b, BankAccount card, bool isCard, bool track, string? payFromName)
    {
        var bill = b.Data.Bills.FirstOrDefault(x => x.PaysAccountId == card.Id);

        if (!isCard || !track)
        {
            // Nothing is deleted: end the bill last month so every month it was owed keeps its
            // history, and re-ticking the box later simply clears the end date again.
            if (bill != null && bill.EndMonth == null)
                bill.EndMonth = BudgetService.AddMonths(BudgetService.CurrentMonth(), -1);
            return;
        }

        var bank = b.BankAccounts().FirstOrDefault(x => x.Name == payFromName);
        if (bill == null)
        {
            bill = new Bill
            {
                PaysAccountId = card.Id,
                StartMonth = BudgetService.CurrentMonth(),
                Repeat = Schedule.Monthly,
                Recurrence = Recurrence.Monthly,
                SortOrder = b.Data.Bills.Count,
            };
            b.Data.Bills.Add(bill);
        }

        bill.Name = $"{card.Name} payment";
        bill.DueDay = card.DueDay;
        bill.Amount = 0;                 // 0 = use the computed minimum
        bill.AccountId = bank?.Id;
        bill.AccountName = bank?.Name ?? "";
        bill.EndMonth = null;
    }

    private static decimal ParseApr(string? v) =>
        decimal.TryParse((v ?? "").Replace("%", "").Trim(), out var d) ? d : 0;
}
