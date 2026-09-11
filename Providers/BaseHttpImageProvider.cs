using System.IO;
using System.Net;
using System.Net.Http;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public abstract class BaseHttpImageProvider : IImageProvider
{
    protected static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static int MaxCachedCount { get; set; } = 5;

    public abstract Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default);

    protected static async Task<string?> DownloadAndCacheImageAsync(string imageUrl, string subFolder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || BlacklistManager.Instance.IsBlacklisted(imageUrl))
        {
            return null;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", subFolder);
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var targetFile = Path.Combine(tempDir, $"wallpaper_{Guid.NewGuid():N}.jpg");
        var downloaded = await DownloadStreamToDiskAsync(imageUrl, targetFile, cancellationToken);
        if (!string.IsNullOrEmpty(downloaded))
        {
            // Cuốn chiếu: Chỉ giữ tối đa 5 tấm gần nhất trong thư mục nguồn này
            CacheManager.EnforceRollingLimit(tempDir, MaxCachedCount, new[] { downloaded });
            return downloaded;
        }

        return null;
    }

    protected static async Task<string?> DownloadStreamToDiskAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var tempDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(tempDir) && !Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var tempDownloading = $"{targetPath}.tmp";

            await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempDownloading, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await httpStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }

            File.Move(tempDownloading, targetPath, true);
            return targetPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BaseHttpImageProvider] Download error for '{url}': {ex.Message}");
            return null;
        }
    }

    protected static void CleanupOldCacheFiles(string folderPath, int keepLatestCount = 10)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;

            var files = Directory.GetFiles(folderPath, "*.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(keepLatestCount);

            foreach (var fi in files)
            {
                try { fi.Delete(); } catch { }
            }
        }
        catch
        {
            // Ignore race conditions on temp folder cleanup
        }
    }
}
