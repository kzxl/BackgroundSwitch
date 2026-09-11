using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class WallhavenImageProvider : BaseHttpImageProvider
{
    private readonly string _query;
    private readonly string _apiKey;
    private readonly Random _random = new();

    public WallhavenImageProvider(string? query, string? apiKey = "")
    {
        _query = string.IsNullOrWhiteSpace(query) ? "nature" : query.Trim();
        _apiKey = apiKey?.Trim() ?? string.Empty;
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(_query)}&sorting=random&resolutions=1920x1080,2560x1440,3840x2160&purity=100";
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
                var candidateUrls = new List<string>();

                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("path", out var pathProp))
                    {
                        var imgUrl = pathProp.GetString();
                        if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                        {
                            candidateUrls.Add(imgUrl);
                        }
                    }
                }

                if (candidateUrls.Count > 0)
                {
                    var selectedUrl = candidateUrls[_random.Next(candidateUrls.Count)];
                    return await DownloadAndCacheImageAsync(selectedUrl, "Wallhaven", cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallhavenImageProvider] Error fetching from Wallhaven ({_query}): {ex.Message}");
        }

        return null;
    }
}
