namespace Dragonfly.Services;

/// <summary>Shared app state: the one BudgetService instance plus the selected month.</summary>
public class AppState
{
    public DataStore Store { get; }
    public BudgetService Budget { get; }
    public string Month { get; set; } = BudgetService.CurrentMonth();

    /// <summary>Raised when the selected month changes.</summary>
    public event Action? MonthChanged;
    /// <summary>Raised when underlying data is saved/changed.</summary>
    public event Action? DataChanged;

    public AppState()
    {
        Store = new DataStore();
        Budget = new BudgetService(Store);
    }

    public void SetMonth(string month)
    {
        Month = month;
        MonthChanged?.Invoke();
    }

    public void ShiftMonth(int delta) => SetMonth(BudgetService.AddMonths(Month, delta));

    public void Save()
    {
        Store.Save();
        DataChanged?.Invoke();
    }
}
