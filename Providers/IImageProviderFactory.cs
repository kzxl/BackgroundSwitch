using ZeroWall.Models;

namespace ZeroWall.Providers;

public interface IImageProviderFactory
{
    IImageProvider CreateProvider(ProviderConfig config);
}
