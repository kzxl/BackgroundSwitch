using System.Collections.Concurrent;
using System.IO;
using ZeroWall.Models;

namespace ZeroWall.Services;

public class WallpaperMetadataManager
{
    private static readonly Lazy<WallpaperMetadataManager> _instance = new(() => new WallpaperMetadataManager());
    public static WallpaperMetadataManager Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, WallpaperMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);
    private WallpaperMetadata? _currentActive;

    public WallpaperMetadata? CurrentActive
    {
        get => _currentActive;
        set => _currentActive = value;
    }

    public void Register(WallpaperMetadata metadata)
    {
        if (metadata == null || string.IsNullOrEmpty(metadata.FilePath)) return;
        _cache[metadata.FilePath] = metadata;
        _currentActive = metadata;
    }

    public WallpaperMetadata? GetMetadata(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        if (_cache.TryGetValue(filePath, out var meta)) return meta;

        // Fallback: Tạo metadata cơ bản từ tên file nếu file tồn tại
        if (File.Exists(filePath))
        {
            var fallback = new WallpaperMetadata
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath),
                Author = "Không rõ tác giả",
                Provider = "Local"
            };
            _cache[filePath] = fallback;
            return fallback;
        }

        return null;
    }

    public void Clear()
    {
        _cache.Clear();
        _currentActive = null;
    }
}
