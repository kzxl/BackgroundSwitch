using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class UnsplashImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _apiKey;
    private readonly string _query;

    public UnsplashImageProvider(string apiKey, string query)
    {
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _query = string.IsNullOrWhiteSpace(query) ? "landscape" : query.Trim();
    }

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string url;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url = $"https://api.unsplash.com/photos/random?query={Uri.EscapeDataString(_query)}&orientation=landscape&client_id={Uri.EscapeDataString(_apiKey)}";
            }
            else
            {
                // Fallback to Unsplash Source open redirect endpoint (No key needed)
                url = $"https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=3840&q=85";
            }

            if (!string.IsNullOrEmpty(_apiKey))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("urls", out var urls) && urls.TryGetProperty("full", out var fullUrlProp))
                {
                    var imgUrl = fullUrlProp.GetString();
                    if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                    {
                        return await DownloadStreamToDiskAsync(imgUrl, cancellationToken);
                    }
                }
            }
            else
            {
                return await DownloadStreamToDiskAsync(url, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UnsplashImageProvider] Error fetching from Unsplash ({_query}): {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> DownloadStreamToDiskAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "Unsplash");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        // Cleanup old temp files (keep last 10)
        try
        {
            var files = Directory.GetFiles(tempPath, "unsplash_*.jpg")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.LastWriteTimeUtc)
                                 .Skip(10);

            foreach (var fi in files)
            {
                try { fi.Delete(); } catch { }
            }
        }
        catch { }

        var filePath = Path.Combine(tempPath, $"unsplash_{Guid.NewGuid():N}.jpg");
        var tempDownloading = $"{filePath}.tmp";

        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(tempDownloading, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await httpStream.CopyToAsync(fileStream, cancellationToken);
        }

        File.Move(tempDownloading, filePath);
        return filePath;
    }
}
