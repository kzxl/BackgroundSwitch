using System.Diagnostics;
using BackgroundSwitch.Models;
using BackgroundSwitch.Providers;

namespace BackgroundSwitch.Services;

public class Scheduler : IDisposable
{
    private System.Timers.Timer? _timer;
    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _isFirstRun = true;

    public event Action<string>? OnError;

    public Scheduler(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void UpdateSettings(AppSettings newSettings)
    {
        _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));

        if (_timer != null)
        {
            _timer.Interval = Math.Max(1, _settings.IntervalMinutes) * 60 * 1000.0;
        }

        // Apply scale position immediately on settings update
        WallpaperManager.SetPosition(_settings.Scale);
    }

    public static IImageProvider CreateProvider(ProviderConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "bingdaily" => new BingDailyImageProvider(),
            "pexels" => new PexelsImageProvider(config.PexelsApiKey, config.PexelsQuery),
            _ => new LocalFolderImageProvider(config.LocalFolderPath)
        };
    }

    public void Start(bool isBootDelayed = false)
    {
        Stop();

        // Apply scaling position
        WallpaperManager.SetPosition(_settings.Scale);

        // Run initial wallpaper change asynchronously
        Task.Run(async () =>
        {
            if (isBootDelayed && _isFirstRun)
            {
                _isFirstRun = false;
                // Wait 8 seconds on Windows startup to ensure Wi-Fi/Ethernet connects
                try
                {
                    await Task.Delay(8000);
                }
                catch { }
            }

            await ChangeWallpaperAsync();
        });

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

            // Set scaling position
            WallpaperManager.SetPosition(_settings.Scale);

            if (_settings.Mode == WallpaperMode.PerMonitor && _settings.Monitors.Count > 0)
            {
                // Mode 2: Per-Monitor (Each monitor has its own provider/image)
                var currentMonitors = WallpaperManager.GetMonitors();
                var tasks = new List<Task>();

                foreach (var monitor in currentMonitors)
                {
                    var monConfig = _settings.Monitors.FirstOrDefault(m => 
                        (!string.IsNullOrEmpty(m.MonitorId) && m.MonitorId == monitor.MonitorId) ||
                        (!string.IsNullOrEmpty(m.DeviceName) && m.DeviceName == monitor.DeviceName)) 
                        ?? new MonitorConfig { Source = _settings.GlobalSource };

                    var provider = CreateProvider(monConfig.Source);
                    var monId = monitor.MonitorId;

                    tasks.Add(Task.Run(async () =>
                    {
                        var imgPath = await provider.GetNextImagePathAsync(token);
                        if (!string.IsNullOrEmpty(imgPath) && !token.IsCancellationRequested)
                        {
                            WallpaperManager.SetWallpaper(monId, imgPath);
                        }
                    }, token));
                }

                await Task.WhenAll(tasks);
            }
            else
            {
                // Mode 1: Synced or Mode 3: Span
                var provider = CreateProvider(_settings.GlobalSource);
                var imagePath = await provider.GetNextImagePathAsync(token);

                if (!string.IsNullOrEmpty(imagePath) && !token.IsCancellationRequested)
                {
                    WallpaperManager.SetWallpaper(null, imagePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Scheduler] Error during wallpaper change: {ex.Message}");
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
