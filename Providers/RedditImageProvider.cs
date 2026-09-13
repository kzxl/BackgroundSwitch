using System.Net.Http;
using System.Text.Json;
using ZeroWall.Models;
using ZeroWall.Services;

namespace ZeroWall.Providers;

public class RedditImageProvider : BaseHttpImageProvider
{
    private readonly string _rawSubreddit;
    private readonly TopicSelectionMode _topicMode;
    private readonly Random _random = new();

    public RedditImageProvider(string? subreddit, TopicSelectionMode topicMode = TopicSelectionMode.Random)
    {
        _rawSubreddit = string.IsNullOrWhiteSpace(subreddit) ? "wallpapers" : subreddit.Trim();
        _topicMode = topicMode;
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSub = TopicResolver.ResolveTopic(_rawSubreddit, _topicMode, "Reddit", "wallpapers").TrimStart('r', '/');
            string url = $"https://www.reddit.com/r/{Uri.EscapeDataString(activeSub)}/hot.json?limit=50";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "ZeroWall/1.0 (Windows NT 10.0; Win64; x64)");

            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("children", out var children))
            {
                var candidates = new List<(string Url, string Title, string Author, string Permalink)>();

                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("data", out var postData))
                    {
                        if (postData.TryGetProperty("url_overridden_by_dest", out var urlProp) ||
                            postData.TryGetProperty("url", out urlProp))
                        {
                            // Filter out portrait/vertical phone wallpapers
                            if (postData.TryGetProperty("preview", out var prevEl) &&
                                prevEl.TryGetProperty("images", out var imgArr) &&
                                imgArr.GetArrayLength() > 0 &&
                                imgArr[0].TryGetProperty("source", out var srcEl))
                            {
                                if (srcEl.TryGetProperty("width", out var wEl) && srcEl.TryGetProperty("height", out var hEl))
                                {
                                    int w = wEl.GetInt32();
                                    int h = hEl.GetInt32();
                                    if (w <= h || (double)w / h < 1.15)
                                    {
                                        continue;
                                    }
                                }
                            }

                            var imgUrl = urlProp.GetString();
                            if (!string.IsNullOrEmpty(imgUrl) && IsDirectImageUrl(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                            {
                                string title = postData.TryGetProperty("title", out var tProp) ? tProp.GetString()?.Trim() ?? string.Empty : $"r/{activeSub}";
                                string author = postData.TryGetProperty("author", out var aProp) ? $"u/{aProp.GetString()?.Trim()}" : string.Empty;
                                string permalink = postData.TryGetProperty("permalink", out var pProp) ? $"https://reddit.com{pProp.GetString()}" : string.Empty;

                                candidates.Add((imgUrl, title, author, permalink));
                            }
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    var selected = candidates[_random.Next(candidates.Count)];
                    var downloaded = await DownloadAndCacheImageAsync(selected.Url, "Reddit", cancellationToken);
                    if (!string.IsNullOrEmpty(downloaded))
                    {
                        WallpaperMetadataManager.Instance.Register(new WallpaperMetadata
                        {
                            FilePath = downloaded,
                            Title = selected.Title,
                            Author = selected.Author,
                            SourceUrl = selected.Permalink,
                            Provider = $"Reddit (r/{activeSub})"
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
            System.Diagnostics.Debug.WriteLine($"[RedditImageProvider] Error fetching from r/{_rawSubreddit}: {ex.Message}");
        }

        return null;
    }

    private static bool IsDirectImageUrl(string url)
    {
        var clean = url.Split('?')[0].ToLowerInvariant();
        return clean.EndsWith(".jpg") || clean.EndsWith(".jpeg") || clean.EndsWith(".png") || clean.EndsWith(".webp");
    }
}
