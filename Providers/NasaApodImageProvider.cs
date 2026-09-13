using System.IO;
using System.Net.Http;
using System.Text.Json;
using ZeroWall.Services;

namespace ZeroWall.Providers;

public class NasaApodImageProvider : BaseHttpImageProvider
{
    private readonly string _apiKey;

    public NasaApodImageProvider(string? apiKey = "")
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? "DEMO_KEY" : apiKey.Trim();
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ZeroWall", "NasaApod");
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
                    var downloaded = await DownloadStreamToDiskAsync(imgUrl, cachedTodayFile, cancellationToken);
                    if (!string.IsNullOrEmpty(downloaded))
                    {
                        string title = root.TryGetProperty("title", out var tProp) ? tProp.GetString()?.Trim() ?? string.Empty : "NASA APOD";
                        string copyright = root.TryGetProperty("copyright", out var crProp) ? crProp.GetString()?.Trim() ?? string.Empty : "NASA / JPL";

                        WallpaperMetadataManager.Instance.Register(new Models.WallpaperMetadata
                        {
                            FilePath = downloaded,
                            Title = title,
                            Author = copyright,
                            SourceUrl = imgUrl,
                            Provider = "NasaApod"
                        });
                        return downloaded;
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
            System.Diagnostics.Debug.WriteLine($"[NasaApodImageProvider] Error fetching NASA APOD: {ex.Message}");
        }

        return null;
    }
}
