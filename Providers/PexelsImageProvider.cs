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

                int randomPage = _random.Next(1, 100);
                string url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(_query)}&per_page=1&page={randomPage}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _apiKey);

                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                if (root.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
                {
                    var photo = photos[0];
                    if (photo.TryGetProperty("src", out var src) && src.TryGetProperty("original", out var imageUrlElement))
                    {
                        var imageUrl = imageUrlElement.GetString();
                        if (!string.IsNullOrEmpty(imageUrl) && !BlacklistManager.Instance.IsBlacklisted(imageUrl))
                        {
                            var localPath = await DownloadAndCacheImageAsync(imageUrl, "Pexels", cancellationToken);
                            if (!string.IsNullOrEmpty(localPath))
                            {
                                return localPath;
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
            System.Diagnostics.Debug.WriteLine($"[PexelsImageProvider] Error fetching image: {ex.Message}");
        }

        return null;
    }
}
