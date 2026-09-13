using System.IO;
using System.Text.Json;

namespace ZeroWall.Services;

public class BlacklistManager
{
    private static readonly Lazy<BlacklistManager> _instance = new(() => new BlacklistManager());
    public static BlacklistManager Instance => _instance.Value;

    private readonly HashSet<string> _blacklisted = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _blacklisted.Count;
            }
        }
    }

    public BlacklistManager()
    {
        Load();
    }

    public bool IsBlacklisted(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return false;

        lock (_lock)
        {
            return _blacklisted.Contains(pathOrUrl.Trim()) || 
                   _blacklisted.Contains(Path.GetFileName(pathOrUrl));
        }
    }

    public void Add(string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return;

        lock (_lock)
        {
            _blacklisted.Add(pathOrUrl.Trim());
            _blacklisted.Add(Path.GetFileName(pathOrUrl));
        }
        Save();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _blacklisted.Clear();
        }
        Save();
    }

    private void Load()
    {
        var path = GetBlacklistFilePath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    lock (_lock)
                    {
                        _blacklisted.Clear();
                        foreach (var item in list)
                        {
                            _blacklisted.Add(item);
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }
    }

    private void Save()
    {
        var path = GetBlacklistFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            List<string> snapshot;
            lock (_lock)
            {
                snapshot = [.. _blacklisted];
            }
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private static string GetBlacklistFilePath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZeroWall", "blacklist.json");
    }
}
