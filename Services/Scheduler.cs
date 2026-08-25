using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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

    public bool IsPaused { get; private set; }
    public WallpaperHistoryManager History { get; } = new();
    public ConcurrentDictionary<string, string> CurrentWallpapers { get; } = new();

    public event Action<string>? OnError;
    public event Action? OnWallpaperChanged;
    public event Action<bool>? OnPauseStateChanged;

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

        // Apply scale position immediately
        WallpaperManager.SetPosition(_settings.Scale);
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            _timer?.Stop();
        }
        else
        {
            _timer?.Start();
        }
        OnPauseStateChanged?.Invoke(IsPaused);
    }

    public static IImageProvider CreateProvider(ProviderConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "reddit" => new RedditImageProvider(config.RedditSubreddit),
            "wallhaven" => new WallhavenImageProvider(config.WallhavenQuery, config.WallhavenApiKey),
            "nasa" or "apod" => new NasaApodImageProvider(config.NasaApiKey),
            "unsplash" => new UnsplashImageProvider(config.UnsplashApiKey, config.UnsplashQuery),
            "bingdaily" => new BingDailyImageProvider(),
            "pexels" => new PexelsImageProvider(config.PexelsApiKey, config.PexelsQuery),
            _ => new LocalFolderImageProvider(config.LocalFolderPath)
        };
    }

    public void Start(bool isBootDelayed = false)
    {
        Stop();
        IsPaused = false;

        // Apply scaling position
        WallpaperManager.SetPosition(_settings.Scale);

        // Run initial wallpaper change asynchronously
        Task.Run(async () =>
        {
            if (isBootDelayed && _isFirstRun)
            {
                _isFirstRun = false;
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
        _timer.Elapsed += async (_, _) =>
        {
            if (!IsPaused)
            {
                await ChangeWallpaperAsync();
            }
        };
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
                            CurrentWallpapers[monId] = imgPath;
                            History.Push(imgPath);
                        }
                    }, token));
                }

                await Task.WhenAll(tasks);
            }
            else
            {
                var provider = CreateProvider(_settings.GlobalSource);
                var imagePath = await provider.GetNextImagePathAsync(token);

                if (!string.IsNullOrEmpty(imagePath) && !token.IsCancellationRequested)
                {
                    WallpaperManager.SetWallpaper(null, imagePath);
                    CurrentWallpapers["global"] = imagePath;
                    History.Push(imagePath);
                }
            }

            OnWallpaperChanged?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Scheduler] Error during wallpaper change: {ex.Message}");
            OnError?.Invoke(ex.Message);
        }
    }

    public async Task PreviousWallpaperAsync()
    {
        var prevImage = History.GetPrevious();
        if (!string.IsNullOrEmpty(prevImage) && File.Exists(prevImage))
        {
            WallpaperManager.SetPosition(_settings.Scale);
            WallpaperManager.SetWallpaper(null, prevImage);
            CurrentWallpapers["global"] = prevImage;
            OnWallpaperChanged?.Invoke();
            await Task.CompletedTask;
        }
    }

    public async Task BlacklistCurrentAsync()
    {
        var currentImg = GetCurrentActiveWallpaperPath();
        if (!string.IsNullOrEmpty(currentImg))
        {
            BlacklistManager.Instance.Add(currentImg);
            History.Remove(currentImg);
        }
        await ChangeWallpaperAsync();
    }

    public async Task ChangeWallpaperForMonitorAsync(uint monitorIndex)
    {
        var monitors = WallpaperManager.GetMonitors();
        if (monitorIndex >= monitors.Count) return;

        var monitor = monitors[(int)monitorIndex];
        var monConfig = _settings.Monitors.FirstOrDefault(m => m.MonitorId == monitor.MonitorId)
                        ?? new MonitorConfig { Source = _settings.GlobalSource };

        var provider = CreateProvider(monConfig.Source);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var imgPath = await provider.GetNextImagePathAsync(cts.Token);

        if (!string.IsNullOrEmpty(imgPath))
        {
            WallpaperManager.SetWallpaper(monitor.MonitorId, imgPath);
            CurrentWallpapers[monitor.MonitorId] = imgPath;
            History.Push(imgPath);
            OnWallpaperChanged?.Invoke();
        }
    }

    public string? GetCurrentActiveWallpaperPath()
    {
        if (CurrentWallpapers.TryGetValue("global", out var globalPath) && !string.IsNullOrEmpty(globalPath))
        {
            return globalPath;
        }

        if (CurrentWallpapers.Count > 0)
        {
            return CurrentWallpapers.Values.FirstOrDefault(p => !string.IsNullOrEmpty(p));
        }

        return History.Current;
    }

    public void ClearWallpaper()
    {
        WallpaperManager.SetWallpaper(null, string.Empty);
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
