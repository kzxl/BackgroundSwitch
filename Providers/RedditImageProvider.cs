using System.IO;
using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class RedditImageProvider : IImageProvider
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _subreddit;
    private readonly Random _random = new();

    public RedditImageProvider(string subreddit)
    {
        _subreddit = string.IsNullOrWhiteSpace(subreddit) ? "wallpapers" : subreddit.Trim().TrimStart('r', '/');
    }

    public async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use public Reddit JSON endpoint with unique User-Agent
            string url = $"https://www.reddit.com/r/{Uri.EscapeDataString(_subreddit)}/hot.json?limit=50";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "BackgroundSwitch/1.0 (Windows NT 10.0; Win64; x64)");

            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("children", out var children))
            {
                var candidateUrls = new List<string>();

                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("data", out var postData))
                    {
                        // Check post_hint or direct image url
                        if (postData.TryGetProperty("url_overridden_by_dest", out var urlProp) ||
                            postData.TryGetProperty("url", out urlProp))
                        {
                            var imgUrl = urlProp.GetString();
                            if (!string.IsNullOrEmpty(imgUrl) && IsDirectImageUrl(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                            {
                                candidateUrls.Add(imgUrl);
                            }
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
            System.Diagnostics.Debug.WriteLine($"[RedditImageProvider] Error fetching from r/{_subreddit}: {ex.Message}");
        }

        return null;
    }

    private static bool IsDirectImageUrl(string url)
    {
        var clean = url.Split('?')[0].ToLowerInvariant();
        return clean.EndsWith(".jpg") || clean.EndsWith(".jpeg") || clean.EndsWith(".png") || clean.EndsWith(".webp");
    }

    private static async Task<string?> DownloadStreamToDiskAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "Reddit");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        // Cleanup old temp image files safely (keep last 10)
        try
        {
            var files = Directory.GetFiles(tempPath, "reddit_*.jpg")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.LastWriteTimeUtc)
                                 .Skip(10);

            foreach (var fi in files)
            {
                try { fi.Delete(); } catch { }
            }
        }
        catch { }

        var filePath = Path.Combine(tempPath, $"reddit_{Guid.NewGuid():N}.jpg");
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
