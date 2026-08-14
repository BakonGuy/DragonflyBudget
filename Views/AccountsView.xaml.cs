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

public partial class AccountsView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;
    private bool _showArchived;

    public AccountsView()
    {
        InitializeComponent();
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();

    private void Refresh()
    {
        Body.Children.Clear();
        var s = B.Summarize(S.Month);
        var banks = B.BankAccounts().ToList();
        var cards = B.CreditCards().ToList();
        var archived = B.Data.Banks.Where(a => a.Archived).ToList();
        var win = Window.GetWindow(this)!;

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Bank total", Fmt.Money(s.BankTotal), $"{banks.Count} account(s)", accent: true));
        AddSpaced(stats, StatCard("Cash on hand", Fmt.Money(s.Cash), ""));
        AddSpaced(stats, StatCard("Available funds", Fmt.Money(s.TotalFunds), $"{Fmt.Money(s.BankTotal)} bank + {Fmt.Money(s.Cash)} cash"));
        if (cards.Count > 0)
            AddSpaced(stats, StatCard("Credit owed", Fmt.Money(s.CreditTotal), $"{cards.Count} card(s)", s.CreditTotal > 0 ? Res("Bad") : Res("Text")));
        Body.Children.Add(stats);

        // ── Bank accounts ──
        var bankPanel = new StackPanel();
        bankPanel.Children.Add(SectionHeader(PackIconRemixIconKind.BankFill, "Bank Accounts", Btn("+ Add Bank", "BtnSm", (_, _) => AddAccount(AccountType.Bank))));
        if (banks.Count == 0)
        {
            bankPanel.Children.Add(Empty("No bank accounts yet. Add one to start tracking your balances."));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AddHeader(table, "BANK", 0);
            AddHeader(table, "BALANCE", 1, right: true);
            AddHeader(table, "", 2);

            var hover = new RowHover(table, 3);
            foreach (var acc in banks)
            {
                var row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                hover.Add(row);

                var nameCol = new StackPanel { Margin = new Thickness(10, 10, 6, 10), VerticalAlignment = VerticalAlignment.Center };
                nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(acc.Name) ? "(unnamed)" : acc.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                var clickAcc = acc;
                Place(table, RowHover.ClickToEdit(nameCol, () => EditAccount(clickAcc)), row, 0);

                var bal = new TextBox
                {
                    Text = Fmt.Money(B.EffectiveBalance(acc)),
                    Style = St("InputNum"),
                    MinWidth = 90,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                bal.LostFocus += (_, _) => { var v = ParseMoney(bal.Text); B.SetBalance(acc, v, BalanceSource.Manual); Save(); bal.Text = Fmt.Money(v); };
                bal.KeyDown += (_, e) => { if (e.Key == Key.Enter) { var v = ParseMoney(bal.Text); B.SetBalance(acc, v, BalanceSource.Manual); Save(); bal.Text = Fmt.Money(v); Keyboard.ClearFocus(); } };
                Place(table, bal, row, 1);

                var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
                acts.Children.Add(Space(IconButton(PackIconRemixIconKind.LineChartFill, "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, acc).ShowDialog(), tooltip: "Balance history")));
                acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => EditAccount(acc), tooltip: "Edit")));
                Place(table, acts, row, 2);
            }
            hover.Attach();
            bankPanel.Children.Add(Card(table));
        }
        Body.Children.Add(bankPanel);

        // ── Credit cards (styled like DebtsView) ──
        if (cards.Count > 0)
        {
            var cardPanel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            cardPanel.Children.Add(SectionHeader(PackIconRemixIconKind.BankCardFill, "Credit Cards", Btn("+ Add Card", "BtnSm", (_, _) => AddAccount(AccountType.CreditCard))));
            var table = new Grid();
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), MoneyCol, MoneyCol, MoneyCol, MoneyCol, new GridLength(58), new GridLength(1, GridUnitType.Star), GridLength.Auto })
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            AddHeader(table, "CARD", 0);
            AddHeader(table, "BALANCE", 1, right: true);
            AddHeader(table, "LIMIT", 2, right: true);
            AddHeader(table, "AVAILABLE", 3, right: true);
            AddHeader(table, "MIN PAYMENT", 4, right: true);
            AddHeader(table, "DUE", 5, right: true);
            AddHeader(table, "UTILIZATION", 6);
            AddHeader(table, "", 7);

            var cardHover = new RowHover(table, 8);
            foreach (var card in cards)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cardHover.Add(row);
                decimal bal = B.EffectiveBalance(card);
                double pct = card.CreditLimit > 0 ? Math.Clamp((double)bal / (double)card.CreditLimit * 100.0, 0, 100) : 0;

                var nameCol = new StackPanel { Margin = new Thickness(10, 10, 6, 10), VerticalAlignment = VerticalAlignment.Center };
                nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(card.Name) ? "(unnamed)" : card.Name, FontWeight = FontWeights.SemiBold });
                var clickCard = card;
                Place(table, RowHover.ClickToEdit(nameCol, () => EditAccount(clickCard)), row, 0);

                Place(table, RightText(Fmt.Money(bal), bal > 0 ? Res("Bad") : Res("Text")), row, 1);

                string limitText = card.CreditLimit > 0 ? Fmt.Money(card.CreditLimit) : "—";
                Place(table, RightText(limitText, Res("TextDim")), row, 2);

                decimal avail = card.CreditLimit > 0 ? card.CreditLimit - bal : 0;
                var availColor = avail > 0 ? Res("Good") : Res("Bad");
                Place(table, RightText(Fmt.Money(avail), availColor), row, 3);

                // Derived from the balance and the card's terms — never stored, so it can't go stale.
                decimal min = B.MinimumPayment(card);
                Place(table, RightText(min > 0 ? Fmt.Money(min) : "—", min > 0 ? Res("Text") : Res("TextDim")), row, 4);
                // An unset due day reads as "—", never as the 1st: a real day and a blank one have to
                // look different or an unanswered field passes for an answer.
                bool hasDue = card.DueDay > 0;
                Place(table, RightText(min > 0 && hasDue ? Fmt.Ordinal(card.DueDay) : "—",
                    min > 0 && !hasDue ? Res("Warn") : Res("TextDim")), row, 5);

                var track = new Border { Background = Res("Bg"), CornerRadius = new CornerRadius(2), Height = 8, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
                var fill = new Border { Background = pct > 80 ? Res("Bad") : pct > 50 ? Res("Warn") : Res("AccentStrong"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
                track.Child = fill;
                var p = pct;
                track.Loaded += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
                track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
                Place(table, track, row, 6);

                var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
                acts.Children.Add(Space(IconButton(PackIconRemixIconKind.LineChartFill, "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, card).ShowDialog(), tooltip: "Balance history")));
                acts.Children.Add(Space(IconButton(PackIconRemixIconKind.EditFill, "BtnGhost", (_, _) => EditAccount(card), tooltip: "Edit")));
                Place(table, acts, row, 7);
            }
            cardHover.Attach();
            cardPanel.Children.Add(Card(table));
            Body.Children.Add(cardPanel);
        }

        // ── Cash on hand ──
        var cashPanel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        cashPanel.Children.Add(SectionHeader(PackIconRemixIconKind.CoinsFill, "Cash on Hand"));
        var cashRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
        cashRow.Children.Add(new TextBlock { Text = "Cash on hand", Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
        var cashEdit = new TextBox
        {
            Text = Fmt.Money(B.Data.CashOnHand),
            Style = St("InputNum"),
            MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        cashEdit.LostFocus += (_, _) => { var v = ParseMoney(cashEdit.Text); B.Data.CashOnHand = v; Save(); cashEdit.Text = Fmt.Money(v); };
        cashEdit.KeyDown += (_, e) => { if (e.Key == Key.Enter) { var v = ParseMoney(cashEdit.Text); B.Data.CashOnHand = v; Save(); cashEdit.Text = Fmt.Money(v); Keyboard.ClearFocus(); } };
        Grid.SetColumn(cashEdit, 1);
        cashRow.Children.Add(cashEdit);
        cashPanel.Children.Add(Card(cashRow));
        Body.Children.Add(cashPanel);

        // ── Archived (subtle expandable section) ──
        if (archived.Count > 0)
        {
            var archSection = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
            var toggleContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            toggleContent.Children.Add(Icon(PackIconRemixIconKind.ArchiveFill, 14));
            toggleContent.Children.Add(new TextBlock { Text = $"View archived ({archived.Count})", Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var toggleBtn = new Button { Content = toggleContent, Style = St("BtnGhost"), HorizontalAlignment = HorizontalAlignment.Left, Foreground = Res("TextFaint"), FontSize = 12.5 };
            toggleBtn.Click += (_, _) => { _showArchived = !_showArchived; Refresh(); };
            archSection.Children.Add(toggleBtn);

            if (_showArchived)
            {
                var inner = new StackPanel();
                foreach (var a in archived)
                {
                    var acc = a;
                    var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = MoneyCol });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(acc.Name) ? "(unnamed)" : acc.Name,
                        Foreground = Res("TextDim"),
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                    var bal = new TextBlock
                    {
                        Text = Fmt.Money(acc.Balance),
                        Foreground = Res("TextFaint"),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(bal, 1);
                    g.Children.Add(bal);

                    var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    acts.Children.Add(Btn("Restore", "BtnGhost", (_, _) => { acc.Archived = false; Save(); }));
                    var del = Btn("Delete", "BtnGhost", (_, _) => DeleteArchived(win, acc));
                    del.Foreground = Res("Bad");
                    del.Margin = new Thickness(4, 0, 0, 0);
                    acts.Children.Add(del);
                    Grid.SetColumn(acts, 2);
                    g.Children.Add(acts);
                    inner.Children.Add(g);
                }
                var card = Card(inner);
                card.Margin = new Thickness(0, 10, 0, 0);
                archSection.Children.Add(card);
            }
            Body.Children.Add(archSection);
        }
    }

    private void AddAccount(AccountType type)
    {
        var win = Window.GetWindow(this)!;
        AccountDialog.Show(win, B, null, Save, type);
    }

    private void EditAccount(BankAccount? existing)
    {
        var win = Window.GetWindow(this)!;
        AccountDialog.Show(win, B, existing, Save);
    }

    /// <summary>Permanently remove an archived account, its balance history, and any bill links.</summary>
    private void DeleteArchived(Window win, BankAccount acc)
    {
        string name = string.IsNullOrWhiteSpace(acc.Name) ? "this account" : $"“{acc.Name}”";
        int points = B.Data.BalanceHistory.Count(e => e.AccountId == acc.Id);
        // The card's own payment bill is part of the card, not a bill the user wrote — it goes with
        // it. Anything else merely references the account and is kept, just unlinked.
        var paymentBills = B.Data.Bills.Where(b => b.PaysAccountId == acc.Id).ToList();
        int linkedBills = B.Data.Bills.Count(b => b.AccountId == acc.Id && b.PaysAccountId != acc.Id);

        var parts = new List<string> { "the account" };
        if (points > 0) parts.Add($"{points} recorded balance point{(points == 1 ? "" : "s")}");
        if (paymentBills.Count > 0) parts.Add("its payment bill");
        string removed = string.Join(", ", parts);
        string billNote = linkedBills > 0
            ? $" {linkedBills} other bill{(linkedBills == 1 ? "" : "s")} linked to it will be unlinked (those bills stay)."
            : "";

        if (!ConfirmDialog.Ask(win, "Delete account permanently?",
                $"This removes {removed} for {name}. Its tracked balance history will be lost and this can't be undone.{billNote}",
                confirmText: "Delete permanently", danger: true))
            return;

        // The payment bill is deleted outright: unlinking it instead would leave a $0 bill named
        // after a card that no longer exists, billing the user forever with nothing behind it.
        foreach (var pb in paymentBills)
        {
            B.Data.BillStatuses.RemoveAll(s => s.BillId == pb.Id);
            B.Data.Bills.Remove(pb);
        }
        // Anything still pointing at the account is only funded by it, so it survives, unlinked.
        foreach (var b in B.Data.Bills.Where(b => b.AccountId == acc.Id))
            b.AccountId = null;
        B.Data.BalanceHistory.RemoveAll(e => e.AccountId == acc.Id);
        B.Data.Banks.RemoveAll(a => a.Id == acc.Id);
        Save();
    }

    private static Border RightText(string text, Brush? brush = null) => new()
    {
        Padding = new Thickness(0, 0, 10, 0),
        Child = new TextBlock { Text = text, Foreground = brush ?? Res("Text"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center },
    };
    private static FrameworkElement Space(Button b) { b.Margin = new Thickness(4, 0, 0, 0); return b; }
    private static void AddSpaced(UniformGrid g, FrameworkElement el)
    {
        el.Margin = new Thickness(g.Children.Count == 0 ? 0 : 7, 0, 7, 0);
        g.Children.Add(el);
    }
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
