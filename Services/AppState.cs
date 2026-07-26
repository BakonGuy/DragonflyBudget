namespace Dragonfly.Services;

/// <summary>Shared app state: the one BudgetService instance plus the selected month.</summary>
public class AppState
{
    public DataStore Store { get; }
    public BudgetService Budget { get; }
    public string Month { get; set; } = BudgetService.CurrentMonth();

    /// <summary>User settings (theme, autopay behaviour, sort prefs, updater).</summary>
    public Models.AppSettings Settings => Store.Data.Settings;

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

    /// <summary>Persist without raising <see cref="DataChanged"/>. For pure view preferences —
    /// column widths and the like — where the screen already shows the change and a rebuild would
    /// only throw away what the user just did.</summary>
    public void SaveQuiet() => Store.Save();
}
