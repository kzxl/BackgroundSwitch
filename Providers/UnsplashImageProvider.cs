using System.Net.Http;
using System.Text.Json;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Providers;

public class UnsplashImageProvider : BaseHttpImageProvider
{
    private readonly string _apiKey;
    private readonly string _query;

    public UnsplashImageProvider(string? apiKey, string? query)
    {
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _query = string.IsNullOrWhiteSpace(query) ? "landscape" : query.Trim();
    }

    public override async Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string url;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url = $"https://api.unsplash.com/photos/random?query={Uri.EscapeDataString(_query)}&orientation=landscape&client_id={Uri.EscapeDataString(_apiKey)}";
            }
            else
            {
                // Fallback to Unsplash Source open redirect endpoint (No key needed)
                url = "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=3840&q=85";
            }

            if (!string.IsNullOrEmpty(_apiKey))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("urls", out var urls) && urls.TryGetProperty("full", out var fullUrlProp))
                {
                    var imgUrl = fullUrlProp.GetString();
                    if (!string.IsNullOrEmpty(imgUrl) && !BlacklistManager.Instance.IsBlacklisted(imgUrl))
                    {
                        return await DownloadAndCacheImageAsync(imgUrl, "Unsplash", cancellationToken);
                    }
                }
            }
            else
            {
                return await DownloadAndCacheImageAsync(url, "Unsplash", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UnsplashImageProvider] Error fetching from Unsplash ({_query}): {ex.Message}");
        }

        return null;
    }
}
