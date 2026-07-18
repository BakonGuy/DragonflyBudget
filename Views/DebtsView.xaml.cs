using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class DebtsView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public DebtsView()
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
        var debts = B.Data.Debts.Where(d => !d.Archived).ToList();
        var paidOff = B.Data.Debts.Where(d => d.Archived).ToList();

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Total owed", Fmt.Money(debts.Sum(d => d.TotalOwed)), ""));
        AddSpaced(stats, StatCard("Paid so far", Fmt.Money(debts.Sum(d => d.AmountPaid)), "", Res("Good")));
        AddSpaced(stats, StatCard("Left to pay", Fmt.Money(debts.Sum(d => d.AmountLeft)), "", accent: true));
        Body.Children.Add(stats);

        if (debts.Count == 0)
        {
            Body.Children.Add(Card(Empty("No open debts. Add things like money owed to people, medical bills, IOUs…")));
        }
        else
        {
            var table = new Grid();
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(110), new GridLength(120), new GridLength(110), new GridLength(130), GridLength.Auto })
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            AddHeader(table, "DEBT", 0);
            AddHeader(table, "OWED", 1, right: true);
            AddHeader(table, "PAID", 2, right: true);
            AddHeader(table, "LEFT", 3, right: true);
            AddHeader(table, "PROGRESS", 4);
            AddHeader(table, "", 5);

            foreach (var d in debts)
            {
                var debt = d;
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                double pct = debt.TotalOwed > 0 ? Math.Clamp((double)(debt.AmountPaid / debt.TotalOwed) * 100, 0, 100) : 0;

                var name = new StackPanel { Margin = new Thickness(10, 10, 6, 10) };
                name.Children.Add(new TextBlock { Text = debt.Name, FontWeight = FontWeights.SemiBold });
                if (!string.IsNullOrWhiteSpace(debt.Notes))
                    name.Children.Add(new TextBlock { Text = debt.Notes, Foreground = Res("TextFaint"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
                Place(table, name, row, 0);

                Place(table, RightText(Fmt.Money(debt.TotalOwed)), row, 1);

                var paid = new TextBox { Text = debt.AmountPaid.ToString("0.00"), Style = St("InputNum"), MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                paid.LostFocus += (_, _) => { debt.AmountPaid = ParseMoney(paid.Text); Save(); };
                paid.KeyDown += (_, e) => { if (e.Key == Key.Enter) { debt.AmountPaid = ParseMoney(paid.Text); Save(); } };
                Place(table, paid, row, 2);

                var left = RightText(Fmt.Money(debt.AmountLeft));
                ((TextBlock)((Border)left).Child).Foreground = debt.AmountLeft <= 0 ? Res("Good") : Res("Text");
                Place(table, left, row, 3);

                var track = new Border { Background = Res("Bg"), CornerRadius = new CornerRadius(2), Height = 8, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
                var fill = new Border { Background = Res("AccentStrong"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
                track.Child = fill;
                track.Loaded += (_, _) => fill.Width = track.ActualWidth * pct / 100.0;
                track.SizeChanged += (_, _) => fill.Width = track.ActualWidth * pct / 100.0;
                Place(table, track, row, 4);

                var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
                if (debt.AmountLeft <= 0)
                    acts.Children.Add(Space(Btn("Done ✓", "BtnSm", (_, _) => { debt.Archived = true; Save(); })));
                acts.Children.Add(Space(Btn("✎", "BtnGhost", (_, _) => EditDebt(debt))));
                Place(table, acts, row, 5);
            }
            Body.Children.Add(Card(table));
        }

        if (paidOff.Count > 0)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
            panel.Children.Add(new TextBlock { Text = "✅ Paid off", Style = St("H2"), Margin = new Thickness(0, 0, 0, 12) });
            var inner = new StackPanel();
            foreach (var d in paidOff)
            {
                var debt = d;
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new TextBlock { Text = debt.Name, Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center });
                var owed = RightText(Fmt.Money(debt.TotalOwed)); Grid.SetColumn(owed, 1); g.Children.Add(owed);
                var restore = Btn("Restore", "BtnGhost", (_, _) => { debt.Archived = false; Save(); }); Grid.SetColumn(restore, 2); g.Children.Add(restore);
                inner.Children.Add(g);
            }
            panel.Children.Add(Card(inner));
            Body.Children.Add(panel);
        }
    }

    private void EditDebt(Debt? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var d = existing;
        var dlg = new EditDialog(isNew ? "Add debt" : "Edit debt", win);
        var name = EditDialog.Text(d?.Name ?? "", "e.g. Back X-Ray, Nick");
        var owed = EditDialog.Text((d?.TotalOwed ?? 0).ToString("0.00"));
        var paid = EditDialog.Text((d?.AmountPaid ?? 0).ToString("0.00"));
        var notes = EditDialog.Notes(d?.Notes ?? "");
        dlg.Add("What is it?", name);
        dlg.Add("Total owed", owed, full: false);
        dlg.Add("Paid so far", paid, full: false, rightColumn: true);
        dlg.Add("Notes", notes);
        dlg.OnValidate(() => { if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; } return true; });
        if (!isNew)
            dlg.EnableDelete(() => { B.Data.Debts.RemoveAll(x => x.Id == d!.Id); Save(); });

        if (dlg.ShowDialog() == true)
        {
            var t = isNew ? new Debt() : B.Data.Debts.First(x => x.Id == d!.Id);
            t.Name = name.Text.Trim();
            t.TotalOwed = ParseMoney(owed.Text);
            t.AmountPaid = ParseMoney(paid.Text);
            t.Notes = notes.Text.Trim();
            if (isNew) B.Data.Debts.Add(t);
            Save();
        }
    }

    private static Border RightText(string text) => new()
    {
        Padding = new Thickness(0, 0, 10, 0),
        Child = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center },
    };
    private static void AddSpaced(UniformGrid g, FrameworkElement el)
    {
        el.Margin = new Thickness(g.Children.Count == 0 ? 0 : 7, 0, 7, 0);
        g.Children.Add(el);
    }
    private static FrameworkElement Space(Button b) { b.Margin = new Thickness(6, 0, 0, 0); return b; }
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
