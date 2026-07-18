using System.Windows;
using Dragonfly.Services;

namespace Dragonfly;

public partial class App : Application
{
    public static AppState State { get; } = new();
}
