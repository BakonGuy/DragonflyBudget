using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Dragonfly.Services;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>Month + Year dropdown pair. Value is "yyyy-MM", or null when Allow-empty and set to "—".</summary>
public class MonthPicker : StackPanel
{
    private readonly ComboBox _month = new();
    private readonly ComboBox _year = new();
    private readonly bool _allowEmpty;
    private const string None = "—";

    public MonthPicker(bool allowEmpty = false)
    {
        Orientation = Orientation.Horizontal;
        _allowEmpty = allowEmpty;
        _month.Style = St("Combo");
        _year.Style = St("Combo");
        _month.Width = 120;
        _year.Width = 90;
        _year.Margin = new Thickness(8, 0, 0, 0);

        if (allowEmpty) _month.Items.Add(None);
        for (int m = 1; m <= 12; m++)
            _month.Items.Add(CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m));

        int thisYear = DateTime.Today.Year;
        if (allowEmpty) _year.Items.Add(None);
        for (int y = thisYear - 3; y <= thisYear + 8; y++)
            _year.Items.Add(y.ToString());

        Children.Add(_month);
        Children.Add(_year);
    }

    /// <summary>Set current value. Pass null/empty for none.</summary>
    public void Set(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            if (_allowEmpty) { _month.SelectedItem = None; _year.SelectedItem = None; }
            return;
        }
        var (y, m) = BudgetService.ParseMonth(key);
        _month.SelectedItem = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
        _year.SelectedItem = y.ToString();
    }

    public string? Value
    {
        get
        {
            if (_month.SelectedItem is not string ms || ms == None) return null;
            if (_year.SelectedItem is not string ys || ys == None) return null;
            int m = Array.IndexOf(
                Enumerable.Range(1, 12).Select(i => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(i)).ToArray(), ms) + 1;
            if (m < 1) return null;
            return BudgetService.MonthKey(int.Parse(ys), m);
        }
    }
}
