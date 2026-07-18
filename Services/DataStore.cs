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
    public string DataDir { get; }
    public string DataFile { get; }
    public AppData Data { get; private set; } = new();

    public event Action? Changed;

    public DataStore()
    {
        DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OvertorqueCreations", "Dragonfly");
        Directory.CreateDirectory(DataDir);
        DataFile = Path.Combine(DataDir, "dragonfly-data.json");
        Load();
    }

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
                var backup = Path.Combine(DataDir, $"backup-{DateTime.Now:yyyy-MM-dd}.json");
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
        var backups = Directory.GetFiles(DataDir, "backup-*.json").OrderByDescending(f => f).Skip(14);
        foreach (var f in backups) { try { File.Delete(f); } catch { } }
    }
}
