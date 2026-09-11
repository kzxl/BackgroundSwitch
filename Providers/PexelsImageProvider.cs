using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class PexelsImageProvider : BaseHttpImageProvider
{
    public const string DefaultApiKey = ProviderConfig.DefaultPexelsApiKey;

    private readonly string _apiKey;
    private readonly string _query;
    private readonly Random _random = new();

    public PexelsImageProvider(string? apiKey, string? query)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? DefaultApiKey : apiKey.Trim();
        _query = string.IsNullOrWhiteSpace(query) ? "nature" : query.Trim();
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int randomPage = _random.Next(1, 10);
                string url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(_query)}&per_page=15&page={randomPage}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _apiKey);

                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                if (root.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
                {
                    var candidates = new List<string>();
                    foreach (var photo in photos.EnumerateArray())
                    {
                        if (photo.TryGetProperty("src", out var src) && src.TryGetProperty("original", out var imgEl))
                        {
                            var imgUrl = imgEl.GetString();
                            if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                            {
                                candidates.Add(imgUrl);
                            }
                        }
                    }

                    if (candidates.Count > 0)
                    {
                        var chosenUrl = candidates[_random.Next(candidates.Count)];
                        var localPath = await DownloadAndCacheImageAsync(chosenUrl, "Pexels", cancellationToken);
                        if (!string.IsNullOrEmpty(localPath))
                        {
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
            System.Diagnostics.Debug.WriteLine($"[PexelsImageProvider] Error fetching image: {ex.Message}");
        }

        return null;
    }
}
