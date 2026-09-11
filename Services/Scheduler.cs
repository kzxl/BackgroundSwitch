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

    private readonly IImageProviderFactory _providerFactory;

    public Scheduler(AppSettings settings, IImageProviderFactory? providerFactory = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _providerFactory = providerFactory ?? ImageProviderFactory.Instance;
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

    public static IImageProvider CreateProvider(ProviderConfig config) => ImageProviderFactory.Instance.CreateProvider(config);
    public IImageProvider ResolveProvider(ProviderConfig config) => _providerFactory.CreateProvider(config);

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

    private readonly SemaphoreSlim _wallpaperLock = new(1, 1);

    public async Task<bool> ChangeWallpaperAsync()
    {
        if (!await _wallpaperLock.WaitAsync(0))
        {
            return false;
        }

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Set scaling position
            WallpaperManager.SetPosition(_settings.Scale);

            bool anySuccess = false;

            if (_settings.Mode == WallpaperMode.PerMonitor)
            {
                var currentMonitors = WallpaperManager.GetMonitors();
                var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var monitor in currentMonitors)
                {
                    if (token.IsCancellationRequested) break;

                    var monConfig = _settings.Monitors.FirstOrDefault(m => 
                        (!string.IsNullOrEmpty(m.MonitorId) && m.MonitorId == monitor.MonitorId) ||
                        (!string.IsNullOrEmpty(m.DeviceName) && m.DeviceName == monitor.DeviceName));

                    var sourceConfig = (monConfig?.Source != null && !string.Equals(monConfig.Source.Type, "UseGlobal", StringComparison.OrdinalIgnoreCase))
                        ? monConfig.Source
                        : _settings.GlobalSource;

                    var provider = ResolveProvider(sourceConfig);
                    var monId = monitor.MonitorId;

                    string? imgPath = null;
                    // Anti-duplicate attempt: try up to 3 times to get an image not yet used for another monitor
                    for (int retry = 0; retry < 3; retry++)
                    {
                        var candidate = await provider.GetNextImagePathAsync(token);
                        if (!string.IsNullOrEmpty(candidate))
                        {
                            imgPath = candidate;
                            if (!usedPaths.Contains(candidate))
                            {
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(imgPath) && !token.IsCancellationRequested)
                    {
                        usedPaths.Add(imgPath);
                        var meta = WallpaperMetadataManager.Instance.GetMetadata(imgPath);
                        string applied = imgPath;

                        if (_settings.ShowWallpaperInfoOnDesktop && meta != null)
                        {
                            applied = WallpaperWatermarkService.CreateWatermarkedWallpaper(imgPath, meta);
                        }

                        bool ok = WallpaperManager.SetWallpaper(monId, applied);
                        if (ok)
                        {
                            CurrentWallpapers[monId] = imgPath;
                            History.Push(imgPath);
                            anySuccess = true;
                        }
                    }
                }
            }
            else
            {
                var provider = ResolveProvider(_settings.GlobalSource);
                var imagePath = await provider.GetNextImagePathAsync(token);

                if (!string.IsNullOrEmpty(imagePath) && !token.IsCancellationRequested)
                {
                    var meta = WallpaperMetadataManager.Instance.GetMetadata(imagePath);
                    string applied = imagePath;

                    if (_settings.ShowWallpaperInfoOnDesktop && meta != null)
                    {
                        applied = WallpaperWatermarkService.CreateWatermarkedWallpaper(imagePath, meta);
                    }

                    bool ok = WallpaperManager.SetWallpaper(null, applied);
                    if (ok)
                    {
                        CurrentWallpapers["global"] = imagePath;
                        History.Push(imagePath);
                        anySuccess = true;
                    }
                }
                else
                {
                    OnError?.Invoke("Không thể tải ảnh từ nguồn đã chọn. Vui lòng kiểm tra lại cấu hình hoặc kết nối mạng.");
                }
            }

            if (anySuccess)
            {
                OnWallpaperChanged?.Invoke();
            }

            return anySuccess;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Scheduler] Error during wallpaper change: {ex.Message}");
            OnError?.Invoke(ex.Message);
            return false;
        }
        finally
        {
            _wallpaperLock.Release();
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

    public async Task<bool> ChangeWallpaperForMonitorAsync(string monitorId)
    {
        var monitors = WallpaperManager.GetMonitors();
        var monitor = monitors.FirstOrDefault(m => 
            (!string.IsNullOrEmpty(m.MonitorId) && m.MonitorId == monitorId) ||
            (!string.IsNullOrEmpty(m.DeviceName) && m.DeviceName == monitorId));

        if (monitor == null) return false;

        WallpaperManager.SetPosition(_settings.Scale);

        var monConfig = _settings.Monitors.FirstOrDefault(m => 
            (!string.IsNullOrEmpty(m.MonitorId) && m.MonitorId == monitor.MonitorId) ||
            (!string.IsNullOrEmpty(m.DeviceName) && m.DeviceName == monitor.DeviceName));

        var sourceConfig = (monConfig?.Source != null && !string.Equals(monConfig.Source.Type, "UseGlobal", StringComparison.OrdinalIgnoreCase))
            ? monConfig.Source
            : _settings.GlobalSource;

        var provider = ResolveProvider(sourceConfig);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var usedPaths = new HashSet<string>(CurrentWallpapers.Values.Where(p => !string.IsNullOrEmpty(p)), StringComparer.OrdinalIgnoreCase);

        string? imgPath = null;
        for (int retry = 0; retry < 3; retry++)
        {
            var candidate = await provider.GetNextImagePathAsync(cts.Token);
            if (!string.IsNullOrEmpty(candidate))
            {
                imgPath = candidate;
                if (!usedPaths.Contains(candidate))
                {
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(imgPath))
        {
            var meta = WallpaperMetadataManager.Instance.GetMetadata(imgPath);
            string applied = imgPath;

            if (_settings.ShowWallpaperInfoOnDesktop && meta != null)
            {
                applied = WallpaperWatermarkService.CreateWatermarkedWallpaper(imgPath, meta);
            }

            bool ok = WallpaperManager.SetWallpaper(monitor.MonitorId, applied);
            if (ok)
            {
                CurrentWallpapers[monitor.MonitorId] = imgPath;
                History.Push(imgPath);
                OnWallpaperChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public async Task ChangeWallpaperForMonitorAsync(uint monitorIndex)
    {
        var monitors = WallpaperManager.GetMonitors();
        if (monitorIndex < monitors.Count)
        {
            await ChangeWallpaperForMonitorAsync(monitors[(int)monitorIndex].MonitorId);
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
