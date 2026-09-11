using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class RedditImageProvider : BaseHttpImageProvider
{
    private readonly string _subreddit;
    private readonly Random _random = new();

    public RedditImageProvider(string? subreddit)
    {
        _subreddit = string.IsNullOrWhiteSpace(subreddit) ? "wallpapers" : subreddit.Trim().TrimStart('r', '/');
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
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
                    return await DownloadAndCacheImageAsync(selectedUrl, "Reddit", cancellationToken);
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
}
