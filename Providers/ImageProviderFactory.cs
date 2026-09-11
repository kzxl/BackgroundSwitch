using BackgroundSwitch.Models;

namespace BackgroundSwitch.Providers;

public class ImageProviderFactory : IImageProviderFactory
{
    private static readonly Lazy<ImageProviderFactory> _instance = new(() => new ImageProviderFactory());
    public static ImageProviderFactory Instance => _instance.Value;

    public IImageProvider CreateProvider(ProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var type = config.Type?.ToLowerInvariant() ?? string.Empty;
        return type switch
        {
            "reddit" => new RedditImageProvider(config.RedditSubreddit),
            "wallhaven" => new WallhavenImageProvider(config.WallhavenQuery, config.WallhavenApiKey),
            "nasa" or "apod" => new NasaApodImageProvider(config.NasaApiKey),
            "unsplash" => new UnsplashImageProvider(config.UnsplashApiKey, config.UnsplashQuery),
            "bingdaily" or "bing" => new BingDailyImageProvider(),
            "pexels" => new PexelsImageProvider(config.PexelsApiKey, config.PexelsQuery),
            _ => new LocalFolderImageProvider(config.LocalFolderPath)
        };
    }
}
