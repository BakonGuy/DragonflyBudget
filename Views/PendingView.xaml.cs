using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dragonfly.Models;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

public partial class PendingView : UserControl
{
    private AppState S => App.State;
    private BudgetService B => App.State.Budget;

    public PendingView()
    {
        InitializeComponent();
        S.MonthChanged += Refresh;
        S.DataChanged += Refresh;
        Loaded += (_, _) => Refresh();
    }

    private void Save() => S.Save();
    private void Add_Click(object sender, RoutedEventArgs e) => EditItem(null);

    private void Refresh()
    {
        Body.Children.Clear();
        var rows = B.PendingFor(S.Month);
        decimal expIn = rows.Where(r => !r.Status.Cleared && r.Item.Amount > 0).Sum(r => r.Item.Amount);
        decimal expOut = rows.Where(r => !r.Status.Cleared && r.Item.Amount < 0).Sum(r => r.Item.Amount);

        var stats = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 20) };
        AddSpaced(stats, StatCard("Expected in", Fmt.MoneySigned(expIn), "", Res("Good")));
        AddSpaced(stats, StatCard("Expected out", Fmt.Money(expOut), "", expOut < 0 ? Res("Bad") : Res("Text")));
        AddSpaced(stats, StatCard("Net pending", Fmt.MoneySigned(expIn + expOut), "", (expIn + expOut) < 0 ? Res("Bad") : Res("Good")));
        Body.Children.Add(stats);

        if (rows.Count == 0)
        {
            Body.Children.Add(Card(Empty("Nothing pending for this month. Add things like an expected bonus, a refund, or a ticket you need to pay.")));
        }
        else
        {
            var table = new Grid();
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(120), GridLength.Auto, GridLength.Auto })
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            AddHeader(table, "ITEM", 0);
            AddHeader(table, "EXPECTED", 1);
            AddHeader(table, "AMOUNT", 2, right: true);
            AddHeader(table, "STATUS", 3);
            AddHeader(table, "", 4);

            foreach (var r in rows)
            {
                int row = table.RowDefinitions.Count;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                double op = r.Status.Cleared ? 0.5 : 1.0;

                var name = new StackPanel { Margin = new Thickness(10, 9, 6, 9), Opacity = op };
                var nameRow = new WrapPanel();
                nameRow.Children.Add(new TextBlock { Text = r.Item.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                if (r.Item.Recurrence == Recurrence.Monthly) nameRow.Children.Add(AccentBadge("Recurring"));
                name.Children.Add(nameRow);
                if (!string.IsNullOrWhiteSpace(r.Item.Notes))
                    name.Children.Add(new TextBlock { Text = r.Item.Notes, Foreground = Res("TextFaint"), FontSize = 12 });
                Place(table, name, row, 0);

                string when = r.Item.ExpectedDate?.ToString("MMM d")
                              ?? (string.IsNullOrWhiteSpace(r.Item.Timeframe) ? "—" : r.Item.Timeframe);
                Place(table, new TextBlock { Text = when, Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0), Opacity = op }, row, 1);

                var amt = new TextBlock { Text = Fmt.MoneySigned(r.Item.Amount), Foreground = r.Item.Amount < 0 ? Res("Bad") : Res("Good"), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Opacity = op };
                Place(table, amt, row, 2);

                var stCell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
                stCell.Children.Add(r.Status.Cleared ? Badge("Cleared", "Good", "Good") : Badge("Expected", "TextDim", "TextDim"));
                Place(table, stCell, row, 3);

                var acts = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 4, 6) };
                acts.Children.Add(Space(Btn(r.Status.Cleared ? "Undo" : "Cleared", r.Status.Cleared ? "BtnGhost" : "BtnSm",
                    (_, _) => { r.Status.Cleared = !r.Status.Cleared; Save(); })));
                acts.Children.Add(Space(Btn("✎", "BtnGhost", (_, _) => EditItem(r.Item))));
                Place(table, acts, row, 4);
            }
            Body.Children.Add(Card(table));
        }

        Body.Children.Add(new TextBlock
        {
            Text = "“Cleared” means the money actually moved — update your bank balance on the Dashboard when it does. Cleared items stay in this month's history.",
            Style = St("Faint"), Margin = new Thickness(2, 12, 0, 0), TextWrapping = TextWrapping.Wrap
        });
    }

    private void EditItem(PendingItem? existing)
    {
        var win = Window.GetWindow(this)!;
        bool isNew = existing == null;
        var p = existing;
        var dlg = new EditDialog(isNew ? "Add pending item" : "Edit pending item", win);

        var name = EditDialog.Text(p?.Name ?? "", "e.g. Bonus check, Speeding ticket");
        var amount = EditDialog.Text((p?.Amount ?? 0).ToString("0.00"));
        var recur = EditDialog.Combo(new[] { "One-off", "Repeats monthly" },
            (p?.Recurrence ?? Recurrence.OneOff) == Recurrence.Monthly ? "Repeats monthly" : "One-off");
        var date = new DatePicker { SelectedDate = p?.ExpectedDate?.ToDateTime(TimeOnly.MinValue), Height = 36, FontSize = 14 };
        var timeframe = EditDialog.Text(p?.Timeframe ?? "", "e.g. next two weeks");
        var start = new MonthPicker(); start.Set(p?.StartMonth ?? S.Month);
        var end = new MonthPicker(allowEmpty: true); end.Set(p?.EndMonth);
        var notes = EditDialog.Notes(p?.Notes ?? "");

        dlg.Add("What is it?", name);
        dlg.Add("Amount (negative = money out)", amount, full: false);
        dlg.Add("Type", recur, full: false, rightColumn: true);
        dlg.Add("Expected date (optional)", date, full: false);
        dlg.Add("Or rough timeframe", timeframe, full: false, rightColumn: true);
        dlg.Add("Recurring starts", start, full: false);
        dlg.Add("Recurring ends (blank = indefinite)", end, full: false, rightColumn: true);
        dlg.Add("Notes", notes);
        dlg.AddHint("Use amount like 1200 for an expected deposit, or -200 for a payment you'll owe.");

        dlg.OnValidate(() =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return false; }
            return true;
        });

        if (!isNew)
            dlg.EnableDelete(() =>
            {
                B.Data.Pending.RemoveAll(x => x.Id == p!.Id);
                B.Data.PendingStatuses.RemoveAll(x => x.ItemId == p!.Id);
                Save();
            });

        if (dlg.ShowDialog() == true)
        {
            var t = isNew ? new PendingItem() : B.Data.Pending.First(x => x.Id == p!.Id);
            t.Name = name.Text.Trim();
            t.Amount = ParseMoney(amount.Text);
            t.Recurrence = recur.SelectedIndex == 1 ? Recurrence.Monthly : Recurrence.OneOff;
            t.ExpectedDate = date.SelectedDate.HasValue ? DateOnly.FromDateTime(date.SelectedDate.Value) : null;
            t.Timeframe = timeframe.Text.Trim();
            t.StartMonth = start.Value ?? S.Month;
            t.EndMonth = t.Recurrence == Recurrence.Monthly ? end.Value : null;
            t.Notes = notes.Text.Trim();
            if (isNew) B.Data.Pending.Add(t);
            Save();
        }
    }

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
