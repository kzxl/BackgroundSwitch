using BackgroundSwitch.Models;
using BackgroundSwitch.Providers;

namespace BackgroundSwitch.Services;

public class Scheduler : IDisposable
{
    private System.Timers.Timer? _timer;
    private AppSettings _settings;
    private IImageProvider _provider;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<string>? OnError;

    public Scheduler(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _provider = CreateProvider(_settings);
    }

    public void UpdateSettings(AppSettings newSettings)
    {
        _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
        _provider = CreateProvider(_settings);

        if (_timer != null)
        {
            _timer.Interval = Math.Max(1, _settings.IntervalMinutes) * 60 * 1000.0;
        }
    }

    private static IImageProvider CreateProvider(AppSettings settings)
    {
        if (string.Equals(settings.SourceType, "Pexels", StringComparison.OrdinalIgnoreCase))
        {
            return new PexelsImageProvider(settings.PexelsApiKey, settings.PexelsQuery);
        }

        return new LocalFolderImageProvider(settings.LocalFolderPath);
    }

    public void Start()
    {
        Stop();

        // Fire immediately on start in a safe background task
        Task.Run(async () => await ChangeWallpaperAsync());

        var intervalMs = Math.Max(1, _settings.IntervalMinutes) * 60 * 1000.0;
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += async (_, _) => await ChangeWallpaperAsync();
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
    }

    public async Task ChangeWallpaperAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var imagePath = await _provider.GetNextImagePathAsync(token);
            if (!string.IsNullOrEmpty(imagePath) && !token.IsCancellationRequested)
            {
                WallpaperManager.SetWallpaper(imagePath);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user cancels or triggers a new change
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
