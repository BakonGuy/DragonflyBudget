using System.IO;
using System.Reflection;

namespace Dragonfly.Services;

public static class AppInfo
{
    private static readonly string DefaultInstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OvertorqueCreations", "Dragonfly");

    public static string VersionString => ForceUpdate ? "0.0.0" : _versionString;
    public static SemVer Version => ForceUpdate ? new SemVer(0, 0, 0) : _version;

    private static readonly string _versionString;
    private static readonly SemVer _version;

    public static string InstallDir => DefaultInstallDir;
    public static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsInstalled =>
        !string.IsNullOrEmpty(ExePath) &&
        ExePath.StartsWith(InstallDir, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// When true, the updater pretends a newer version exists. Set via the <c>--force-update</c>
    /// launch argument so you can test the banner, dialog, and download flow without cutting a release.
    /// </summary>
    public static bool ForceUpdate { get; set; }

    static AppInfo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
            _versionString = info.Split('+')[0];
        else
            _versionString = asm.GetName().Version?.ToString() ?? "0.0.0";

        _version = SemVer.Parse(_versionString) ?? new SemVer(0, 0, 0);
    }
}
