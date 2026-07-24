using System.Windows;
using System.Windows.Controls;
using Dragonfly.Models;
using static Dragonfly.Views.UiKit;

namespace Dragonfly.Views;

/// <summary>
/// Editor for a repeat <see cref="Schedule"/>: one-off, monthly, every N months, yearly, or every N
/// years. Reads back as a <see cref="Schedule"/> to store on a bill or pending item.
/// </summary>
public class RecurrenceEditor : StackPanel
{
    private const int KindOneOff = 0, KindMonthly = 1, KindEveryMonths = 2, KindYearly = 3, KindEveryYears = 4;

    private readonly ComboBox _kind = new() { Style = St("Combo"), Width = 210 };
    private readonly TextBox _interval = new() { Style = St("Input"), Width = 60, MaxLength = 3, Visibility = Visibility.Collapsed };
    private readonly TextBlock _unit = new() { Foreground = Res("TextDim"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };

    public RecurrenceEditor(Schedule? schedule, string oneOffLabel = "One-off")
    {
        Orientation = Orientation.Horizontal;

        _kind.Items.Add(oneOffLabel);
        _kind.Items.Add("Monthly");
        _kind.Items.Add("Every N months");
        _kind.Items.Add("Yearly");
        _kind.Items.Add("Every N years");
        _interval.Margin = new Thickness(8, 0, 0, 0);
        _interval.TextAlignment = TextAlignment.Center;
        _interval.PreviewTextInput += (_, e) => e.Handled = !int.TryParse(e.Text, out int _);

        Children.Add(_kind);
        Children.Add(_interval);
        Children.Add(_unit);

        _kind.SelectionChanged += (_, _) => SyncInterval();
        SetFrom(schedule);
    }

    private void SetFrom(Schedule? s)
    {
        s ??= Schedule.Monthly;
        if (s.IsOneOff) { _kind.SelectedIndex = KindOneOff; _interval.Text = "2"; }
        else if (s.Unit == RepeatUnit.Year)
        {
            _kind.SelectedIndex = s.Interval <= 1 ? KindYearly : KindEveryYears;
            _interval.Text = Math.Max(2, s.Interval).ToString();
        }
        else
        {
            _kind.SelectedIndex = s.Interval <= 1 ? KindMonthly : KindEveryMonths;
            _interval.Text = Math.Max(2, s.Interval).ToString();
        }
        SyncInterval();
    }

    private void SyncInterval()
    {
        bool everyMonths = _kind.SelectedIndex == KindEveryMonths;
        bool everyYears = _kind.SelectedIndex == KindEveryYears;
        bool showInterval = everyMonths || everyYears;
        _interval.Visibility = showInterval ? Visibility.Visible : Visibility.Collapsed;
        _unit.Visibility = showInterval ? Visibility.Visible : Visibility.Collapsed;
        _unit.Text = everyYears ? "years" : "months";
    }

    /// <summary>The chosen schedule.</summary>
    public Schedule Value
    {
        get
        {
            int n = int.TryParse(_interval.Text, out var v) ? Math.Max(2, v) : 2;
            return _kind.SelectedIndex switch
            {
                KindOneOff => Schedule.OneOff,
                KindMonthly => new Schedule { Unit = RepeatUnit.Month, Interval = 1 },
                KindEveryMonths => new Schedule { Unit = RepeatUnit.Month, Interval = n },
                KindYearly => new Schedule { Unit = RepeatUnit.Year, Interval = 1 },
                KindEveryYears => new Schedule { Unit = RepeatUnit.Year, Interval = n },
                _ => Schedule.Monthly,
            };
        }
    }

    public bool IsOneOff => _kind.SelectedIndex == KindOneOff;
}
