using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class NasaApodImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _apiKey;

    public NasaApodImageProvider(string apiKey = "")
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? "DEMO_KEY" : apiKey.Trim();
    }

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "NasaApod");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var cachedTodayFile = Path.Combine(tempDir, $"apod_{todayStr}.jpg");

            // Reuse today's image if already downloaded and not blacklisted
            if (File.Exists(cachedTodayFile) && new FileInfo(cachedTodayFile).Length > 10000)
            {
                if (!BlacklistManager.Instance.IsBlacklisted(cachedTodayFile))
                {
                    return cachedTodayFile;
                }
            }

            // Fetch latest APOD metadata
            string url = $"https://api.nasa.gov/planetary/apod?api_key={Uri.EscapeDataString(_apiKey)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check if media_type is "image" (sometimes it's a YouTube video)
            if (root.TryGetProperty("media_type", out var mediaType) && mediaType.GetString() == "image")
            {
                string? imgUrl = null;
                if (root.TryGetProperty("hdurl", out var hdUrlProp))
                {
                    imgUrl = hdUrlProp.GetString();
                }
                else if (root.TryGetProperty("url", out var urlProp))
                {
                    imgUrl = urlProp.GetString();
                }

                if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                {
                    return await DownloadStreamToDiskAsync(imgUrl, cachedTodayFile, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NasaApodImageProvider] Error fetching NASA APOD: {ex.Message}");
        }

        return null;
    }

    private static async Task<string?> DownloadStreamToDiskAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempDownloadingFile = $"{targetPath}.tmp";
        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(tempDownloadingFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await httpStream.CopyToAsync(fileStream, cancellationToken);
        }

        if (File.Exists(targetPath))
        {
            try { File.Delete(targetPath); } catch { }
        }

        File.Move(tempDownloadingFile, targetPath);
        return targetPath;
    }
}
