using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using MahApps.Metro.IconPacks;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// One screen that answers a single question: which debt do I put money on first, and what does that
/// get me? Everything here is written for somebody who finds this stuff hard — one number to enter,
/// one debt to focus on, and a reason in plain words for every ranking. The strategy names people
/// argue about online ("avalanche", "snowball") never appear; the choice is offered as what it
/// actually does for you.
/// </summary>
public partial class PlanView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public PlanView()
    {
        InitializeComponent();
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();

    private PlanStrategy Strategy =>
        S.Settings.DebtStrategy == "snowball" ? PlanStrategy.Snowball : PlanStrategy.Avalanche;

    private void Refresh()
    {
        Body.Children.Clear();

        var targets = B.PayoffTargets();
        if (targets.Count == 0)
        {
            SubText.Text = "What to pay first, and what it gets you.";
            Body.Children.Add(Card(Empty(
                "No debts to plan yet. Add a credit card on Accounts, a loan on Bills, or an entry on "
                + "Debts to Pay, and this screen will work out the fastest way to clear them.")));
            return;
        }

        decimal minimums = targets.Sum(t => t.MinimumPayment);
        decimal budget = S.Settings.DebtBudget > 0 ? S.Settings.DebtBudget : minimums;
        decimal owed = targets.Sum(t => t.Balance);
        SubText.Text = $"You owe {Fmt.Money(owed)} across {targets.Count} debt(s). Here's the fastest way out.";

        Warnings(targets);
        Body.Children.Add(BudgetCard(targets, minimums, budget));

        // Only the projection needs a budget. What to pay first, and why, comes out of the debts
        // themselves — so that card renders whether or not the user has told us anything yet.
        var plan = PayoffPlanner.Simulate(targets, budget, Strategy);
        if (plan.Infeasible)
        {
            Body.Children.Add(TroubleCard(plan, budget, minimums));
            Body.Children.Add(StepsCard(plan, projected: false));
            return;
        }

        Body.Children.Add(VerdictCard(targets, plan, budget));
        Body.Children.Add(StepsCard(plan, projected: true));
        Body.Children.Add(WhatIfCard(targets, plan, budget));
    }

    // ── the one number the user has to give us ──
    private Border BudgetCard(List<PayoffTarget> targets, decimal minimums, decimal budget)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.WalletFill, "What you can put toward debt"));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = "Each month I can pay",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });
        var box = new MoneyTextBox(budget) { MinWidth = 130, VerticalAlignment = VerticalAlignment.Center };
        void Commit()
        {
            decimal v = box.Value;
            if (v == S.Settings.DebtBudget) return;
            S.Settings.DebtBudget = v;
            Save();
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); Keyboard.ClearFocus(); } };
        row.Children.Add(box);
        panel.Children.Add(row);

        panel.Children.Add(new TextBlock
        {
            Text = $"Your required payments come to {Fmt.Money(minimums)} a month. Every dollar above that "
                 + "is what actually clears your debt — below it, you're only paying interest.",
            Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
        });

        // The strategy choice, named for what it does rather than what it's called.
        panel.Children.Add(new TextBlock
        {
            Text = "WHICH DEBT TO FOCUS ON", Foreground = Res("Accent"), FontSize = 11,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 8),
        });
        var choice = new StackPanel { Orientation = Orientation.Horizontal };
        choice.Children.Add(StrategyBtn("Save the most money", "avalanche", "The one charging the most interest"));
        choice.Children.Add(StrategyBtn("Get quick wins", "snowball", "The smallest one, so debts disappear sooner"));
        panel.Children.Add(choice);

        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    private FrameworkElement StrategyBtn(string label, string key, string note)
    {
        bool on = S.Settings.DebtStrategy == key;
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Foreground = on ? Res("Text") : Res("TextDim") });
        content.Children.Add(new TextBlock { Text = note, Foreground = Res("TextFaint"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

        var b = new Button
        {
            Content = content,
            Style = St("BtnGhost"),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 8, 0),
            MaxWidth = 280,
            BorderBrush = on ? Res("AccentStrong") : Res("BorderSoft"),
            BorderThickness = new Thickness(on ? 1.5 : 1),
            Background = on ? new SolidColorBrush(((SolidColorBrush)Res("Accent")).Color) { Opacity = 0.10 } : Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        b.Click += (_, _) => { S.Settings.DebtStrategy = key; Save(); };
        return b;
    }

    // ── budget below the minimums: say so plainly, don't invent a plan ──
    private Border TroubleCard(PlanResult plan, decimal budget, decimal minimums)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.ErrorWarningFill, "This budget won't work yet"));

        // Nothing required and nothing offered — there is no plan to make, just a number to enter.
        if (budget <= 0)
        {
            panel.Children.Add(Big("Tell us what you can pay", Res("Warn")));
            panel.Children.Add(new TextBlock
            {
                Text = "None of your debts has a required monthly payment set, so we can't guess a "
                     + "starting point. Put an amount in the box above — even a small one — and this "
                     + "screen will show you exactly where to put it.",
                Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
            });
        }
        else if (plan.Shortfall > 0)
        {
            panel.Children.Add(Big($"You're {Fmt.Money(plan.Shortfall)} short each month", Res("Bad")));
            panel.Children.Add(new TextBlock
            {
                Text = $"Your debts require {Fmt.Money(minimums)} a month between them, and you've put "
                     + $"{Fmt.Money(budget)}. Raise the amount above to see a plan — or, if that money "
                     + "genuinely isn't there, the required payments themselves are what to look at: "
                     + "a lower rate or a longer term on the biggest one is the thing that moves.",
                Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
            });
        }
        else
        {
            panel.Children.Add(Big("At this amount the debt never clears", Res("Bad")));
            panel.Children.Add(new TextBlock
            {
                Text = "The interest is growing as fast as you're paying it off. Even a small increase "
                     + "changes this completely — try raising the amount above.",
                Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
            });
        }
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── the headline ──
    private Border VerdictCard(List<PayoffTarget> targets, PlanResult plan, decimal budget)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.FlagFill, "Where this gets you"));

        string when = BudgetService.MonthLabel(BudgetService.AddMonths(BudgetService.CurrentMonth(), plan.MonthsToDebtFree));
        panel.Children.Add(Big($"Debt-free in {when}", Res("Good")));
        panel.Children.Add(new TextBlock
        {
            Text = $"That's {Years(plan.MonthsToDebtFree)} of paying {Fmt.Money(budget)} a month, with "
                 + $"{Fmt.Money(plan.TotalInterest)} going to interest along the way.",
            Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        });

        // Both strategies, compared honestly, so the choice above isn't blind.
        var other = Strategy == PlanStrategy.Avalanche ? PlanStrategy.Snowball : PlanStrategy.Avalanche;
        var alt = PayoffPlanner.Simulate(targets, budget, other);
        if (!alt.Infeasible)
        {
            decimal diff = alt.TotalInterest - plan.TotalInterest;
            int monthsDiff = alt.MonthsToDebtFree - plan.MonthsToDebtFree;
            string otherName = other == PlanStrategy.Snowball ? "smallest-first" : "highest-rate-first";
            string text = diff > 0.5m || monthsDiff > 0
                ? $"Good choice — going {otherName} instead would cost you {Fmt.Money(diff)} more"
                  + (monthsDiff > 0 ? $" and take {monthsDiff} month(s) longer." : ".")
                : diff < -0.5m
                    ? $"Worth knowing: going {otherName} instead would save you {Fmt.Money(-diff)}"
                      + (monthsDiff < 0 ? $" and finish {-monthsDiff} month(s) sooner." : ".")
                    : $"Either approach works out about the same for you — going {otherName} costs "
                      + "roughly the same. Pick whichever you'll stick with.";
            panel.Children.Add(new TextBlock
            {
                Text = text, Foreground = Res("TextDim"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── the actual instructions ──
    /// <param name="projected">
    /// False when there's no workable budget yet: the order and the required payments still stand,
    /// but there are no payoff dates and nothing spare to direct anywhere.
    /// </param>
    private Border StepsCard(PlanResult plan, bool projected)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.ListOrdered, "What to pay this month"));

        var order = plan.Order.Where(t => t.Balance > 0).ToList();
        for (int i = 0; i < order.Count; i++)
        {
            var t = order[i];
            bool focus = i == 0;
            // Without a budget the simulation never ran, so fall back to what each debt requires.
            decimal pay = projected && plan.FirstMonthPayment.TryGetValue(t.Id, out var p) ? p : t.MinimumPayment;

            var box = new StackPanel();

            var head = new WrapPanel();
            head.Children.Add(new Border
            {
                Background = focus ? Res("AccentStrong") : Res("Bg"),
                CornerRadius = new CornerRadius(11),
                Width = 22, Height = 22,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(), FontSize = 11.5, FontWeight = FontWeights.Bold,
                    Foreground = focus ? Res("Text") : Res("TextDim"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            });
            head.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(t.Name) ? "(unnamed)" : t.Name,
                FontWeight = FontWeights.Bold, FontSize = focus ? 16 : 14.5,
                VerticalAlignment = VerticalAlignment.Center,
            });
            head.Children.Add(Badge(t.Kind, "TextDim", "TextDim"));
            if (focus) head.Children.Add(AccentBadge("Focus on this one"));
            box.Children.Add(head);

            // The instruction, in money and plain words.
            box.Children.Add(new TextBlock
            {
                Text = focus
                    ? projected
                        ? $"Pay {Fmt.Money(pay)} — its required payment plus every spare dollar you have."
                        : pay > 0
                            ? $"Pay {Fmt.Money(pay)} at minimum — and put anything else you can spare here too."
                            : "Put anything you can spare here."
                    : pay > 0
                        ? $"Pay {Fmt.Money(pay)} — just what's required, for now."
                        : "Nothing to pay this month.",
                FontWeight = focus ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = focus ? Res("Good") : Res("Text"),
                Margin = new Thickness(31, 7, 0, 0), TextWrapping = TextWrapping.Wrap,
            });

            box.Children.Add(new TextBlock
            {
                Text = Reason(t, focus, plan),
                Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(31, 4, 0, 0),
            });

            var facts = new TextBlock
            {
                Text = $"{Fmt.Money(t.Balance)} left"
                     + (t.Apr > 0 ? $" · {t.Apr:0.##}% interest" : " · no interest")
                     + (projected && plan.PayoffMonth.TryGetValue(t.Id, out int pm)
                         ? $" · gone by {BudgetService.MonthLabel(BudgetService.AddMonths(BudgetService.CurrentMonth(), pm))}"
                         : "")
                     + $" · tracked on {t.Where}",
                Foreground = Res("TextFaint"), FontSize = 11.5,
                Margin = new Thickness(31, 6, 0, 0), TextWrapping = TextWrapping.Wrap,
            };
            box.Children.Add(facts);

            panel.Children.Add(new Border
            {
                Child = box,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, i == 0 ? 4 : 8, 0, 0),
                CornerRadius = new CornerRadius(6),
                Background = focus ? new SolidColorBrush(((SolidColorBrush)Res("Accent")).Color) { Opacity = 0.08 } : Brushes.Transparent,
                BorderBrush = focus ? Res("AccentStrong") : Res("BorderSoft"),
                BorderThickness = new Thickness(focus ? 1.5 : 1),
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "When the debt at the top is gone, everything you were paying it rolls onto the next "
                 + "one. That's what makes the last few disappear so much faster than the first."
                 + (projected ? "" : " Fill in what you can pay each month above to see when each one clears."),
            Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0),
        });
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    /// <summary>Why this debt sits where it does, in words rather than a rule name.</summary>
    private string Reason(PayoffTarget t, bool focus, PlanResult plan)
    {
        if (t.NoMinimum && !focus)
            return "Nothing is required on this one each month, so it waits its turn.";

        if (focus)
        {
            if (t.Apr <= 0)
                return "Nothing here is charging interest, so it's simply the one to finish off.";
            return Strategy == PlanStrategy.Snowball
                ? $"It's your smallest balance, so it's the one you can clear soonest — {Fmt.Money(t.Balance)} to go."
                : $"At {t.Apr:0.##}% it's costing you about {Fmt.Money(t.MonthlyInterest)} a month in interest, "
                  + "more per dollar owed than anything else you have. Every extra dollar does the most good here.";
        }

        if (t.Apr <= 0)
            return "No interest is building on this, so paying it early wouldn't save you anything.";

        return Strategy == PlanStrategy.Snowball
            ? $"Bigger than the one above ({Fmt.Money(t.Balance)}), so it comes after."
            : $"At {t.Apr:0.##}% it costs less per dollar than the one above, so it waits — "
              + $"about {Fmt.Money(t.MonthlyInterest)} a month while it does.";
    }

    // ── the nudge ──
    private Border WhatIfCard(List<PayoffTarget> targets, PlanResult plan, decimal budget)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.MagicFill, "If you could find a little more"));

        var more = PayoffPlanner.Simulate(targets, budget + 50, Strategy);
        if (more.Infeasible)
            return Card(panel, margin: new Thickness(0, 0, 0, 18));

        int sooner = plan.MonthsToDebtFree - more.MonthsToDebtFree;
        decimal saved = plan.TotalInterest - more.TotalInterest;
        panel.Children.Add(new TextBlock
        {
            Text = sooner > 0 || saved > 0.5m
                ? $"Another $50 a month gets you out {sooner} month(s) sooner and saves {Fmt.Money(saved)} in interest."
                : "You're close enough to the end that a little extra won't change much — you're nearly there.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "It goes on the debt at the top of the list, same as the rest of your payment.",
            Style = St("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        return Card(panel, margin: new Thickness(0, 0, 0, 18));
    }

    // ── the same debt counted twice makes every number here wrong ──
    private void Warnings(List<PayoffTarget> targets)
    {
        var dupes = B.DuplicateTargets(targets);
        if (dupes.Count == 0) return;

        var panel = new StackPanel();
        panel.Children.Add(SectionHeader(PackIconRemixIconKind.ErrorWarningFill, "Check this first"));
        foreach (var (a, b) in dupes)
            panel.Children.Add(new TextBlock
            {
                Text = $"“{a.Name}” is tracked twice — once as a {a.Kind.ToLower()} on {a.Where}, and once "
                     + $"as a {b.Kind.ToLower()} on {b.Where}. The plan below is counting it twice. Archive "
                     + "whichever one you don't use.",
                Foreground = Res("Bad"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
            });
        Body.Children.Add(Card(panel, margin: new Thickness(0, 0, 0, 18)));
    }

    private static TextBlock Big(string text, Brush brush) => new()
    {
        Text = text, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = brush,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
    };

    private static string Years(int months)
    {
        if (months < 12) return $"{months} month(s)";
        int y = months / 12, m = months % 12;
        return m == 0 ? $"{y} year(s)" : $"{y} year(s) and {m} month(s)";
    }
}
