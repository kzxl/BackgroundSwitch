using System.Net.Http;
using System.Text.Json;
using ZeroWall.Models;
using ZeroWall.Services;

namespace ZeroWall.Providers;

public class PexelsImageProvider : BaseHttpImageProvider
{
    public const string DefaultApiKey = ProviderConfig.DefaultPexelsApiKey;

    private readonly string _apiKey;
    private readonly string _rawQuery;
    private readonly TopicSelectionMode _topicMode;
    private readonly Random _random = new();

    public PexelsImageProvider(string? apiKey, string? query, TopicSelectionMode topicMode = TopicSelectionMode.Random)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? DefaultApiKey : apiKey.Trim();
        _rawQuery = string.IsNullOrWhiteSpace(query) ? "nature" : query.Trim();
        _topicMode = topicMode;
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string currentTopic = TopicResolver.ResolveTopic(_rawQuery, _topicMode, "Pexels", "nature");

            for (int attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int randomPage = _random.Next(1, 10);
                string url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(currentTopic)}&orientation=landscape&per_page=15&page={randomPage}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _apiKey);

                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                if (root.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
                {
                    var candidates = new List<(string Url, string Title, string Author, string PhotoUrl)>();
                    foreach (var photo in photos.EnumerateArray())
                    {
                        // Filter out any portrait or square images to avoid bad crops on widescreen monitors
                        if (photo.TryGetProperty("width", out var wEl) && photo.TryGetProperty("height", out var hEl))
                        {
                            int w = wEl.GetInt32();
                            int h = hEl.GetInt32();
                            if (w <= h || (double)w / h < 1.15)
                            {
                                continue;
                            }
                        }

                        if (photo.TryGetProperty("src", out var src) && src.TryGetProperty("original", out var imgEl))
                        {
                            var imgUrl = imgEl.GetString();
                            if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                            {
                                string title = photo.TryGetProperty("alt", out var altProp) && !string.IsNullOrWhiteSpace(altProp.GetString())
                                    ? altProp.GetString()!.Trim()
                                    : currentTopic;
                                string author = photo.TryGetProperty("photographer", out var authProp) ? authProp.GetString()?.Trim() ?? string.Empty : string.Empty;
                                string photoUrl = photo.TryGetProperty("url", out var urlProp) ? urlProp.GetString()?.Trim() ?? string.Empty : string.Empty;

                                candidates.Add((imgUrl, title, author, photoUrl));
                            }
                        }
                    }

                    if (candidates.Count > 0)
                    {
                        var chosen = candidates[_random.Next(candidates.Count)];
                        var localPath = await DownloadAndCacheImageAsync(chosen.Url, "Pexels", cancellationToken);
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            WallpaperMetadataManager.Instance.Register(new WallpaperMetadata
                            {
                                FilePath = localPath,
                                Title = chosen.Title,
                                Author = chosen.Author,
                                SourceUrl = chosen.PhotoUrl,
                                Provider = "Pexels"
                            });
                            return localPath;
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
            System.Diagnostics.Debug.WriteLine($"[PexelsImageProvider] Error fetching image ({_rawQuery}): {ex.Message}");
        }

        return null;
    }
}
