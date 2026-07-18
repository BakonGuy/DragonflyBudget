using System.Windows;
using System.Windows.Controls;
using Dragonfly.Services;

namespace Dragonfly.Views;

public partial class MonthHeader : UserControl
{
    private AppState State => App.State;

    public MonthHeader()
    {
        InitializeComponent();
        Loaded += (_, _) => Sync();
    }

    private void Sync()
    {
        MonthLabel.Text = BudgetService.MonthLabel(State.Month);
        bool isCurrent = State.Month == BudgetService.CurrentMonth();
        TodayBtn.Visibility = isCurrent ? Visibility.Collapsed : Visibility.Visible;
        CurrentChip.Visibility = isCurrent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Prev_Click(object sender, RoutedEventArgs e) { State.ShiftMonth(-1); Sync(); }
    private void Next_Click(object sender, RoutedEventArgs e) { State.ShiftMonth(1); Sync(); }
    private void Today_Click(object sender, RoutedEventArgs e) { State.SetMonth(BudgetService.CurrentMonth()); Sync(); }
}
