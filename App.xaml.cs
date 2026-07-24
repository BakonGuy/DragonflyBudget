using System.Windows;
using Dragonfly.Services;

namespace Dragonfly;

public partial class App : Application
{
    public static AppState State { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Assemble resources: code-built palette brushes for the saved theme first, then the styles
        // that reference them. The palette must be present before Theme.xaml loads.
        Resources.MergedDictionaries.Add(ThemeManager.BuildPaletteDictionary(State.Settings.Theme));
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Theme.xaml") });

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
