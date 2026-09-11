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
            "reddit" => new RedditImageProvider(config.RedditSubreddit, config.TopicMode),
            "wallhaven" => new WallhavenImageProvider(config.WallhavenQuery, config.WallhavenApiKey, config.TopicMode),
            "nasa" or "apod" => new NasaApodImageProvider(config.NasaApiKey),
            "unsplash" => new UnsplashImageProvider(config.UnsplashApiKey, config.UnsplashQuery, config.TopicMode),
            "bingdaily" or "bing" => new BingDailyImageProvider(),
            "pexels" => new PexelsImageProvider(config.PexelsApiKey, config.PexelsQuery, config.TopicMode),
            _ => new LocalFolderImageProvider(config.LocalFolderPath)
        };
    }
}
