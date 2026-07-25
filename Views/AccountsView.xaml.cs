using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
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
        bankPanel.Children.Add(SectionHead("🏦 Bank Accounts", Btn("+ Add Bank", "BtnSm", (_, _) => AddAccount(AccountType.Bank))));
        if (banks.Count == 0)
        {
            bankPanel.Children.Add(Empty("No bank accounts yet. Add one to start tracking your balances."));
        }
        else
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AddHeader(table, "BANK", 0);
            AddHeader(table, "BALANCE", 1, right: true);
            AddHeader(table, "", 2);

            foreach (var acc in banks)
            {
                var row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var nameCol = new StackPanel { Margin = new Thickness(10, 10, 6, 10), VerticalAlignment = VerticalAlignment.Center };
                nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(acc.Name) ? "(unnamed)" : acc.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                Place(table, nameCol, row, 0);

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
                acts.Children.Add(Space(Btn("📈", "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, acc).ShowDialog())));
                acts.Children.Add(Space(Btn("✎", "BtnGhost", (_, _) => EditAccount(acc))));
                Place(table, acts, row, 2);
            }
            bankPanel.Children.Add(Card(table));
        }
        Body.Children.Add(bankPanel);

        // ── Credit cards (styled like DebtsView) ──
        if (cards.Count > 0)
        {
            var cardPanel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            cardPanel.Children.Add(SectionHead("💳 Credit Cards", Btn("+ Add Card", "BtnSm", (_, _) => AddAccount(AccountType.CreditCard))));
            var table = new Grid();
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(100), new GridLength(100), new GridLength(100), new GridLength(1, GridUnitType.Star), GridLength.Auto })
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            AddHeader(table, "CARD", 0);
            AddHeader(table, "BALANCE", 1, right: true);
            AddHeader(table, "LIMIT", 2, right: true);
            AddHeader(table, "AVAILABLE", 3, right: true);
            AddHeader(table, "UTILIZATION", 4);
            AddHeader(table, "", 5);

            foreach (var card in cards)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                decimal bal = B.EffectiveBalance(card);
                double pct = card.CreditLimit > 0 ? Math.Clamp((double)bal / (double)card.CreditLimit * 100.0, 0, 100) : 0;

                var nameCol = new StackPanel { Margin = new Thickness(10, 10, 6, 10), VerticalAlignment = VerticalAlignment.Center };
                nameCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(card.Name) ? "(unnamed)" : card.Name, FontWeight = FontWeights.SemiBold });
                Place(table, nameCol, row, 0);

                Place(table, RightText(Fmt.Money(bal), bal > 0 ? Res("Bad") : Res("Text")), row, 1);

                string limitText = card.CreditLimit > 0 ? Fmt.Money(card.CreditLimit) : "—";
                Place(table, RightText(limitText, Res("TextDim")), row, 2);

                decimal avail = card.CreditLimit > 0 ? card.CreditLimit - bal : 0;
                var availColor = avail > 0 ? Res("Good") : Res("Bad");
                Place(table, RightText(Fmt.Money(avail), availColor), row, 3);

                var track = new Border { Background = Res("Bg"), CornerRadius = new CornerRadius(2), Height = 8, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
                var fill = new Border { Background = pct > 80 ? Res("Bad") : pct > 50 ? Res("Warn") : Res("AccentStrong"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
                track.Child = fill;
                var p = pct;
                track.Loaded += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
                track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * p / 100.0;
                Place(table, track, row, 4);

                var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
                acts.Children.Add(Space(Btn("📈", "BtnGhost", (_, _) => new BalanceHistoryWindow(win, B, card).ShowDialog())));
                acts.Children.Add(Space(Btn("✎", "BtnGhost", (_, _) => EditAccount(card))));
                Place(table, acts, row, 5);
            }
            cardPanel.Children.Add(Card(table));
            Body.Children.Add(cardPanel);
        }

        // ── Cash on hand ──
        var cashPanel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        cashPanel.Children.Add(SectionHead("💵 Cash on Hand", null));
        var cashRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cashRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
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
            var toggleBtn = Btn($"🗄  View archived ({archived.Count})", "BtnGhost", (_, _) =>
            {
                _showArchived = !_showArchived;
                Refresh();
            });
            toggleBtn.HorizontalAlignment = HorizontalAlignment.Left;
            toggleBtn.Foreground = Res("TextFaint");
            toggleBtn.FontSize = 12.5;
            archSection.Children.Add(toggleBtn);

            if (_showArchived)
            {
                var inner = new StackPanel();
                foreach (var a in archived)
                {
                    var acc = a;
                    var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
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
                    var restore = Btn("Restore", "BtnGhost", (_, _) => { acc.Archived = false; Save(); });
                    Grid.SetColumn(restore, 2);
                    g.Children.Add(restore);
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
    private static Grid SectionHead(string title, UIElement? action)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = title, Style = St("H2") });
        if (action != null) { Grid.SetColumn(action, 1); g.Children.Add(action); }
        return g;
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
