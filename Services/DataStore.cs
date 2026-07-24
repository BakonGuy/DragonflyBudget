using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dragonfly.Models;

namespace Dragonfly.Services;

/// <summary>Single-file JSON persistence with atomic writes and rolling backups.</summary>
public class DataStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _lock = new();
    /// <summary>Root app folder — also the install directory, so app data lives under <see cref="SavedDir"/>.</summary>
    public string RootDir { get; }
    /// <summary>Where the data file and backups live, kept out of the install root.</summary>
    public string SavedDir { get; }
    public string BackupDir { get; }
    public string DataFile { get; }

    // Back-compat: some callers still ask for "DataDir" (now the Saved folder).
    public string DataDir => SavedDir;

    public AppData Data { get; private set; } = new();

    public event Action? Changed;

    public DataStore()
    {
        RootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OvertorqueCreations", "Dragonfly");
        SavedDir = Path.Combine(RootDir, "Saved");
        BackupDir = Path.Combine(SavedDir, "Backups");
        Directory.CreateDirectory(SavedDir);
        Directory.CreateDirectory(BackupDir);
        DataFile = Path.Combine(SavedDir, "dragonfly-data.json");

        MigrateStorageLayout();
        Load();
    }

    /// <summary>
    /// v1.0 stored the data file and backups directly in the root folder, which doubles as the
    /// install directory. Move any pre-existing files into Saved\ / Saved\Backups\ once. Only the
    /// known data/backup file names are touched — installed binaries are left alone.
    /// </summary>
    private void MigrateStorageLayout()
    {
        try
        {
            var oldData = Path.Combine(RootDir, "dragonfly-data.json");
            if (File.Exists(oldData) && !File.Exists(DataFile))
                File.Move(oldData, DataFile);

            foreach (var old in Directory.GetFiles(RootDir, "backup-*.json"))
            {
                var dest = Path.Combine(BackupDir, Path.GetFileName(old));
                if (!File.Exists(dest)) File.Move(old, dest);
                else File.Delete(old);
            }

            // Corrupt-file copies from the old location, if any.
            foreach (var old in Directory.GetFiles(RootDir, "dragonfly-data.json.corrupt-*"))
            {
                var dest = Path.Combine(SavedDir, Path.GetFileName(old));
                if (!File.Exists(dest)) File.Move(old, dest);
            }
        }
        catch
        {
            // Never let a migration hiccup stop the app from starting.
        }
    }

    /// <summary>
    /// Current on-disk schema version. Stays at 2 for the whole of v1.1 development — the single
    /// v1 -> v2 migration is amended as models change (see dev/README.md), so it is not bumped per
    /// change. Bump to 3 only when the next release starts a new round of schema changes.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private void Load()
    {
        if (File.Exists(DataFile))
        {
            try
            {
                Data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataFile), JsonOpts) ?? new AppData();
            }
            catch
            {
                // corrupted file: keep it aside, start fresh rather than crash
                File.Copy(DataFile, DataFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
                Data = new AppData();
            }
        }
        Migrate();
    }

    /// <summary>Upgrade loaded data to the current schema version, writing a one-time safety backup first.</summary>
    private void Migrate()
    {
        if (Data.Version >= CurrentSchemaVersion) return;

        // Safety net: keep a labelled copy of the pre-upgrade file before we change anything.
        if (File.Exists(DataFile))
        {
            try
            {
                var safe = Path.Combine(BackupDir, $"pre-v1.1-schema{Data.Version}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                if (!File.Exists(safe)) File.Copy(DataFile, safe);
            }
            catch { /* backup is best-effort */ }
        }

        MigrationV1ToV2.Apply(Data);

        Data.Version = CurrentSchemaVersion;
        Save();
    }

    public void Save()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(Data, JsonOpts);
            var tmp = DataFile + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(DataFile))
            {
                // rolling daily backup
                var backup = Path.Combine(BackupDir, $"backup-{DateTime.Now:yyyy-MM-dd}.json");
                if (!File.Exists(backup)) File.Copy(DataFile, backup);
                PruneBackups();
                File.Replace(tmp, DataFile, null);
            }
            else
            {
                File.Move(tmp, DataFile);
            }
        }
        Changed?.Invoke();
    }

    private void PruneBackups()
    {
        var backups = Directory.GetFiles(BackupDir, "backup-*.json").OrderByDescending(f => f).Skip(14);
        foreach (var f in backups) { try { File.Delete(f); } catch { } }
    }
}
