using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

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
            // Pick a random page between 1 and 100 to diversify wallpapers
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
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        return await DownloadImageAsync(imageUrl, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Propagate or handle cancellation cleanly
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PexelsImageProvider] Error fetching image: {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> DownloadImageAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        // Cleanup old temp image files safely
        try
        {
            foreach (var file in Directory.GetFiles(tempPath, "wallpaper_*.jpg"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch
        {
            // Ignore folder read race conditions
        }

        var filePath = Path.Combine(tempPath, $"wallpaper_{Guid.NewGuid():N}.jpg");
        var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

        return filePath;
    }
}
