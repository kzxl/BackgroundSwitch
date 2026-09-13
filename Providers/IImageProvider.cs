namespace ZeroWall.Providers;

public interface IImageProvider
{
    Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default);
}
