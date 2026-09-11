using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using BackgroundSwitch.Common;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using Microsoft.Win32;

namespace BackgroundSwitch.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Scheduler _scheduler;
    private readonly Action _requestCloseOrHide;

    // UI state
    private WallpaperMode _mode;
    private WallpaperScale _scale;
    private string _sourceType = "BingDaily";
    private string _redditSubreddit = "wallpapers";
    private string _wallhavenQuery = "nature";
    private string _wallhavenApiKey = string.Empty;
    private string _localFolderPath = string.Empty;
    private string _pexelsApiKey = ProviderConfig.DefaultPexelsApiKey;
    private string _pexelsQuery = "nature";
    private string _unsplashQuery = "landscape";
    private string _unsplashApiKey = string.Empty;
    private int _intervalMinutes = 15;
    private bool _autoStart = true;
    private string _language = "bilingual";
    private int _blacklistCount;
    private bool _isChangingWallpaper;
    private string _providerDescription = string.Empty;
    private string _changeNowButtonText = "🔄 Đổi hình nền ngay";

    public ObservableCollection<MonitorInfoItem> Monitors { get; } = [];

    // Commands
    public ICommand BrowseFolderCommand { get; }
    public ICommand ClearBlacklistCommand { get; }
    public ICommand ChangeNowCommand { get; }
    public ICommand SaveCommand { get; }

    public MainViewModel(AppSettings settings, Scheduler scheduler, Action requestCloseOrHide)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _requestCloseOrHide = requestCloseOrHide ?? throw new ArgumentNullException(nameof(requestCloseOrHide));

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ClearBlacklistCommand = new RelayCommand(ClearBlacklist);
        ChangeNowCommand = new AsyncRelayCommand(ChangeWallpaperNowAsync, () => !IsChangingWallpaper);
        SaveCommand = new RelayCommand(SaveSettings);

        LoadFromSettings();
    }

    #region Observable Properties

    public WallpaperMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsModeSynced));
                OnPropertyChanged(nameof(IsModePerMonitor));
                OnPropertyChanged(nameof(IsModeSpan));
                OnPropertyChanged(nameof(IsPerMonitorVisible));
            }
        }
    }

    public bool IsModeSynced
    {
        get => Mode == WallpaperMode.Synced;
        set { if (value) Mode = WallpaperMode.Synced; }
    }

    public bool IsModePerMonitor
    {
        get => Mode == WallpaperMode.PerMonitor;
        set { if (value) Mode = WallpaperMode.PerMonitor; }
    }

    public bool IsModeSpan
    {
        get => Mode == WallpaperMode.Span;
        set { if (value) Mode = WallpaperMode.Span; }
    }

    public bool IsPerMonitorVisible => Mode == WallpaperMode.PerMonitor;

    public WallpaperScale Scale
    {
        get => _scale;
        set => SetProperty(ref _scale, value);
    }

    public string SourceType
    {
        get => _sourceType;
        set
        {
            if (SetProperty(ref _sourceType, value))
            {
                OnPropertyChanged(nameof(IsSourceBing));
                OnPropertyChanged(nameof(IsSourceReddit));
                OnPropertyChanged(nameof(IsSourceWallhaven));
                OnPropertyChanged(nameof(IsSourceNasa));
                OnPropertyChanged(nameof(IsSourceLocal));
                OnPropertyChanged(nameof(IsSourcePexels));
                OnPropertyChanged(nameof(IsSourceUnsplash));

                OnPropertyChanged(nameof(IsRedditPanelVisible));
                OnPropertyChanged(nameof(IsWallhavenPanelVisible));
                OnPropertyChanged(nameof(IsLocalPanelVisible));
                OnPropertyChanged(nameof(IsPexelsPanelVisible));
                OnPropertyChanged(nameof(IsUnsplashPanelVisible));

                UpdateProviderDescription();
            }
        }
    }

    public bool IsSourceBing
    {
        get => string.Equals(SourceType, "BingDaily", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "BingDaily"; }
    }

    public bool IsSourceReddit
    {
        get => string.Equals(SourceType, "Reddit", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Reddit"; }
    }

    public bool IsSourceWallhaven
    {
        get => string.Equals(SourceType, "Wallhaven", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Wallhaven"; }
    }

    public bool IsSourceNasa
    {
        get => string.Equals(SourceType, "Nasa", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Nasa"; }
    }

    public bool IsSourceLocal
    {
        get => string.Equals(SourceType, "Local", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Local"; }
    }

    public bool IsSourcePexels
    {
        get => string.Equals(SourceType, "Pexels", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Pexels"; }
    }

    public bool IsSourceUnsplash
    {
        get => string.Equals(SourceType, "Unsplash", StringComparison.OrdinalIgnoreCase);
        set { if (value) SourceType = "Unsplash"; }
    }

    public bool IsRedditPanelVisible => IsSourceReddit;
    public bool IsWallhavenPanelVisible => IsSourceWallhaven;
    public bool IsLocalPanelVisible => IsSourceLocal;
    public bool IsPexelsPanelVisible => IsSourcePexels;
    public bool IsUnsplashPanelVisible => IsSourceUnsplash;

    public string RedditSubreddit
    {
        get => _redditSubreddit;
        set => SetProperty(ref _redditSubreddit, value);
    }

    public string WallhavenQuery
    {
        get => _wallhavenQuery;
        set => SetProperty(ref _wallhavenQuery, value);
    }

    public string WallhavenApiKey
    {
        get => _wallhavenApiKey;
        set => SetProperty(ref _wallhavenApiKey, value);
    }

    public string LocalFolderPath
    {
        get => _localFolderPath;
        set => SetProperty(ref _localFolderPath, value);
    }

    public string PexelsApiKey
    {
        get => _pexelsApiKey;
        set => SetProperty(ref _pexelsApiKey, value);
    }

    public string PexelsQuery
    {
        get => _pexelsQuery;
        set => SetProperty(ref _pexelsQuery, value);
    }

    public string UnsplashQuery
    {
        get => _unsplashQuery;
        set => SetProperty(ref _unsplashQuery, value);
    }

    public string UnsplashApiKey
    {
        get => _unsplashApiKey;
        set => SetProperty(ref _unsplashApiKey, value);
    }

    public int IntervalMinutes
    {
        get => _intervalMinutes;
        set => SetProperty(ref _intervalMinutes, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                App.UpdateAppLanguage(value);
                RefreshLocalizationProperties();
            }
        }
    }

    public int BlacklistCount
    {
        get => _blacklistCount;
        set
        {
            if (SetProperty(ref _blacklistCount, value))
            {
                OnPropertyChanged(nameof(BlacklistCountText));
            }
        }
    }

    public string BlacklistCountText => $"{LocalizationManager.Get("UI_BlacklistInfo")} {BlacklistCount}";

    public string ProviderDescription
    {
        get => _providerDescription;
        set => SetProperty(ref _providerDescription, value);
    }

    public bool IsChangingWallpaper
    {
        get => _isChangingWallpaper;
        set
        {
            if (SetProperty(ref _isChangingWallpaper, value))
            {
                ChangeNowButtonText = value ? "⏳ Đang đổi..." : LocalizationManager.Get("UI_ChangeNowBtn");
            }
        }
    }

    public string ChangeNowButtonText
    {
        get => _changeNowButtonText;
        set => SetProperty(ref _changeNowButtonText, value);
    }

    #endregion

    #region Localized Strings for UI Binding

    public string AppTitleText => LocalizationManager.Get("UI_Title");
    public string AppSubtitleText => LocalizationManager.Get("UI_Subtitle");
    public string LanguageLabelText => $"🌐 {LocalizationManager.Get("UI_LanguageLabel")}";
    public string ModeSectionText => LocalizationManager.Get("UI_ModeSection");
    public string ModeSyncedText => LocalizationManager.Get("UI_ModeSynced");
    public string ModePerMonitorText => LocalizationManager.Get("UI_ModePerMonitor");
    public string ModeSpanText => LocalizationManager.Get("UI_ModeSpan");
    public string ScaleLabelText => LocalizationManager.Get("UI_ScaleLabel");

    public string SourceSectionText => LocalizationManager.Get("UI_SourceSection");
    public string SourceBingText => LocalizationManager.Get("UI_SourceBing");
    public string SourceRedditText => LocalizationManager.Get("UI_SourceReddit");
    public string SourceWallhavenText => LocalizationManager.Get("UI_SourceWallhaven");
    public string SourceNasaText => LocalizationManager.Get("UI_SourceNasa");
    public string SourceLocalText => LocalizationManager.Get("UI_SourceLocal");
    public string SourcePexelsText => LocalizationManager.Get("UI_SourcePexels");
    public string SourceUnsplashText => LocalizationManager.Get("UI_SourceUnsplash");

    public string RedditSubLabelText => LocalizationManager.Get("UI_RedditSub");
    public string WallhavenQueryLabelText => LocalizationManager.Get("UI_WallhavenQuery");
    public string WallhavenApiKeyLabelText => LocalizationManager.Get("UI_WallhavenApiKey");
    public string LocalFolderPathLabelText => LocalizationManager.Get("UI_LocalFolderPath");
    public string BrowseBtnText => LocalizationManager.Get("UI_BrowseBtn");
    public string PexelsApiKeyLabelText => LocalizationManager.Get("UI_PexelsApiKey");
    public string PexelsQueryLabelText => LocalizationManager.Get("UI_PexelsQuery");
    public string UnsplashQueryLabelText => LocalizationManager.Get("UI_UnsplashQuery");
    public string UnsplashApiKeyLabelText => LocalizationManager.Get("UI_UnsplashApiKey");

    public string PerMonitorSectionText => LocalizationManager.Get("UI_PerMonitorSection");
    public string GeneralSectionText => LocalizationManager.Get("UI_GeneralSection");
    public string IntervalLabelText => LocalizationManager.Get("UI_IntervalLabel");
    public string MinutesUnitText => LocalizationManager.Get("UI_MinutesUnit");
    public string AutoStartText => LocalizationManager.Get("UI_AutoStart");
    public string ClearBlacklistBtnText => LocalizationManager.Get("UI_ClearBlacklistBtn");
    public string SaveBtnText => LocalizationManager.Get("UI_SaveBtn");

    public void RefreshLocalizationProperties()
    {
        OnPropertyChanged(nameof(AppTitleText));
        OnPropertyChanged(nameof(AppSubtitleText));
        OnPropertyChanged(nameof(LanguageLabelText));
        OnPropertyChanged(nameof(ModeSectionText));
        OnPropertyChanged(nameof(ModeSyncedText));
        OnPropertyChanged(nameof(ModePerMonitorText));
        OnPropertyChanged(nameof(ModeSpanText));
        OnPropertyChanged(nameof(ScaleLabelText));
        OnPropertyChanged(nameof(SourceSectionText));
        OnPropertyChanged(nameof(SourceBingText));
        OnPropertyChanged(nameof(SourceRedditText));
        OnPropertyChanged(nameof(SourceWallhavenText));
        OnPropertyChanged(nameof(SourceNasaText));
        OnPropertyChanged(nameof(SourceLocalText));
        OnPropertyChanged(nameof(SourcePexelsText));
        OnPropertyChanged(nameof(SourceUnsplashText));
        OnPropertyChanged(nameof(RedditSubLabelText));
        OnPropertyChanged(nameof(WallhavenQueryLabelText));
        OnPropertyChanged(nameof(WallhavenApiKeyLabelText));
        OnPropertyChanged(nameof(LocalFolderPathLabelText));
        OnPropertyChanged(nameof(BrowseBtnText));
        OnPropertyChanged(nameof(PexelsApiKeyLabelText));
        OnPropertyChanged(nameof(PexelsQueryLabelText));
        OnPropertyChanged(nameof(UnsplashQueryLabelText));
        OnPropertyChanged(nameof(UnsplashApiKeyLabelText));
        OnPropertyChanged(nameof(PerMonitorSectionText));
        OnPropertyChanged(nameof(GeneralSectionText));
        OnPropertyChanged(nameof(IntervalLabelText));
        OnPropertyChanged(nameof(MinutesUnitText));
        OnPropertyChanged(nameof(AutoStartText));
        OnPropertyChanged(nameof(ClearBlacklistBtnText));
        OnPropertyChanged(nameof(SaveBtnText));
        OnPropertyChanged(nameof(BlacklistCountText));

        if (!IsChangingWallpaper)
        {
            ChangeNowButtonText = LocalizationManager.Get("UI_ChangeNowBtn");
        }

        UpdateProviderDescription();
    }

    #endregion

    private void UpdateProviderDescription()
    {
        ProviderDescription = SourceType.ToLowerInvariant() switch
        {
            "reddit" => LocalizationManager.Get("UI_RedditDesc"),
            "wallhaven" => LocalizationManager.Get("UI_WallhavenDesc"),
            "nasa" or "apod" => LocalizationManager.Get("UI_NasaDesc"),
            "local" => LocalizationManager.Get("UI_LocalDesc"),
            "pexels" => LocalizationManager.Get("UI_PexelsDesc"),
            "unsplash" => LocalizationManager.Get("UI_UnsplashDesc"),
            _ => LocalizationManager.Get("UI_BingDesc")
        };
    }

    private void LoadFromSettings()
    {
        Language = string.IsNullOrWhiteSpace(_settings.Language) ? "bilingual" : _settings.Language;
        Mode = _settings.Mode;
        Scale = _settings.Scale;
        IntervalMinutes = Math.Max(1, _settings.IntervalMinutes);
        AutoStart = _settings.AutoStart;

        var source = _settings.GlobalSource;
        SourceType = string.IsNullOrWhiteSpace(source.Type) ? "BingDaily" : source.Type;
        RedditSubreddit = string.IsNullOrWhiteSpace(source.RedditSubreddit) ? "wallpapers" : source.RedditSubreddit;
        WallhavenQuery = string.IsNullOrWhiteSpace(source.WallhavenQuery) ? "nature" : source.WallhavenQuery;
        WallhavenApiKey = source.WallhavenApiKey ?? string.Empty;
        LocalFolderPath = source.LocalFolderPath ?? string.Empty;
        PexelsApiKey = string.IsNullOrWhiteSpace(source.PexelsApiKey) ? ProviderConfig.DefaultPexelsApiKey : source.PexelsApiKey;
        PexelsQuery = string.IsNullOrWhiteSpace(source.PexelsQuery) ? "nature" : source.PexelsQuery;
        UnsplashQuery = string.IsNullOrWhiteSpace(source.UnsplashQuery) ? "landscape" : source.UnsplashQuery;
        UnsplashApiKey = source.UnsplashApiKey ?? string.Empty;

        // Load Monitors
        Monitors.Clear();
        foreach (var m in WallpaperManager.GetMonitors())
        {
            Monitors.Add(m);
        }

        BlacklistCount = BlacklistManager.Instance.Count;
        RefreshLocalizationProperties();
    }

    private void SaveToSettings()
    {
        _settings.Language = Language;
        _settings.Mode = Mode;
        _settings.Scale = Scale;
        _settings.IntervalMinutes = Math.Max(1, IntervalMinutes);
        _settings.AutoStart = AutoStart;

        _settings.GlobalSource.Type = SourceType;
        _settings.GlobalSource.RedditSubreddit = string.IsNullOrWhiteSpace(RedditSubreddit) ? "wallpapers" : RedditSubreddit.Trim();
        _settings.GlobalSource.WallhavenQuery = string.IsNullOrWhiteSpace(WallhavenQuery) ? "nature" : WallhavenQuery.Trim();
        _settings.GlobalSource.WallhavenApiKey = WallhavenApiKey.Trim();
        _settings.GlobalSource.LocalFolderPath = LocalFolderPath.Trim();
        _settings.GlobalSource.PexelsApiKey = string.IsNullOrWhiteSpace(PexelsApiKey) ? ProviderConfig.DefaultPexelsApiKey : PexelsApiKey.Trim();
        _settings.GlobalSource.PexelsQuery = string.IsNullOrWhiteSpace(PexelsQuery) ? "nature" : PexelsQuery.Trim();
        _settings.GlobalSource.UnsplashQuery = string.IsNullOrWhiteSpace(UnsplashQuery) ? "landscape" : UnsplashQuery.Trim();
        _settings.GlobalSource.UnsplashApiKey = UnsplashApiKey.Trim();

        var currentMonitors = WallpaperManager.GetMonitors();
        _settings.Monitors = currentMonitors.Select(m => new MonitorConfig
        {
            MonitorId = m.MonitorId,
            DeviceName = m.DeviceName,
            FriendlyName = m.FriendlyName,
            Width = m.Width,
            Height = m.Height,
            Source = _settings.GlobalSource
        }).ToList();
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LocalFolderPath = dialog.SelectedPath;
        }
    }

    private void ClearBlacklist()
    {
        var confirmMsg = LocalizationManager.Get("Msg_ClearBlacklistConfirm");
        var result = System.Windows.MessageBox.Show(confirmMsg, "BackgroundSwitch", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            BlacklistManager.Instance.Clear();
            BlacklistCount = BlacklistManager.Instance.Count;
            System.Windows.MessageBox.Show(LocalizationManager.Get("Msg_ClearBlacklistDone"), "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task ChangeWallpaperNowAsync()
    {
        try
        {
            IsChangingWallpaper = true;
            SaveToSettings();
            _scheduler.UpdateSettings(_settings);

            bool success = await _scheduler.ChangeWallpaperAsync();
            if (!success)
            {
                System.Windows.MessageBox.Show(
                    "Không thể đổi hình nền từ nguồn đã chọn.\n- Nếu dùng Local Folder: Hãy chọn thư mục có chứa ảnh (.jpg, .png).\n- Nếu dùng Online (Bing/Reddit/Wallhaven/Pexels): Hãy kiểm tra kết nối mạng internet.",
                    "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsChangingWallpaper = false;
        }
    }

    private void SaveSettings()
    {
        if (IntervalMinutes <= 0)
        {
            System.Windows.MessageBox.Show("Interval must be a valid positive number (minutes).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveToSettings();
        ManageAutoStart(_settings.AutoStart);

        _settings.Save();
        _scheduler.UpdateSettings(_settings);
        _scheduler.Start();

        System.Windows.MessageBox.Show(LocalizationManager.Get("Msg_SaveSuccess"), "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
        _requestCloseOrHide();
    }

    private static void ManageAutoStart(bool enable)
    {
        try
        {
            using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (rk == null) return;

            const string appName = "BackgroundSwitch";
            var processPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(processPath)) return;

            if (enable)
            {
                rk.SetValue(appName, $"\"{processPath}\" --hidden");
            }
            else
            {
                rk.DeleteValue(appName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoStart] Error configuring registry: {ex.Message}");
        }
    }
}
