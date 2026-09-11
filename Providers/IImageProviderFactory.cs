using BackgroundSwitch.Models;

namespace BackgroundSwitch.Providers;

public interface IImageProviderFactory
{
    IImageProvider CreateProvider(ProviderConfig config);
}
