using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class RepaymentView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public RepaymentView()
    {
        InitializeComponent();
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();
    private void Add_Click(object sender, RoutedEventArgs e) => EditDebt(null);

    private void Refresh()
    {
        Body.Children.Clear();
        var debts = B.Data.InterestDebts.Where(d => !d.Archived).ToList();

        if (debts.Count == 0)
        {
            Body.Children.Add(Card(Empty("Nothing here yet. Add a credit card or car loan to see payoff timelines, total interest, and the payment that hits your goal date.")));
            return;
        }

        var wrap = new WrapPanel();
        foreach (var d in debts)
            wrap.Children.Add(BuildCard(d));
        Body.Children.Add(wrap);
    }

    private Border BuildCard(InterestDebt debt)
    {
        var payoff = BudgetService.CalcPayoff(debt.Balance, debt.AprPercent, debt.MonthlyPayment);
        decimal interestOnly = Math.Round(debt.Balance * debt.AprPercent / 100m / 12m, 2);
        var goalPayment = BudgetService.CalcPaymentForMonths(debt.Balance, debt.AprPercent, debt.GoalMonths);
        var goalPayoff = BudgetService.CalcPayoff(debt.Balance, debt.AprPercent, goalPayment);
        var now = DateTime.Today;

        var panel = new StackPanel();

        // header
        var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = debt.Name, Style = St("H2") });
        var edit = Btn("✎ Edit", "BtnGhost", (_, _) => EditDebt(debt)); Grid.SetColumn(edit, 1); head.Children.Add(edit);
        panel.Children.Add(head);

        // facts
        var facts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        facts.Children.Add(Fact("Balance", Fmt.Money(debt.Balance)));
        facts.Children.Add(Fact("Interest rate", debt.AprPercent.ToString("0.##") + "%"));
        facts.Children.Add(Fact("Interest-only pmt", Fmt.Money(interestOnly)));
        panel.Children.Add(facts);

        // current payment block
        var cur = new StackPanel();
        var curTitle = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        curTitle.Children.Add(new TextBlock { Text = "At your payment of ", Foreground = Res("TextDim"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var payEdit = new TextBox { Text = debt.MonthlyPayment.ToString("0.00"), Style = St("InputNum"), MinWidth = 90, Width = 95, VerticalAlignment = VerticalAlignment.Center };
        payEdit.LostFocus += (_, _) => { debt.MonthlyPayment = ParseMoney(payEdit.Text); Save(); };
        payEdit.KeyDown += (_, e) => { if (e.Key == Key.Enter) { debt.MonthlyPayment = ParseMoney(payEdit.Text); Save(); } };
        curTitle.Children.Add(payEdit);
        curTitle.Children.Add(new TextBlock { Text = " /mo", Foreground = Res("TextDim"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        cur.Children.Add(curTitle);

        if (debt.Balance <= 0)
            cur.Children.Add(new TextBlock { Text = "Paid off 🎉", Foreground = Res("TextDim") });
        else if (payoff.NeverPaysOff)
            cur.Children.Add(new TextBlock { Text = $"⚠ That payment doesn't beat the interest ({Fmt.Money(interestOnly)}/mo) — the balance would never go down.", Foreground = Res("Bad"), TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        else
        {
            cur.Children.Add(KvRow("Payoff time", $"{payoff.Months} months · {now.AddMonths(payoff.Months):MMMM yyyy}", Res("Text"), true));
            cur.Children.Add(KvRow("Total interest paid", Fmt.Money(payoff.TotalInterest), Res("Bad")));
        }
        panel.Children.Add(Block(cur, accent: false));

        // goal block
        var goal = new StackPanel();
        var goalTitle = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        goalTitle.Children.Add(new TextBlock { Text = "Goal: paid off in ", Foreground = Res("TextDim"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var goalEdit = new TextBox { Text = debt.GoalMonths.ToString(), Style = St("InputNum"), MinWidth = 55, Width = 58, VerticalAlignment = VerticalAlignment.Center };
        goalEdit.LostFocus += (_, _) => { if (int.TryParse(goalEdit.Text, out var m)) debt.GoalMonths = Math.Clamp(m, 1, 600); Save(); };
        goalEdit.KeyDown += (_, e) => { if (e.Key == Key.Enter) { if (int.TryParse(goalEdit.Text, out var m)) debt.GoalMonths = Math.Clamp(m, 1, 600); Save(); } };
        goalTitle.Children.Add(goalEdit);
        goalTitle.Children.Add(new TextBlock { Text = $" months ({now.AddMonths(debt.GoalMonths):MMMM yyyy})", Foreground = Res("TextDim"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        goal.Children.Add(goalTitle);

        if (debt.Balance > 0)
        {
            goal.Children.Add(KvRow("Monthly payment needed", Fmt.Money(goalPayment), Res("Accent"), true));
            goal.Children.Add(KvRow("Total interest at goal", Fmt.Money(goalPayoff.TotalInterest), Res("Bad")));
            if (!payoff.NeverPaysOff && payoff.Months > 0 && payoff.TotalInterest > goalPayoff.TotalInterest)
                goal.Children.Add(new TextBlock { Text = $"Hitting the goal saves {Fmt.Money(payoff.TotalInterest - goalPayoff.TotalInterest)} in interest vs. your current payment.", Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        }
        panel.Children.Add(Block(goal, accent: true));

        if (!string.IsNullOrWhiteSpace(debt.Notes))
            panel.Children.Add(new TextBlock { Text = debt.Notes, Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });

        var card = Card(panel);
        card.Width = 420;
        card.Margin = new Thickness(0, 0, 18, 18);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private void EditDebt(InterestDebt? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var d = existing;
        var dlg = new EditDialog(isNew ? "Add interest debt" : "Edit debt", win);
        var name = EditDialog.Text(d?.Name ?? "", "e.g. Capital One");
        var bal = EditDialog.Text((d?.Balance ?? 0).ToString("0.00"));
        var apr = EditDialog.Text((d?.AprPercent ?? 0).ToString("0.##"));
        var pay = EditDialog.Text((d?.MonthlyPayment ?? 0).ToString("0.00"));
        var goal = EditDialog.Text((d?.GoalMonths ?? 12).ToString());
        var notes = EditDialog.Notes(d?.Notes ?? "");
        dlg.Add("Name", name);
        dlg.Add("Current balance", bal, full: false);
        dlg.Add("Interest rate (APR %)", apr, full: false, rightColumn: true);
        dlg.Add("Your monthly payment", pay, full: false);
        dlg.Add("Payoff goal (months)", goal, full: false, rightColumn: true);
        dlg.Add("Notes", notes);
        dlg.OnValidate(() => { if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; } return true; });
        if (!isNew)
            dlg.EnableDelete(() => { B.Data.InterestDebts.RemoveAll(x => x.Id == d!.Id); Save(); });

        if (dlg.ShowDialog() == true)
        {
            var t = isNew ? new InterestDebt() : B.Data.InterestDebts.First(x => x.Id == d!.Id);
            t.Name = name.Text.Trim();
            t.Balance = ParseMoney(bal.Text);
            t.AprPercent = ParseMoney(apr.Text);
            t.MonthlyPayment = ParseMoney(pay.Text);
            t.GoalMonths = int.TryParse(goal.Text, out var m) ? Math.Clamp(m, 1, 600) : 12;
            t.Notes = notes.Text.Trim();
            if (isNew) B.Data.InterestDebts.Add(t);
            Save();
        }
    }

    private static StackPanel Fact(string label, string value)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 26, 0) };
        sp.Children.Add(new TextBlock { Text = label.ToUpper(), Style = St("StatLabel") });
        sp.Children.Add(new TextBlock { Text = value, FontSize = 19, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0) });
        return sp;
    }

    private static Border Block(UIElement child, bool accent) => new()
    {
        Background = Res("Bg"),
        BorderBrush = accent ? Res("AccentSoft") : Res("BorderSoft"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(14, 12, 14, 12),
        Margin = new Thickness(0, 12, 0, 0),
        Child = child,
    };

    private static Grid KvRow(string label, string value, Brush valueBrush, bool bold = false)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = label, Foreground = Res("TextDim"), FontSize = 13.5 });
        var v = new TextBlock { Text = value, Foreground = valueBrush, FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 13.5 };
        Grid.SetColumn(v, 1); g.Children.Add(v);
        return g;
    }

    private static decimal ParseMoney(string? v)
    {
        var t = (v ?? "").Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(t, out var d) ? d : 0;
    }
}
