using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class WallhavenImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _query;
    private readonly string _apiKey;
    private readonly Random _random = new();

    public WallhavenImageProvider(string query, string apiKey = "")
    {
        _query = string.IsNullOrWhiteSpace(query) ? "nature" : query.Trim();
        _apiKey = apiKey?.Trim() ?? string.Empty;
    }

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(_query)}&sorting=random&resolutions=1920x1080,2560x1440,3840x2160&purity=100";
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url += $"&apikey={Uri.EscapeDataString(_apiKey)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            {
                var candidateUrls = new List<string>();

                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("path", out var pathProp))
                    {
                        var imgUrl = pathProp.GetString();
                        if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                        {
                            candidateUrls.Add(imgUrl);
                        }
                    }
                }

                if (candidateUrls.Count > 0)
                {
                    var selectedUrl = candidateUrls[_random.Next(candidateUrls.Count)];
                    return await DownloadStreamToDiskAsync(selectedUrl, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallhavenImageProvider] Error fetching from Wallhaven ({_query}): {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> DownloadStreamToDiskAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "Wallhaven");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        // Cleanup old temp files (keep last 10)
        try
        {
            var files = Directory.GetFiles(tempPath, "wallhaven_*.jpg")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.LastWriteTimeUtc)
                                 .Skip(10);

            foreach (var fi in files)
            {
                try { fi.Delete(); } catch { }
            }
        }
        catch { }

        var filePath = Path.Combine(tempPath, $"wallhaven_{Guid.NewGuid():N}.jpg");
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
