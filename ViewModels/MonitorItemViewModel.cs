using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using ZeroWall.Common;
using ZeroWall.Models;
using ZeroWall.Services;

namespace ZeroWall.ViewModels;

public class MonitorItemViewModel : ViewModelBase
{
    private readonly Scheduler _scheduler;
    private readonly AppSettings _appSettings;
    private readonly Action<string, string> _showToast;

    private string _currentWallpaperPath = string.Empty;
    private string _selectedSourceType = "UseGlobal";
    private string _customQuery = string.Empty;
    private string _customFolderPath = string.Empty;
    private bool _isChanging;

    public MonitorInfoItem Info { get; }

    public string MonitorId => Info.MonitorId;
    public string DeviceName => Info.DeviceName;
    public string FriendlyName => string.IsNullOrWhiteSpace(Info.FriendlyName) ? Info.DeviceName : Info.FriendlyName;
    public int Width => Info.Width;
    public int Height => Info.Height;
    public int Left => Info.Left;
    public int Top => Info.Top;
    public bool IsPrimary => Info.IsPrimary;
    public uint DisplayIndex => Info.Index + 1;

    public string ResolutionText => $"{Width} × {Height}";
    public string DisplayTitle => $"Màn hình {DisplayIndex}: {FriendlyName}";

    public string CurrentWallpaperPath
    {
        get => _currentWallpaperPath;
        set
        {
            if (SetProperty(ref _currentWallpaperPath, value))
            {
                OnPropertyChanged(nameof(HasWallpaper));
                OnPropertyChanged(nameof(WallpaperFileName));
            }
        }
    }

    public bool HasWallpaper => !string.IsNullOrWhiteSpace(CurrentWallpaperPath) && File.Exists(CurrentWallpaperPath);
    public string WallpaperFileName => HasWallpaper ? Path.GetFileName(CurrentWallpaperPath) : LocalizationManager.Get("UI_NoWallpaperAssigned");

    public string SelectedSourceType
    {
        get => _selectedSourceType;
        set
        {
            if (SetProperty(ref _selectedSourceType, value))
            {
                OnPropertyChanged(nameof(IsLocalSource));
                OnPropertyChanged(nameof(IsQuerySource));
                OnPropertyChanged(nameof(IsCustomSource));
            }
        }
    }

    public bool IsLocalSource => string.Equals(SelectedSourceType, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsQuerySource => string.Equals(SelectedSourceType, "Pexels", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(SelectedSourceType, "Wallhaven", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(SelectedSourceType, "Unsplash", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(SelectedSourceType, "Reddit", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomSource => !string.Equals(SelectedSourceType, "UseGlobal", StringComparison.OrdinalIgnoreCase);

    public string CustomQuery
    {
        get => _customQuery;
        set => SetProperty(ref _customQuery, value);
    }

    public string CustomFolderPath
    {
        get => _customFolderPath;
        set => SetProperty(ref _customFolderPath, value);
    }

    public bool IsChanging
    {
        get => _isChanging;
        set => SetProperty(ref _isChanging, value);
    }

    // Localization strings
    public string SourceLabelText => LocalizationManager.Get("UI_MonitorSourceLabel");
    public string ChangeThisBtnText => LocalizationManager.Get("UI_ChangeThisMonitorBtn");
    public string PrimaryBadgeText => LocalizationManager.Get("UI_PrimaryBadge");
    public string KeywordsLabelText => LocalizationManager.Get("UI_MonitorKeywordsLabel");
    public string FolderLabelText => LocalizationManager.Get("UI_MonitorFolderLabel");
    public string CurrentLabelText => LocalizationManager.Get("UI_CurrentWallpaperLabel");
    public string BrowseBtnText => LocalizationManager.Get("UI_BrowseBtn");
    public string SourceUseGlobalText => LocalizationManager.Get("UI_SourceUseGlobal");

    public ICommand ChangeWallpaperCommand { get; }
    public ICommand BrowseFolderCommand { get; }

    public MonitorItemViewModel(
        MonitorInfoItem info, 
        MonitorConfig? savedConfig, 
        Scheduler scheduler, 
        AppSettings appSettings,
        Action<string, string> showToast)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));

        // Initialize current wallpaper
        if (!string.IsNullOrWhiteSpace(info.CurrentWallpaper))
        {
            _currentWallpaperPath = info.CurrentWallpaper;
        }
        else if (_scheduler.CurrentWallpapers.TryGetValue(info.MonitorId, out var cur) && !string.IsNullOrEmpty(cur))
        {
            _currentWallpaperPath = cur;
        }

        // Initialize configuration from savedConfig
        if (savedConfig?.Source != null && !string.IsNullOrWhiteSpace(savedConfig.Source.Type))
        {
            _selectedSourceType = savedConfig.Source.Type;
            _customFolderPath = savedConfig.Source.LocalFolderPath ?? string.Empty;
            _customQuery = !string.IsNullOrWhiteSpace(savedConfig.Source.PexelsQuery) ? savedConfig.Source.PexelsQuery :
                           !string.IsNullOrWhiteSpace(savedConfig.Source.WallhavenQuery) ? savedConfig.Source.WallhavenQuery :
                           !string.IsNullOrWhiteSpace(savedConfig.Source.UnsplashQuery) ? savedConfig.Source.UnsplashQuery :
                           savedConfig.Source.RedditSubreddit ?? string.Empty;
        }
        else
        {
            _selectedSourceType = "UseGlobal";
            _customQuery = "nature";
        }

        ChangeWallpaperCommand = new AsyncRelayCommand(ChangeWallpaperAsync, () => !IsChanging);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            CustomFolderPath = dialog.SelectedPath;
        }
    }

    private async Task ChangeWallpaperAsync()
    {
        if (IsChanging) return;

        IsChanging = true;
        try
        {
            // Update settings before fetching so scheduler uses the latest monitor config
            SaveToSettingsConfig();

            bool ok = await _scheduler.ChangeWallpaperForMonitorAsync(MonitorId);
            if (ok)
            {
                if (_scheduler.CurrentWallpapers.TryGetValue(MonitorId, out var newPath))
                {
                    CurrentWallpaperPath = newPath;
                }
                _showToast($"Đã đổi ảnh màn hình {DisplayIndex} ({FriendlyName}) thành công!", "Success");
            }
            else
            {
                _showToast($"Không thể đổi ảnh màn hình {DisplayIndex}. Vui lòng thử lại!", "Warning");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MonitorItemViewModel] Error changing wallpaper: {ex.Message}");
            _showToast($"Lỗi: {ex.Message}", "Error");
        }
        finally
        {
            IsChanging = false;
        }
    }

    public void SaveToSettingsConfig()
    {
        var existing = _appSettings.Monitors.FirstOrDefault(m => 
            (!string.IsNullOrEmpty(m.MonitorId) && m.MonitorId == MonitorId) ||
            (!string.IsNullOrEmpty(m.DeviceName) && m.DeviceName == DeviceName));

        var targetConfig = existing;
        if (targetConfig == null)
        {
            targetConfig = new MonitorConfig
            {
                MonitorId = MonitorId,
                DeviceName = DeviceName,
                FriendlyName = FriendlyName,
                Width = Width,
                Height = Height
            };
            _appSettings.Monitors.Add(targetConfig);
        }

        if (string.Equals(SelectedSourceType, "UseGlobal", StringComparison.OrdinalIgnoreCase))
        {
            targetConfig.Source = new ProviderConfig { Type = "UseGlobal" };
        }
        else
        {
            targetConfig.Source = new ProviderConfig
            {
                Type = SelectedSourceType,
                LocalFolderPath = CustomFolderPath.Trim(),
                PexelsApiKey = _appSettings.GlobalSource.PexelsApiKey,
                PexelsQuery = string.IsNullOrWhiteSpace(CustomQuery) ? "nature" : CustomQuery.Trim(),
                WallhavenQuery = string.IsNullOrWhiteSpace(CustomQuery) ? "nature" : CustomQuery.Trim(),
                WallhavenApiKey = _appSettings.GlobalSource.WallhavenApiKey,
                RedditSubreddit = string.IsNullOrWhiteSpace(CustomQuery) ? "wallpapers" : CustomQuery.Trim(),
                UnsplashQuery = string.IsNullOrWhiteSpace(CustomQuery) ? "landscape" : CustomQuery.Trim(),
                UnsplashApiKey = _appSettings.GlobalSource.UnsplashApiKey,
                TopicMode = _appSettings.GlobalSource.TopicMode
            };
        }
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(SourceLabelText));
        OnPropertyChanged(nameof(ChangeThisBtnText));
        OnPropertyChanged(nameof(PrimaryBadgeText));
        OnPropertyChanged(nameof(KeywordsLabelText));
        OnPropertyChanged(nameof(FolderLabelText));
        OnPropertyChanged(nameof(CurrentLabelText));
        OnPropertyChanged(nameof(BrowseBtnText));
        OnPropertyChanged(nameof(SourceUseGlobalText));
        OnPropertyChanged(nameof(WallpaperFileName));
    }
}
