using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class BingDailyImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
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

            // If already downloaded today and not blacklisted, reuse it directly
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
                    if (imgObj.TryGetProperty("urlbase", out var urlBaseProp))
                    {
                        urlSuffix = urlBaseProp.GetString() + "_UHD.jpg";
                    }
                    else if (imgObj.TryGetProperty("url", out var urlProp))
                    {
                        urlSuffix = urlProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(urlSuffix))
                    {
                        var fullImageUrl = urlSuffix.StartsWith("http") ? urlSuffix : $"https://www.bing.com{urlSuffix}";
                        if (!BlacklistManager.Instance.IsBlacklisted(fullImageUrl))
                        {
                            var targetFile = Path.Combine(tempDir, $"bing_{Guid.NewGuid():N}.jpg");
                            return await DownloadStreamToDiskAsync(fullImageUrl, targetFile, cancellationToken);
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
