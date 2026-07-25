using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace Dragonfly.Services;

public enum UpdateOutcome { UpToDate, Available, Throttled, NoAsset, Failed }

public class UpdateInfo
{
    public SemVer Version { get; }
    public string Tag { get; }
    public string Title { get; }
    public string Notes { get; }
    public string DownloadUrl { get; }
    public string AssetName { get; }
    public long AssetSize { get; }
    public string HtmlUrl { get; }
    public string? ChecksumUrl { get; }

    public UpdateInfo(SemVer version, string tag, string title, string notes,
        string downloadUrl, string assetName, long assetSize,
        string htmlUrl, string? checksumUrl)
    {
        Version = version;
        Tag = tag;
        Title = title;
        Notes = notes;
        DownloadUrl = downloadUrl;
        AssetName = assetName;
        AssetSize = assetSize;
        HtmlUrl = htmlUrl;
        ChecksumUrl = checksumUrl;
    }
}

public class UpdateCheckResult
{
    public UpdateOutcome Outcome { get; }
    public UpdateInfo? Info { get; }
    public TimeSpan? RetryAfter { get; }
    public string? Error { get; }

    public UpdateCheckResult(UpdateOutcome outcome, UpdateInfo? info,
        TimeSpan? retryAfter, string? error)
    {
        Outcome = outcome;
        Info = info;
        RetryAfter = retryAfter;
        Error = error;
    }
}

public class UpdateService
{
    private static readonly TimeSpan AutoInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan ManualCooldown = TimeSpan.FromMinutes(1);
    private static readonly HttpClient Client;
    private static readonly string[] DownloadHosts = ["github.com", "objects.githubusercontent.com"];

    private DateTime _lastManualUtc = DateTime.MinValue;

    static UpdateService()
    {
        Client = new HttpClient();
        Client.DefaultRequestHeaders.Add("User-Agent", "Dragonfly-Updater");
        Client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        Client.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken ct = default)
    {
        try
        {
            var settings = App.State.Settings;

            if (!manual)
            {
                if (!AppInfo.ForceUpdate && !AppInfo.IsInstalled)
                    return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);

                if (!settings.CheckForUpdates)
                    return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);

                if (!AppInfo.ForceUpdate)
                {
                    var sinceLast = settings.LastUpdateCheckUtc.HasValue
                        ? DateTime.UtcNow - settings.LastUpdateCheckUtc.Value
                        : AutoInterval;
                    if (sinceLast < AutoInterval)
                        return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);
                }
            }
            else
            {
                var elapsed = DateTime.UtcNow - _lastManualUtc;
                if (elapsed < ManualCooldown)
                    return new UpdateCheckResult(UpdateOutcome.Throttled, null, ManualCooldown - elapsed, null);
            }

            var result = await FetchLatestReleaseAsync(ct);

            if (result.Outcome != UpdateOutcome.Failed)
            {
                settings.LastUpdateCheckUtc = DateTime.UtcNow;
                if (manual) _lastManualUtc = DateTime.UtcNow;
                App.State.Save();
            }

            if (!manual && result.Info != null && settings.SkippedUpdateVersion == result.Info.Tag)
            {
                return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);
            }

            return result;
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateOutcome.Failed, null, null, "Request timed out.");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateOutcome.Failed, null, null, ex.Message);
        }
    }

    private async Task<UpdateCheckResult> FetchLatestReleaseAsync(CancellationToken ct)
    {
        const string url = "https://api.github.com/repos/BakonGuy/DragonflyBudget/releases/latest";
        using var resp = await Client.GetAsync(url, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);

        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return new UpdateCheckResult(UpdateOutcome.Failed, null, null, "Rate limited by GitHub.");

        if (!resp.IsSuccessStatusCode)
            return new UpdateCheckResult(UpdateOutcome.Failed, null, null, $"HTTP {(int)resp.StatusCode}");

        var doc = await resp.Content.ReadFromJsonAsync<GitHubRelease>(JsonOpts, ct);
        if (doc == null)
            return new UpdateCheckResult(UpdateOutcome.Failed, null, null, "Empty response.");

        var releaseVersion = SemVer.Parse(doc.TagName);
        if (releaseVersion == null)
            return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);

        if (!releaseVersion.IsNewerThan(AppInfo.Version))
            return new UpdateCheckResult(UpdateOutcome.UpToDate, null, null, null);

        var asset = PickInstallerAsset(doc.Assets);
        if (asset == null)
        {
            var partial = new UpdateInfo(releaseVersion, doc.TagName, doc.Name ?? doc.TagName,
                doc.Body ?? "", "", "", 0, doc.HtmlUrl, null);
            return new UpdateCheckResult(UpdateOutcome.NoAsset, partial, null, null);
        }

        string? checksumUrl = null;
        foreach (var a in doc.Assets)
        {
            var n = (a.Name ?? "").ToLowerInvariant();
            if (n.EndsWith(".sha256") || n == "sha256sums.txt")
            {
                checksumUrl = a.BrowserDownloadUrl;
                break;
            }
        }

        var info = new UpdateInfo(releaseVersion, doc.TagName, doc.Name ?? doc.TagName,
            doc.Body ?? "", asset.BrowserDownloadUrl, asset.Name, asset.Size,
            doc.HtmlUrl, checksumUrl);

        return new UpdateCheckResult(UpdateOutcome.Available, info, null, null);
    }

    private static GitHubAsset? PickInstallerAsset(List<GitHubAsset> assets)
    {
        GitHubAsset? msi = null, exe = null;
        foreach (var a in assets)
        {
            var n = (a.Name ?? "").ToLowerInvariant();
            if (n.EndsWith(".msi")) msi ??= a;
            else if (n.EndsWith(".exe")) exe ??= a;
        }
        return msi ?? exe;
    }

    public async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        var uri = new Uri(info.DownloadUrl);
        if (!DownloadHosts.Any(h => uri.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase)))
            return null;

        var updatesDir = App.State.Store.UpdatesDir;
        var destPath = Path.Combine(updatesDir, info.AssetName);

        try
        {
            CleanUpdates(updatesDir, destPath);

            using var resp = await Client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var file = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                progress?.Report((double)totalRead / info.AssetSize);
            }

            if (info.AssetSize > 0 && totalRead != info.AssetSize)
            {
                TryDelete(destPath);
                return null;
            }

            if (info.ChecksumUrl != null)
            {
                var ok = await VerifyChecksumAsync(destPath, info.ChecksumUrl, ct);
                if (!ok)
                {
                    TryDelete(destPath);
                    return null;
                }
            }

            return destPath;
        }
        catch
        {
            TryDelete(destPath);
            return null;
        }
    }

    private async Task<bool> VerifyChecksumAsync(string filePath, string checksumUrl, CancellationToken ct)
    {
        try
        {
            var shaLines = await Client.GetStringAsync(checksumUrl, ct);
            var expectedHash = ParseChecksum(shaLines, Path.GetFileName(filePath));
            if (expectedHash == null) return false;

            await using var fs = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(fs, ct);
            var actual = Convert.ToHexStringLower(hash);
            return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseChecksum(string content, string fileName)
    {
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var parts = trimmed.Split(new[] { ' ', '\t', '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].Trim().Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

    public void LaunchInstallerAndExit(string installerPath)
    {
        int pid = Environment.ProcessId;
        string exe = Path.Combine(AppInfo.InstallDir, "Dragonfly.exe");
        var updatesDir = App.State.Store.UpdatesDir;
        string bat = Path.Combine(updatesDir, "run-update.cmd");
        string log = Path.Combine(updatesDir, "update.log");
        string ext = Path.GetExtension(installerPath).ToLowerInvariant();

        string runLine = ext == ".msi"
            ? $"msiexec /i \"{installerPath}\" /qb /norestart"
            : $"\"{installerPath}\"";

        // Write a pre-flight log entry so we know the launcher started
        try { File.WriteAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting launcher (PID {pid}, installer: {installerPath})" + Environment.NewLine); } catch { }

        File.WriteAllText(bat, $"""
            @echo off
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Waiting for PID {pid} to exit... >> "{log}"
            :wait
            tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] PID {pid} exited. Running installer... >> "{log}"
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Running: {runLine} >> "{log}"
            {runLine}
            set EXIT_CODE=%ERRORLEVEL%
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Installer exit code: %EXIT_CODE% >> "{log}"
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Relaunching: {exe} >> "{log}"
            start "" "{exe}"
            echo [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Relaunch sent. Cleaning up... >> "{log}"
            del "%~f0"
            """);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = updatesDir,
        });

        Application.Current.Shutdown();
    }

    private static void CleanUpdates(string updatesDir, string keepPath)
    {
        try
        {
            if (!Directory.Exists(updatesDir)) return;
            foreach (var f in Directory.GetFiles(updatesDir))
            {
                if (!f.Equals(keepPath, StringComparison.OrdinalIgnoreCase))
                    TryDelete(f);
            }
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

    private record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("content_type")] string ContentType);
}
