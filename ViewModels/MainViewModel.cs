using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ZeroWall.Common;
using ZeroWall.Models;
using ZeroWall.Providers;
using ZeroWall.Services;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace ZeroWall.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Scheduler _scheduler;
    private readonly Action _requestCloseOrHide;

    // Navigation Tabs
    private int _selectedTabIndex = 0; // 0 = Sources, 1 = Displays, 2 = Settings

    // Settings state
    private WallpaperMode _mode;
    private WallpaperScale _scale;
    private string _sourceType = "Pexels";
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

    // Live preview & status
    private string? _currentWallpaperPath;
    private bool _isChangingWallpaper;
    private string _providerDescription = string.Empty;
    private string _changeNowButtonText = "🔄 Đổi hình nền ngay";

    // In-Window Toast / InfoBar
    private string _statusMessage = string.Empty;
    private string _statusSeverity = "Info"; // "Success", "Info", "Warning", "Error"
    private bool _isStatusVisible;

    // Countdown timer
    private readonly System.Windows.Threading.DispatcherTimer _countdownTimer;
    private string _nextSwitchCountdownText = string.Empty;
    public string NextSwitchCountdownText
    {
        get => _nextSwitchCountdownText;
        set => SetProperty(ref _nextSwitchCountdownText, value);
    }

    public ObservableCollection<MonitorItemViewModel> Monitors { get; } = [];
    public ObservableCollection<int> CacheLimitOptions { get; } = [5, 10, 20, 50];

    public string PerMonitorActiveBannerText => LocalizationManager.Get("UI_PerMonitorActiveBanner");
    public string SyncedActiveBannerText => LocalizationManager.Get("UI_SyncedActiveBanner");

    // Commands
    public ICommand SelectTabCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand ClearBlacklistCommand { get; }
    public ICommand ChangeNowCommand { get; }
    public ICommand TestSampleCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SelectPexelsTagCommand { get; }
    public ICommand SelectRedditTagCommand { get; }
    public ICommand SelectWallhavenTagCommand { get; }
    public ICommand ClearCurrentTopicsCommand { get; }
    public ICommand SelectIntervalPresetCommand { get; }
    public ICommand SaveCurrentPictureAsCommand { get; }
    public ICommand OpenCurrentInExplorerCommand { get; }
    public ICommand AddToFavoritesCommand { get; }
    public ICommand ClearCacheNowCommand { get; }
    public ICommand OpenFavoritesFolderCommand { get; }
    public ICommand BlacklistCurrentWallpaperCommand { get; }
    public ICommand DismissStatusCommand { get; }

    public MainViewModel(AppSettings settings, Scheduler scheduler, Action requestCloseOrHide)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _requestCloseOrHide = requestCloseOrHide ?? throw new ArgumentNullException(nameof(requestCloseOrHide));

        SelectTabCommand = new RelayCommand(p =>
        {
            if (p != null && int.TryParse(p.ToString(), out int tab))
            {
                SelectedTabIndex = tab;
            }
        });

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ClearBlacklistCommand = new RelayCommand(ClearBlacklist);
        ChangeNowCommand = new AsyncRelayCommand(ChangeWallpaperNowAsync, () => !IsChangingWallpaper);
        SaveCommand = new RelayCommand(SaveSettings);

        SelectPexelsTagCommand = new RelayCommand(p =>
        {
            if (p is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                PexelsQuery = TopicResolver.ToggleTopic(PexelsQuery, tag);
                ShowToast($"Chủ đề Pexels: {PexelsQuery}", "Info");
            }
        });

        SelectRedditTagCommand = new RelayCommand(p =>
        {
            if (p is string sub && !string.IsNullOrWhiteSpace(sub))
            {
                RedditSubreddit = TopicResolver.ToggleTopic(RedditSubreddit, sub);
                ShowToast($"Subreddit: {RedditSubreddit}", "Info");
            }
        });

        SelectWallhavenTagCommand = new RelayCommand(p =>
        {
            if (p is string q && !string.IsNullOrWhiteSpace(q))
            {
                WallhavenQuery = TopicResolver.ToggleTopic(WallhavenQuery, q);
                ShowToast($"Chủ đề Wallhaven: {WallhavenQuery}", "Info");
            }
        });

        ClearCurrentTopicsCommand = new RelayCommand(() =>
        {
            if (IsSourcePexels) PexelsQuery = string.Empty;
            else if (IsSourceWallhaven) WallhavenQuery = string.Empty;
            else if (IsSourceReddit) RedditSubreddit = string.Empty;
            else if (IsSourceUnsplash) UnsplashQuery = string.Empty;
            ShowToast("Đã xóa danh sách chủ đề.", "Info");
        });

        SelectIntervalPresetCommand = new RelayCommand(p =>
        {
            if (p != null && int.TryParse(p.ToString(), out int mins))
            {
                IntervalMinutes = mins;
                ShowToast($"Tần suất đổi ảnh: {mins} phút", "Info");
            }
        });

        SaveCurrentPictureAsCommand = new RelayCommand(SaveCurrentPictureAs);
        OpenCurrentInExplorerCommand = new RelayCommand(OpenCurrentInExplorer);
        AddToFavoritesCommand = new RelayCommand(AddToFavorites);
        ClearCacheNowCommand = new RelayCommand(ClearCacheNow);
        OpenFavoritesFolderCommand = new RelayCommand(OpenFavoritesFolder);
        BlacklistCurrentWallpaperCommand = new AsyncRelayCommand(BlacklistCurrentAsync);
        DismissStatusCommand = new RelayCommand(() => IsStatusVisible = false);
        TestSampleCommand = new AsyncRelayCommand(TestSampleAsync, () => !IsChangingWallpaper);

        // Setup 1-second countdown timer
        _countdownTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, e) => UpdateCountdownText();
        _countdownTimer.Start();
        UpdateCountdownText();

        // Listen to wallpaper changes from scheduler
        _scheduler.OnWallpaperChanged += OnWallpaperChangedCallback;

        LoadFromSettings();
    }

    private void OnWallpaperChangedCallback()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
        {
            CurrentWallpaperPath = _scheduler.GetCurrentActiveWallpaperPath();
            BlacklistCount = BlacklistManager.Instance.Count;
            RefreshCacheStats();

            foreach (var mon in Monitors)
            {
                if (_scheduler.CurrentWallpapers.TryGetValue(mon.MonitorId, out var p) && !string.IsNullOrEmpty(p))
                {
                    mon.CurrentWallpaperPath = p;
                }
                else if (Mode != WallpaperMode.PerMonitor && !string.IsNullOrEmpty(CurrentWallpaperPath))
                {
                    mon.CurrentWallpaperPath = CurrentWallpaperPath;
                }
            }
        }));
    }

    #region Navigation Tab Properties

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsTabSources));
                OnPropertyChanged(nameof(IsTabDisplays));
                OnPropertyChanged(nameof(IsTabSettings));
            }
        }
    }

    public bool IsTabSources => SelectedTabIndex == 0;
    public bool IsTabDisplays => SelectedTabIndex == 1;
    public bool IsTabSettings => SelectedTabIndex == 2;

    #endregion

    #region Live Wallpaper Preview Properties

    public string? CurrentWallpaperPath
    {
        get => _currentWallpaperPath;
        set
        {
            if (SetProperty(ref _currentWallpaperPath, value))
            {
                OnPropertyChanged(nameof(HasCurrentWallpaper));
                OnPropertyChanged(nameof(CurrentWallpaperFileName));
                OnPropertyChanged(nameof(CurrentWallpaperTitle));
                OnPropertyChanged(nameof(CurrentWallpaperAuthor));
                OnPropertyChanged(nameof(HasWallpaperAuthor));
                OnPropertyChanged(nameof(CurrentWallpaperAuthorDisplay));
                OnPropertyChanged(nameof(CurrentWallpaperDetails));
            }
        }
    }

    public bool HasCurrentWallpaper => !string.IsNullOrEmpty(CurrentWallpaperPath) && File.Exists(CurrentWallpaperPath);

    public string CurrentWallpaperFileName
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentWallpaperPath)) return "Chưa có hình nền";
            return Path.GetFileName(CurrentWallpaperPath);
        }
    }

    public string CurrentWallpaperTitle
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentWallpaperPath)) return "Chưa có hình nền";
            var meta = WallpaperMetadataManager.Instance.GetMetadata(CurrentWallpaperPath);
            if (meta != null && !string.IsNullOrWhiteSpace(meta.Title))
            {
                return meta.Title;
            }
            return Path.GetFileNameWithoutExtension(CurrentWallpaperPath);
        }
    }

    public string CurrentWallpaperAuthor
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentWallpaperPath)) return string.Empty;
            var meta = WallpaperMetadataManager.Instance.GetMetadata(CurrentWallpaperPath);
            if (meta != null && !string.IsNullOrWhiteSpace(meta.Author))
            {
                return meta.Author;
            }
            return string.Empty;
        }
    }

    public bool HasWallpaperAuthor => !string.IsNullOrEmpty(CurrentWallpaperAuthor);

    public string CurrentWallpaperAuthorDisplay => HasWallpaperAuthor ? $"📸 {CurrentWallpaperAuthor}" : string.Empty;

    public string CurrentWallpaperDetails
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentWallpaperPath) || !File.Exists(CurrentWallpaperPath))
            {
                return "Đang chờ đổi ảnh tự động...";
            }
            try
            {
                var fi = new FileInfo(CurrentWallpaperPath);
                double sizeKb = fi.Length / 1024.0;
                var meta = WallpaperMetadataManager.Instance.GetMetadata(CurrentWallpaperPath);
                string provider = !string.IsNullOrEmpty(meta?.Provider) ? meta.Provider : SourceType;
                return $"Nguồn: {provider} | Kích thước: {sizeKb:F1} KB | Cập nhật: {fi.LastWriteTime:HH:mm:ss}";
            }
            catch
            {
                return $"Nguồn: {SourceType}";
            }
        }
    }

    #endregion

    #region Status Toast / InfoBar Properties

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusSeverity
    {
        get => _statusSeverity;
        set => SetProperty(ref _statusSeverity, value);
    }

    public bool IsStatusVisible
    {
        get => _isStatusVisible;
        set => SetProperty(ref _isStatusVisible, value);
    }

    public void ShowToast(string message, string severity = "Success")
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusVisible = true;
    }

    #endregion

    #region Observable Settings Properties

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
        set
        {
            if (SetProperty(ref _scale, value))
            {
                _settings.Scale = value;
                WallpaperManager.SetPosition(value, refreshImmediately: true);
                ShowToast($"{LocalizationManager.Get("UI_ScaleLabel")} {GetScaleDisplayName(value)}", "Info");
            }
        }
    }

    private static string GetScaleDisplayName(WallpaperScale scale)
    {
        return scale switch
        {
            WallpaperScale.Fill => LocalizationManager.Get("UI_ScaleFill"),
            WallpaperScale.Fit => LocalizationManager.Get("UI_ScaleFit"),
            WallpaperScale.Stretch => LocalizationManager.Get("UI_ScaleStretch"),
            WallpaperScale.Center => LocalizationManager.Get("UI_ScaleCenter"),
            WallpaperScale.Tile => LocalizationManager.Get("UI_ScaleTile"),
            WallpaperScale.Span => LocalizationManager.Get("UI_ScaleSpan"),
            _ => scale.ToString()
        };
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

    private TopicSelectionMode _topicMode = TopicSelectionMode.Random;

    public TopicSelectionMode TopicMode
    {
        get => _topicMode;
        set
        {
            if (SetProperty(ref _topicMode, value))
            {
                OnPropertyChanged(nameof(IsTopicModeRandom));
                OnPropertyChanged(nameof(IsTopicModeSequential));
            }
        }
    }

    public bool IsTopicModeRandom
    {
        get => TopicMode == TopicSelectionMode.Random;
        set { if (value) TopicMode = TopicSelectionMode.Random; }
    }

    public bool IsTopicModeSequential
    {
        get => TopicMode == TopicSelectionMode.Sequential;
        set { if (value) TopicMode = TopicSelectionMode.Sequential; }
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
        set
        {
            if (SetProperty(ref _intervalMinutes, Math.Max(1, value)))
            {
                _settings.IntervalMinutes = _intervalMinutes;
                _scheduler.UpdateSettings(_settings);
                UpdateCountdownText();
            }
        }
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    private bool _showWallpaperInfoOnDesktop;
    public bool ShowWallpaperInfoOnDesktop
    {
        get => _showWallpaperInfoOnDesktop;
        set => SetProperty(ref _showWallpaperInfoOnDesktop, value);
    }

    private bool _showWallpaperInfoInApp = true;
    public bool ShowWallpaperInfoInApp
    {
        get => _showWallpaperInfoInApp;
        set => SetProperty(ref _showWallpaperInfoInApp, value);
    }

    private int _maxCachedImages = 5;
    public int MaxCachedImages
    {
        get => _maxCachedImages;
        set
        {
            if (SetProperty(ref _maxCachedImages, value))
            {
                BaseHttpImageProvider.MaxCachedCount = value;
            }
        }
    }

    private bool _clearCacheOnExit;
    public bool ClearCacheOnExit
    {
        get => _clearCacheOnExit;
        set => SetProperty(ref _clearCacheOnExit, value);
    }

    private string _cacheUsageText = "0.0 MB (0 ảnh)";
    public string CacheUsageText
    {
        get => _cacheUsageText;
        set => SetProperty(ref _cacheUsageText, value);
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
    public string TopicModeLabelText => LocalizationManager.Get("UI_TopicModeLabel");
    public string TopicModeRandomText => LocalizationManager.Get("UI_TopicModeRandom");
    public string TopicModeSequentialText => LocalizationManager.Get("UI_TopicModeSequential");
    public string TopicHelpText => LocalizationManager.Get("UI_TopicHelp");
    public string ClearTopicsBtnText => LocalizationManager.Get("UI_ClearTopicsBtn");

    public string PerMonitorSectionText => LocalizationManager.Get("UI_PerMonitorSection");
    public string GeneralSectionText => LocalizationManager.Get("UI_GeneralSection");
    public string IntervalLabelText => LocalizationManager.Get("UI_IntervalLabel");
    public string MinutesUnitText => LocalizationManager.Get("UI_MinutesUnit");
    public string AutoStartText => LocalizationManager.Get("UI_AutoStart");
    public string ShowWallpaperInfoOnDesktopText => LocalizationManager.Get("UI_ShowWallpaperInfoOnDesktop");
    public string ShowWallpaperInfoInAppText => LocalizationManager.Get("UI_ShowWallpaperInfoInApp");
    public string FavoriteBtnText => LocalizationManager.Get("UI_FavoriteBtn");
    public string CacheSectionText => LocalizationManager.Get("UI_CacheSection");
    public string MaxCachedImagesText => LocalizationManager.Get("UI_MaxCachedImages");
    public string ClearCacheOnExitText => LocalizationManager.Get("UI_ClearCacheOnExit");
    public string CurrentCacheUsageText => LocalizationManager.Get("UI_CurrentCacheUsage");
    public string ClearCacheNowBtnText => LocalizationManager.Get("UI_ClearCacheNowBtn");
    public string OpenFavoritesFolderBtnText => LocalizationManager.Get("UI_OpenFavoritesFolderBtn");
    public string ClearBlacklistBtnText => LocalizationManager.Get("UI_ClearBlacklistBtn");
    public string SaveBtnText => LocalizationManager.Get("UI_SaveBtn");

    // Navigation Tabs & Header
    public string NavSourcesText => LocalizationManager.Get("UI_NavSources");
    public string NavDisplaysText => LocalizationManager.Get("UI_NavDisplays");
    public string NavSettingsText => LocalizationManager.Get("UI_NavSettings");
    public string PexelsDefaultBadgeText => LocalizationManager.Get("UI_PexelsDefaultBadge");

    // Hero Preview Bar
    public string PreviewPlaceholderText => LocalizationManager.Get("UI_PreviewPlaceholder");
    public string SavePictureBtnText => LocalizationManager.Get("UI_SavePictureBtn");
    public string ViewFileBtnText => LocalizationManager.Get("UI_ViewFileBtn");
    public string BlacklistBtnText => LocalizationManager.Get("UI_BlacklistBtn");

    // Tab 0: Sources
    public string SelectSourceTitleText => LocalizationManager.Get("UI_SelectSourceTitle");
    public string CardTitlePexelsText => LocalizationManager.Get("UI_CardTitlePexels");
    public string CardTitleBingText => LocalizationManager.Get("UI_CardTitleBing");
    public string CardTitleWallhavenText => LocalizationManager.Get("UI_CardTitleWallhaven");
    public string CardTitleNasaText => LocalizationManager.Get("UI_CardTitleNasa");
    public string CardTitleUnsplashText => LocalizationManager.Get("UI_CardTitleUnsplash");
    public string CardTitleRedditText => LocalizationManager.Get("UI_CardTitleReddit");
    public string CardTitleLocalText => LocalizationManager.Get("UI_CardTitleLocal");
    public string BadgeUltraHdText => LocalizationManager.Get("UI_BadgeUltraHd");
    public string BadgeDailyText => LocalizationManager.Get("UI_BadgeDaily");
    public string BadgeArtText => LocalizationManager.Get("UI_BadgeArt");
    public string BadgeSpaceText => LocalizationManager.Get("UI_BadgeSpace");
    public string BadgeCommunityText => LocalizationManager.Get("UI_BadgeCommunity");
    public string BadgeOfflineText => LocalizationManager.Get("UI_BadgeOffline");

    public string CardPexelsDescText => LocalizationManager.Get("UI_CardPexelsDesc");
    public string CardBingDescText => LocalizationManager.Get("UI_CardBingDesc");
    public string CardWallhavenDescText => LocalizationManager.Get("UI_CardWallhavenDesc");
    public string CardNasaDescText => LocalizationManager.Get("UI_CardNasaDesc");
    public string CardUnsplashDescText => LocalizationManager.Get("UI_CardUnsplashDesc");
    public string CardRedditDescText => LocalizationManager.Get("UI_CardRedditDesc");
    public string CardLocalDescText => LocalizationManager.Get("UI_CardLocalDesc");

    public string ConfigPexelsTitleText => LocalizationManager.Get("UI_ConfigPexelsTitle");
    public string KeyValidText => LocalizationManager.Get("UI_KeyValid");
    public string QuickTopicHintText => LocalizationManager.Get("UI_QuickTopicHint");
    public string PexelsKeyHelpText => LocalizationManager.Get("UI_PexelsKeyHelp");
    public string ConfigWallhavenTitleText => LocalizationManager.Get("UI_ConfigWallhavenTitle");
    public string ConfigRedditTitleText => LocalizationManager.Get("UI_ConfigRedditTitle");
    public string ConfigLocalTitleText => LocalizationManager.Get("UI_ConfigLocalTitle");
    public string ConfigUnsplashTitleText => LocalizationManager.Get("UI_ConfigUnsplashTitle");
    public string BingNoConfigHintText => LocalizationManager.Get("UI_BingNoConfigHint");
    public string NasaNoConfigHintText => LocalizationManager.Get("UI_NasaNoConfigHint");

    // Tab 1: Displays & Scale
    public string ScaleFillText => LocalizationManager.Get("UI_ScaleFill");
    public string ScaleFitText => LocalizationManager.Get("UI_ScaleFit");
    public string ScaleStretchText => LocalizationManager.Get("UI_ScaleStretch");
    public string ScaleCenterText => LocalizationManager.Get("UI_ScaleCenter");
    public string ScaleTileText => LocalizationManager.Get("UI_ScaleTile");
    public string ScaleSpanText => LocalizationManager.Get("UI_ScaleSpan");
    public string DetectedMonitorsTitleText => LocalizationManager.Get("UI_DetectedMonitorsTitle");
    public string ConnectedStatusText => LocalizationManager.Get("UI_ConnectedStatus");

    // Tab 2: Settings & Presets
    public string IntervalSectionTitleText => LocalizationManager.Get("UI_IntervalSectionTitle");
    public string QuickIntervalTitleText => LocalizationManager.Get("UI_QuickIntervalTitle");
    public string Preset15MinText => LocalizationManager.Get("UI_Preset15Min");
    public string Preset30MinText => LocalizationManager.Get("UI_Preset30Min");
    public string Preset1HourText => LocalizationManager.Get("UI_Preset1Hour");
    public string Preset2HourText => LocalizationManager.Get("UI_Preset2Hour");
    public string Preset4HourText => LocalizationManager.Get("UI_Preset4Hour");
    public string Preset24HourText => LocalizationManager.Get("UI_Preset24Hour");
    public string MetadataSectionTitleText => LocalizationManager.Get("UI_MetadataSectionTitle");
    public string SpotlightHelpText => LocalizationManager.Get("UI_SpotlightHelpText");
    public string AppMetaHelpText => LocalizationManager.Get("UI_AppMetaHelpText");
    public string CacheHelpText => LocalizationManager.Get("UI_CacheHelpText");
    public string BlacklistSectionTitleText => LocalizationManager.Get("UI_BlacklistSectionTitle");
    public string BlacklistHelpText => LocalizationManager.Get("UI_BlacklistHelpText");

    // Bottom Sticky Bar
    public string TrayActiveHintText => LocalizationManager.Get("UI_TrayActiveHint");
    public string TrayActionHintText => LocalizationManager.Get("UI_TrayActionHint");

    // Dynamic enhancements
    public string TestSampleBtnText => LocalizationManager.Get("UI_TestSampleBtn");
    public string FrequencySliderLabelText => LocalizationManager.Get("UI_FrequencySliderLabel");
    public string NextCountdownText => LocalizationManager.Get("UI_NextCountdown");

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
        OnPropertyChanged(nameof(TopicModeLabelText));
        OnPropertyChanged(nameof(TopicModeRandomText));
        OnPropertyChanged(nameof(TopicModeSequentialText));
        OnPropertyChanged(nameof(TopicHelpText));
        OnPropertyChanged(nameof(ClearTopicsBtnText));
        OnPropertyChanged(nameof(PerMonitorSectionText));
        OnPropertyChanged(nameof(GeneralSectionText));
        OnPropertyChanged(nameof(IntervalLabelText));
        OnPropertyChanged(nameof(MinutesUnitText));
        OnPropertyChanged(nameof(AutoStartText));
        OnPropertyChanged(nameof(ShowWallpaperInfoOnDesktopText));
        OnPropertyChanged(nameof(ShowWallpaperInfoInAppText));
        OnPropertyChanged(nameof(FavoriteBtnText));
        OnPropertyChanged(nameof(CacheSectionText));
        OnPropertyChanged(nameof(MaxCachedImagesText));
        OnPropertyChanged(nameof(ClearCacheOnExitText));
        OnPropertyChanged(nameof(CurrentCacheUsageText));
        OnPropertyChanged(nameof(ClearCacheNowBtnText));
        OnPropertyChanged(nameof(OpenFavoritesFolderBtnText));
        OnPropertyChanged(nameof(ClearBlacklistBtnText));
        OnPropertyChanged(nameof(SaveBtnText));
        OnPropertyChanged(nameof(BlacklistCountText));
        OnPropertyChanged(nameof(CurrentWallpaperDetails));

        // New properties
        OnPropertyChanged(nameof(NavSourcesText));
        OnPropertyChanged(nameof(NavDisplaysText));
        OnPropertyChanged(nameof(NavSettingsText));
        OnPropertyChanged(nameof(PexelsDefaultBadgeText));
        OnPropertyChanged(nameof(PreviewPlaceholderText));
        OnPropertyChanged(nameof(SavePictureBtnText));
        OnPropertyChanged(nameof(ViewFileBtnText));
        OnPropertyChanged(nameof(BlacklistBtnText));
        OnPropertyChanged(nameof(SelectSourceTitleText));
        OnPropertyChanged(nameof(CardTitlePexelsText));
        OnPropertyChanged(nameof(CardTitleBingText));
        OnPropertyChanged(nameof(CardTitleWallhavenText));
        OnPropertyChanged(nameof(CardTitleNasaText));
        OnPropertyChanged(nameof(CardTitleUnsplashText));
        OnPropertyChanged(nameof(CardTitleRedditText));
        OnPropertyChanged(nameof(CardTitleLocalText));
        OnPropertyChanged(nameof(BadgeUltraHdText));
        OnPropertyChanged(nameof(BadgeDailyText));
        OnPropertyChanged(nameof(BadgeArtText));
        OnPropertyChanged(nameof(BadgeSpaceText));
        OnPropertyChanged(nameof(BadgeCommunityText));
        OnPropertyChanged(nameof(BadgeOfflineText));
        OnPropertyChanged(nameof(CardPexelsDescText));
        OnPropertyChanged(nameof(CardBingDescText));
        OnPropertyChanged(nameof(CardWallhavenDescText));
        OnPropertyChanged(nameof(CardNasaDescText));
        OnPropertyChanged(nameof(CardUnsplashDescText));
        OnPropertyChanged(nameof(CardRedditDescText));
        OnPropertyChanged(nameof(CardLocalDescText));
        OnPropertyChanged(nameof(ConfigPexelsTitleText));
        OnPropertyChanged(nameof(KeyValidText));
        OnPropertyChanged(nameof(QuickTopicHintText));
        OnPropertyChanged(nameof(PexelsKeyHelpText));
        OnPropertyChanged(nameof(ConfigWallhavenTitleText));
        OnPropertyChanged(nameof(ConfigRedditTitleText));
        OnPropertyChanged(nameof(ConfigLocalTitleText));
        OnPropertyChanged(nameof(ConfigUnsplashTitleText));
        OnPropertyChanged(nameof(BingNoConfigHintText));
        OnPropertyChanged(nameof(NasaNoConfigHintText));
        OnPropertyChanged(nameof(ScaleFillText));
        OnPropertyChanged(nameof(ScaleFitText));
        OnPropertyChanged(nameof(ScaleStretchText));
        OnPropertyChanged(nameof(ScaleCenterText));
        OnPropertyChanged(nameof(ScaleTileText));
        OnPropertyChanged(nameof(ScaleSpanText));
        OnPropertyChanged(nameof(DetectedMonitorsTitleText));
        OnPropertyChanged(nameof(ConnectedStatusText));
        OnPropertyChanged(nameof(IntervalSectionTitleText));
        OnPropertyChanged(nameof(QuickIntervalTitleText));
        OnPropertyChanged(nameof(Preset15MinText));
        OnPropertyChanged(nameof(Preset30MinText));
        OnPropertyChanged(nameof(Preset1HourText));
        OnPropertyChanged(nameof(Preset2HourText));
        OnPropertyChanged(nameof(Preset4HourText));
        OnPropertyChanged(nameof(Preset24HourText));
        OnPropertyChanged(nameof(MetadataSectionTitleText));
        OnPropertyChanged(nameof(SpotlightHelpText));
        OnPropertyChanged(nameof(AppMetaHelpText));
        OnPropertyChanged(nameof(CacheHelpText));
        OnPropertyChanged(nameof(BlacklistSectionTitleText));
        OnPropertyChanged(nameof(BlacklistHelpText));
        OnPropertyChanged(nameof(TrayActiveHintText));
        OnPropertyChanged(nameof(TrayActionHintText));
        OnPropertyChanged(nameof(PerMonitorActiveBannerText));
        OnPropertyChanged(nameof(SyncedActiveBannerText));
        OnPropertyChanged(nameof(TestSampleBtnText));
        OnPropertyChanged(nameof(FrequencySliderLabelText));
        OnPropertyChanged(nameof(NextCountdownText));
        UpdateCountdownText();

        foreach (var mon in Monitors)
        {
            mon.RefreshLocalization();
        }

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
        ShowWallpaperInfoOnDesktop = _settings.ShowWallpaperInfoOnDesktop;
        ShowWallpaperInfoInApp = _settings.ShowWallpaperInfoInApp;
        MaxCachedImages = _settings.MaxCachedImages;
        ClearCacheOnExit = _settings.ClearCacheOnExit;
        RefreshCacheStats();

        var source = _settings.GlobalSource;
        SourceType = string.IsNullOrWhiteSpace(source.Type) ? "Pexels" : source.Type;
        RedditSubreddit = string.IsNullOrWhiteSpace(source.RedditSubreddit) ? "wallpapers" : source.RedditSubreddit;
        WallhavenQuery = string.IsNullOrWhiteSpace(source.WallhavenQuery) ? "nature" : source.WallhavenQuery;
        WallhavenApiKey = source.WallhavenApiKey ?? string.Empty;
        LocalFolderPath = source.LocalFolderPath ?? string.Empty;
        PexelsApiKey = string.IsNullOrWhiteSpace(source.PexelsApiKey) ? ProviderConfig.DefaultPexelsApiKey : source.PexelsApiKey;
        PexelsQuery = string.IsNullOrWhiteSpace(source.PexelsQuery) ? "nature" : source.PexelsQuery;
        TopicMode = source.TopicMode;
        UnsplashQuery = string.IsNullOrWhiteSpace(source.UnsplashQuery) ? "landscape" : source.UnsplashQuery;
        UnsplashApiKey = source.UnsplashApiKey ?? string.Empty;

        // Load Monitors
        Monitors.Clear();
        var currentMonitors = WallpaperManager.GetMonitors();
        foreach (var m in currentMonitors)
        {
            var saved = _settings.Monitors.FirstOrDefault(x => 
                (!string.IsNullOrEmpty(x.MonitorId) && x.MonitorId == m.MonitorId) ||
                (!string.IsNullOrEmpty(x.DeviceName) && x.DeviceName == m.DeviceName));

            var vm = new MonitorItemViewModel(m, saved, _scheduler, _settings, ShowToast);
            Monitors.Add(vm);
        }

        BlacklistCount = BlacklistManager.Instance.Count;
        CurrentWallpaperPath = _scheduler.GetCurrentActiveWallpaperPath();

        RefreshLocalizationProperties();
    }

    private void SaveToSettings()
    {
        _settings.Language = Language;
        _settings.Mode = Mode;
        _settings.Scale = Scale;
        _settings.IntervalMinutes = Math.Max(1, IntervalMinutes);
        _settings.AutoStart = AutoStart;
        _settings.ShowWallpaperInfoOnDesktop = ShowWallpaperInfoOnDesktop;
        _settings.ShowWallpaperInfoInApp = ShowWallpaperInfoInApp;
        _settings.MaxCachedImages = MaxCachedImages;
        _settings.ClearCacheOnExit = ClearCacheOnExit;

        _settings.GlobalSource.Type = SourceType;
        _settings.GlobalSource.RedditSubreddit = string.IsNullOrWhiteSpace(RedditSubreddit) ? "wallpapers" : RedditSubreddit.Trim();
        _settings.GlobalSource.WallhavenQuery = string.IsNullOrWhiteSpace(WallhavenQuery) ? "nature" : WallhavenQuery.Trim();
        _settings.GlobalSource.WallhavenApiKey = WallhavenApiKey.Trim();
        _settings.GlobalSource.LocalFolderPath = LocalFolderPath.Trim();
        _settings.GlobalSource.PexelsApiKey = string.IsNullOrWhiteSpace(PexelsApiKey) ? ProviderConfig.DefaultPexelsApiKey : PexelsApiKey.Trim();
        _settings.GlobalSource.PexelsQuery = string.IsNullOrWhiteSpace(PexelsQuery) ? "nature" : PexelsQuery.Trim();
        _settings.GlobalSource.TopicMode = TopicMode;
        _settings.GlobalSource.UnsplashQuery = string.IsNullOrWhiteSpace(UnsplashQuery) ? "landscape" : UnsplashQuery.Trim();
        _settings.GlobalSource.UnsplashApiKey = UnsplashApiKey.Trim();

        foreach (var m in Monitors)
        {
            m.SaveToSettingsConfig();
        }
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
        var result = System.Windows.MessageBox.Show(confirmMsg, "ZeroWall", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            BlacklistManager.Instance.Clear();
            BlacklistCount = BlacklistManager.Instance.Count;
            ShowToast(LocalizationManager.Get("Msg_ClearBlacklistDone"), "Success");
        }
    }

    private void SaveCurrentPictureAs()
    {
        var currentImg = _scheduler.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            ShowToast("Chưa có ảnh nền đang hoạt động để lưu.", "Warning");
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Wallpaper As",
            Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png|All Files (*.*)|*.*",
            FileName = $"Wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(currentImg, saveDialog.FileName, true);
                ShowToast("Đã lưu hình nền vào máy thành công!", "Success");
            }
            catch (Exception ex)
            {
                ShowToast($"Không thể lưu ảnh: {ex.Message}", "Error");
            }
        }
    }

    private void OpenCurrentInExplorer()
    {
        var currentImg = _scheduler.GetCurrentActiveWallpaperPath();
        if (!string.IsNullOrEmpty(currentImg) && File.Exists(currentImg))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{currentImg}\"");
            }
            catch { }
        }
        else
        {
            TrayIconService.OpenCacheFolder();
        }
    }

    private void RefreshCacheStats()
    {
        var stats = CacheManager.GetCacheStats();
        CacheUsageText = stats.FormattedText;
    }

    private void AddToFavorites()
    {
        var currentImg = _scheduler.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            ShowToast("Chưa có ảnh nền đang hoạt động để lưu.", "Warning");
            return;
        }

        var meta = WallpaperMetadataManager.Instance.GetMetadata(currentImg);
        var savedPath = CacheManager.SaveToFavorites(currentImg, meta, _settings.GetEffectiveFavoritesFolder());
        if (!string.IsNullOrEmpty(savedPath))
        {
            ShowToast($"{LocalizationManager.Get("Msg_FavoriteSaved")} ➔ {Path.GetFileName(savedPath)}", "Success");
        }
        else
        {
            ShowToast("Không thể lưu ảnh vào mục Yêu thích.", "Error");
        }
    }

    private void ClearCacheNow()
    {
        var activeFiles = _scheduler.CurrentWallpapers.Values.ToList();
        int deleted = CacheManager.ClearAllCache(activeFiles);
        RefreshCacheStats();
        ShowToast($"{LocalizationManager.Get("Msg_CacheCleared")} ({deleted} files)", "Success");
    }

    private void OpenFavoritesFolder()
    {
        try
        {
            var folder = _settings.GetEffectiveFavoritesFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể mở thư mục: {ex.Message}", "Error");
        }
    }

    private async Task BlacklistCurrentAsync()
    {
        await _scheduler.BlacklistCurrentAsync();
        CurrentWallpaperPath = _scheduler.GetCurrentActiveWallpaperPath();
        BlacklistCount = BlacklistManager.Instance.Count;
        ShowToast("Đã chặn ảnh hiện tại và chuyển sang ảnh mới.", "Info");
    }

    private ProviderConfig BuildCurrentProviderConfig()
    {
        return new ProviderConfig
        {
            Type = SourceType,
            RedditSubreddit = string.IsNullOrWhiteSpace(RedditSubreddit) ? "wallpapers" : RedditSubreddit.Trim(),
            WallhavenQuery = string.IsNullOrWhiteSpace(WallhavenQuery) ? "nature" : WallhavenQuery.Trim(),
            WallhavenApiKey = WallhavenApiKey.Trim(),
            LocalFolderPath = LocalFolderPath.Trim(),
            PexelsApiKey = string.IsNullOrWhiteSpace(PexelsApiKey) ? ProviderConfig.DefaultPexelsApiKey : PexelsApiKey.Trim(),
            PexelsQuery = string.IsNullOrWhiteSpace(PexelsQuery) ? "nature" : PexelsQuery.Trim(),
            TopicMode = TopicMode,
            UnsplashQuery = string.IsNullOrWhiteSpace(UnsplashQuery) ? "landscape" : UnsplashQuery.Trim(),
            UnsplashApiKey = UnsplashApiKey.Trim()
        };
    }

    private async Task TestSampleAsync()
    {
        if (IsChangingWallpaper) return;

        try
        {
            IsChangingWallpaper = true;
            ShowToast("⚡ " + (_language == "vietnamese" ? "Đang tải ảnh mẫu kiểm tra..." : "Fetching sample preview..."), "Info");

            var config = BuildCurrentProviderConfig();
            var samplePath = await _scheduler.FetchSampleImageAsync(config);

            if (!string.IsNullOrEmpty(samplePath) && File.Exists(samplePath))
            {
                CurrentWallpaperPath = samplePath;
                ShowToast(LocalizationManager.Get("UI_TestSampleSuccess"), "Success");
            }
            else
            {
                ShowToast("Không thể tải ảnh mẫu từ nguồn này. Vui lòng kiểm tra lại từ khóa hoặc kết nối mạng.", "Warning");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Lỗi tải ảnh mẫu: {ex.Message}", "Error");
        }
        finally
        {
            IsChangingWallpaper = false;
        }
    }

    private void UpdateCountdownText()
    {
        if (_scheduler.IsPaused)
        {
            NextSwitchCountdownText = "⏸️ " + (Language == "vietnamese" ? "Đang tạm dừng" : "Paused");
            return;
        }

        var remaining = _scheduler.GetRemainingTime();
        if (remaining <= TimeSpan.Zero)
        {
            NextSwitchCountdownText = "⏳ " + (Language == "vietnamese" ? "Đang chuẩn bị đổi..." : "Switching soon...");
        }
        else if (remaining.TotalHours >= 1)
        {
            NextSwitchCountdownText = $"⏱️ {LocalizationManager.Get("UI_NextCountdown")} {(int)remaining.TotalHours}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
        }
        else
        {
            NextSwitchCountdownText = $"⏱️ {LocalizationManager.Get("UI_NextCountdown")} {remaining.Minutes}m {remaining.Seconds:D2}s";
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
            if (success)
            {
                CurrentWallpaperPath = _scheduler.GetCurrentActiveWallpaperPath();
                var meta = WallpaperMetadataManager.Instance.GetMetadata(CurrentWallpaperPath);
                if (ShowWallpaperInfoInApp && meta != null && !string.IsNullOrWhiteSpace(meta.Title))
                {
                    string authorPart = !string.IsNullOrWhiteSpace(meta.Author) ? $" • {meta.Author}" : "";
                    ShowToast($"Đã đổi ảnh: {meta.DisplayTitle}{authorPart}", "Success");
                }
                else
                {
                    ShowToast("Đã đổi hình nền thành công!", "Success");
                }
            }
            else
            {
                ShowToast("Không thể đổi ảnh từ nguồn này. Vui lòng kiểm tra kết nối mạng hoặc từ khóa.", "Warning");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Lỗi: {ex.Message}", "Error");
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
            ShowToast("Tần suất đổi ảnh phải là số phút dương (>0).", "Warning");
            return;
        }

        SaveToSettings();
        ManageAutoStart(_settings.AutoStart);

        _settings.Save();
        _scheduler.UpdateSettings(_settings);
        _scheduler.Start();

        ShowToast(LocalizationManager.Get("Msg_SaveSuccess"), "Success");
        _requestCloseOrHide();
    }

    private static void ManageAutoStart(bool enable)
    {
        try
        {
            using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (rk == null) return;

            const string appName = "ZeroWall";
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
            Debug.WriteLine($"[AutoStart] Error configuring registry: {ex.Message}");
        }
    }
}
