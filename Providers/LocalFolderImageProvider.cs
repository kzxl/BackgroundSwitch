using System.IO;

namespace BackgroundSwitch.Providers;

public class LocalFolderImageProvider : IImageProvider
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];
    private readonly string _folderPath;
    private readonly Random _random = new();

    public LocalFolderImageProvider(string folderPath)
    {
        _folderPath = folderPath ?? string.Empty;
    }

    public Task<string?> GetNextImagePathAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<string?>(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(_folderPath) || !Directory.Exists(_folderPath))
        {
            return Task.FromResult<string?>(null);
        }

        var files = Directory.EnumerateFiles(_folderPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (files.Count == 0)
        {
            return Task.FromResult<string?>(null);
        }

        var randomFile = files[_random.Next(files.Count)];
        return Task.FromResult<string?>(randomFile);
    }
}
