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
        var showRepay = new CheckBox
        {
            Style = St("Check"),
            Content = "Show this card on the Repayment screen",
            IsChecked = a?.ShowInRepayment ?? true,
        };

        dlg.Add("Name", name);
        dlg.Add("Type", type, full: false);
        dlg.Add("Current balance", balance, full: false, rightColumn: true);

        // Credit-card-only fields; shown/hidden with the type selection.
        var limitField = dlg.AddTracked("Credit limit", limit, full: false);
        var aprField = dlg.AddTracked("Interest rate (APR %)", apr, full: false, rightColumn: true);
        var payField = dlg.AddTracked("Your monthly payment", payment, full: false);
        var showField = dlg.AddTracked("On repayment screen?", showRepay, full: false, rightColumn: true);

        void SyncType()
        {
            bool card = type.SelectedIndex == 1;
            var vis = card ? Visibility.Visible : Visibility.Collapsed;
            limitField.Visibility = vis;
            aprField.Visibility = vis;
            payField.Visibility = vis;
            showField.Visibility = vis;
        }
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
            // Record the balance through the service so history captures the manual entry.
            if (isNew) b.Data.Banks.Add(target);
            b.SetBalance(target, balance.Value, BalanceSource.Manual);
            save();
            return true;
        }
        return false;
    }

    private static decimal ParseApr(string? v) =>
        decimal.TryParse((v ?? "").Replace("%", "").Trim(), out var d) ? d : 0;
}
