using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class PexelsImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _apiKey;
    private readonly string _query;
    private readonly Random _random = new();

    public PexelsImageProvider(string apiKey, string query)
    {
        _apiKey = apiKey ?? string.Empty;
        _query = string.IsNullOrWhiteSpace(query) ? "nature" : query;
    }

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return null;
        }

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int randomPage = _random.Next(1, 100);
                string url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(_query)}&per_page=1&page={randomPage}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _apiKey);

                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                if (root.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
                {
                    var photo = photos[0];
                    if (photo.TryGetProperty("src", out var src) && src.TryGetProperty("original", out var imageUrlElement))
                    {
                        var imageUrl = imageUrlElement.GetString();
                        if (!string.IsNullOrEmpty(imageUrl) && !BlacklistManager.Instance.IsBlacklisted(imageUrl))
                        {
                            return await DownloadStreamToDiskAsync(imageUrl, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PexelsImageProvider] Error fetching image: {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> DownloadStreamToDiskAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "Pexels");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        // Cleanup old temp image files safely (keep last 5)
        try
        {
            var files = Directory.GetFiles(tempPath, "wallpaper_*.jpg")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.LastWriteTimeUtc)
                                 .Skip(5);

            foreach (var fi in files)
            {
                try { fi.Delete(); } catch { }
            }
        }
        catch
        {
            // Ignore folder race conditions
        }

        var filePath = Path.Combine(tempPath, $"wallpaper_{Guid.NewGuid():N}.jpg");
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
