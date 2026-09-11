using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class WallhavenImageProvider : BaseHttpImageProvider
{
    private readonly string _rawQuery;
    private readonly string _apiKey;
    private readonly TopicSelectionMode _topicMode;
    private readonly Random _random = new();

    public WallhavenImageProvider(string? query, string? apiKey = "", TopicSelectionMode topicMode = TopicSelectionMode.Random)
    {
        _rawQuery = string.IsNullOrWhiteSpace(query) ? "nature" : query.Trim();
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _topicMode = topicMode;
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentTopic = TopicResolver.ResolveTopic(_rawQuery, _topicMode, "Wallhaven", "nature");
            var url = $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(currentTopic)}&sorting=random&resolutions=1920x1080,2560x1440,3840x2160&purity=100";
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
                var candidates = new List<(string Url, string Title, string Author, string SourceUrl)>();

                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("path", out var pathProp))
                    {
                        var imgUrl = pathProp.GetString();
                        if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                        {
                            string id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            string uploader = item.TryGetProperty("uploader", out var upEl) && upEl.TryGetProperty("username", out var uProp) ? uProp.GetString() ?? "" : "Wallhaven Community";
                            string pageUrl = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : $"https://wallhaven.cc/w/{id}";
                            string title = !string.IsNullOrEmpty(currentTopic) ? $"Wallhaven #{id} ({currentTopic})" : $"Wallhaven #{id}";

                            candidates.Add((imgUrl, title, uploader, pageUrl));
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    var selected = candidates[_random.Next(candidates.Count)];
                    var downloaded = await DownloadAndCacheImageAsync(selected.Url, "Wallhaven", cancellationToken);
                    if (!string.IsNullOrEmpty(downloaded))
                    {
                        WallpaperMetadataManager.Instance.Register(new WallpaperMetadata
                        {
                            FilePath = downloaded,
                            Title = selected.Title,
                            Author = selected.Author,
                            SourceUrl = selected.SourceUrl,
                            Provider = "Wallhaven"
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
            System.Diagnostics.Debug.WriteLine($"[WallhavenImageProvider] Error fetching from Wallhaven ({_rawQuery}): {ex.Message}");
        }

        return null;
    }
}
