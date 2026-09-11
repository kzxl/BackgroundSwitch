using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class BingDailyImageProvider : BaseHttpImageProvider
{
    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "BingDaily");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var cachedTodayFile = Path.Combine(tempDir, $"bing_{todayStr}.jpg");

            // If already downloaded today and valid (>10KB), reuse it
            if (File.Exists(cachedTodayFile) && new FileInfo(cachedTodayFile).Length > 10000)
            {
                if (!BlacklistManager.Instance.IsBlacklisted(cachedTodayFile))
                {
                    return cachedTodayFile;
                }
            }

            const string archiveApiUrl = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=8&mkt=en-US";
            using var request = new HttpRequestMessage(HttpMethod.Get, archiveApiUrl);
            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
            {
                foreach (var imgObj in images.EnumerateArray())
                {
                    string? urlSuffix = null;
                    if (imgObj.TryGetProperty("url", out var urlProp))
                    {
                        urlSuffix = urlProp.GetString();
                    }
                    else if (imgObj.TryGetProperty("urlbase", out var urlBaseProp))
                    {
                        urlSuffix = urlBaseProp.GetString() + "_1920x1080.jpg";
                    }

                    if (!string.IsNullOrEmpty(urlSuffix))
                    {
                        var fullImageUrl = urlSuffix.StartsWith("http") ? urlSuffix : $"https://www.bing.com{urlSuffix}";
                        if (!BlacklistManager.Instance.IsBlacklisted(fullImageUrl))
                        {
                            var targetFile = Path.Combine(tempDir, $"bing_{Guid.NewGuid():N}.jpg");
                            var downloadedPath = await DownloadStreamToDiskAsync(fullImageUrl, targetFile, cancellationToken);
                            if (!string.IsNullOrEmpty(downloadedPath))
                            {
                                string title = imgObj.TryGetProperty("title", out var tProp) ? tProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                                string copyright = imgObj.TryGetProperty("copyright", out var crProp) ? crProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                                string copyrightLink = imgObj.TryGetProperty("copyrightlink", out var clProp) ? clProp.GetString()?.Trim() ?? string.Empty : string.Empty;

                                WallpaperMetadataManager.Instance.Register(new Models.WallpaperMetadata
                                {
                                    FilePath = downloadedPath,
                                    Title = !string.IsNullOrEmpty(title) ? title : "Bing Daily Wallpaper",
                                    Author = copyright,
                                    SourceUrl = copyrightLink,
                                    Provider = "BingDaily"
                                });

                                return downloadedPath;
                            }
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
            System.Diagnostics.Debug.WriteLine($"[BingDailyImageProvider] Error fetching Bing Daily wallpaper: {ex.Message}");
        }

        return null;
    }
}
