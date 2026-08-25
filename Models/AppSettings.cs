using System.IO;
using System.Text.Json;

namespace BackgroundSwitch.Models;

public class AppSettings
{
    public string SourceType { get; set; } = "Local"; // "Local" or "Pexels"
    public string LocalFolderPath { get; set; } = string.Empty;
    public string PexelsApiKey { get; set; } = string.Empty;
    public string PexelsQuery { get; set; } = "nature";
    public int IntervalMinutes { get; set; } = 15;
    public bool AutoStart { get; set; } = true;

    public static AppSettings Load()
    {
        var path = GetSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                // Fallback to default on deserialize error
            }
        }
        return new AppSettings();
    }

    public void Save()
    {
        var path = GetSettingsPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BackgroundSwitch", "settings.json");
    }
}
