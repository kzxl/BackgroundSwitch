using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackgroundSwitch.Models;

public enum WallpaperMode
{
    Synced = 0,     // Tất cả màn hình dùng chung 1 hình nền
    PerMonitor = 1, // Mỗi màn hình có hình nền và nguồn riêng
    Span = 2        // 1 hình nền trải rộng qua toàn bộ các màn hình
}

public enum WallpaperScale
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5
}

public class ProviderConfig
{
    public const string DefaultPexelsApiKey = "3zV2AOzguJiTCbhfkJ23RX9OKmVTECdaHr4K25jL184ojUFwz4KrOJA2";

    public string Type { get; set; } = "Pexels"; // "Pexels", "BingDaily", "Reddit", "Wallhaven", "Nasa", "Local", "Unsplash"
    public string LocalFolderPath { get; set; } = string.Empty;
    public string PexelsApiKey { get; set; } = DefaultPexelsApiKey;
    public string PexelsQuery { get; set; } = "nature";

    // New Providers Config
    public string RedditSubreddit { get; set; } = "wallpapers"; // "wallpapers", "EarthPorn", "spaceporn", "AnimeWallpaper"
    public string WallhavenQuery { get; set; } = "nature";      // "nature", "anime", "cyberpunk", "landscape"
    public string WallhavenApiKey { get; set; } = string.Empty;
    public string NasaApiKey { get; set; } = "DEMO_KEY";
    public string UnsplashApiKey { get; set; } = string.Empty;
    public string UnsplashQuery { get; set; } = "nature";
}

public class MonitorConfig
{
    public string MonitorId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public ProviderConfig Source { get; set; } = new();
}

public class AppSettings
{
    public WallpaperMode Mode { get; set; } = WallpaperMode.Synced;
    public WallpaperScale Scale { get; set; } = WallpaperScale.Fill;
    public int IntervalMinutes { get; set; } = 15;
    public bool AutoStart { get; set; } = true;
    public string Language { get; set; } = "bilingual"; // "bilingual", "vi", "en"

    // Nguồn ảnh chung khi ở chế độ Synced hoặc Span
    public ProviderConfig GlobalSource { get; set; } = new();

    // Cấu hình riêng cho từng màn hình khi ở chế độ PerMonitor
    public List<MonitorConfig> Monitors { get; set; } = new();

    // Thuộc tính tương thích ngược với bản cũ
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceType
    {
        get => GlobalSource.Type;
        set { if (value != null) GlobalSource.Type = value; }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalFolderPath
    {
        get => GlobalSource.LocalFolderPath;
        set { if (value != null) GlobalSource.LocalFolderPath = value; }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PexelsApiKey
    {
        get => GlobalSource.PexelsApiKey;
        set { if (value != null) GlobalSource.PexelsApiKey = value; }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PexelsQuery
    {
        get => GlobalSource.PexelsQuery;
        set { if (value != null) GlobalSource.PexelsQuery = value; }
    }

    public static AppSettings Load()
    {
        var path = GetSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (string.IsNullOrWhiteSpace(settings.GlobalSource.PexelsApiKey))
                    {
                        settings.GlobalSource.PexelsApiKey = ProviderConfig.DefaultPexelsApiKey;
                    }
                    if (settings.Monitors != null)
                    {
                        foreach (var mon in settings.Monitors)
                        {
                            if (mon.Source != null && string.IsNullOrWhiteSpace(mon.Source.PexelsApiKey))
                            {
                                mon.Source.PexelsApiKey = ProviderConfig.DefaultPexelsApiKey;
                            }
                        }
                    }
                    return settings;
                }
            }
            catch
            {
                // Fallback
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
