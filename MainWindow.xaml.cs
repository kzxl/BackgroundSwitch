using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using Microsoft.Win32;

namespace BackgroundSwitch;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Scheduler _scheduler;
    private bool _isRealClose;
    private bool _isInitializing = true;

    public MainWindow(AppSettings settings, Scheduler scheduler)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _isInitializing = true;

        InitializeComponent();

        LoadSettingsToUI();
        _isInitializing = false;
    }

    private void LoadSettingsToUI()
    {
        // 1. Language
        var langTag = _settings.Language?.ToLowerInvariant() ?? "bilingual";
        foreach (ComboBoxItem item in cbLanguage.Items)
        {
            if (string.Equals(item.Tag?.ToString(), langTag, StringComparison.OrdinalIgnoreCase))
            {
                cbLanguage.SelectedItem = item;
                break;
            }
        }
        App.UpdateAppLanguage(langTag);
        ApplyLocalization();

        // 2. Monitor Mode
        switch (_settings.Mode)
        {
            case WallpaperMode.PerMonitor:
                rbModePerMonitor.IsChecked = true;
                cardPerMonitor.Visibility = Visibility.Visible;
                break;
            case WallpaperMode.Span:
                rbModeSpan.IsChecked = true;
                cardPerMonitor.Visibility = Visibility.Collapsed;
                break;
            default:
                rbModeSynced.IsChecked = true;
                cardPerMonitor.Visibility = Visibility.Collapsed;
                break;
        }

        // 3. Scale Selection
        var scaleTag = _settings.Scale.ToString();
        foreach (ComboBoxItem item in cbScale.Items)
        {
            if (string.Equals(item.Tag?.ToString(), scaleTag, StringComparison.OrdinalIgnoreCase))
            {
                cbScale.SelectedItem = item;
                break;
            }
        }

        // 4. Global Source Selection
        var sourceType = _settings.GlobalSource.Type;
        switch (sourceType.ToLowerInvariant())
        {
            case "reddit":
                rbSourceReddit.IsChecked = true;
                break;
            case "wallhaven":
                rbSourceWallhaven.IsChecked = true;
                break;
            case "nasa" or "apod":
                rbSourceNasa.IsChecked = true;
                break;
            case "local":
                rbSourceLocal.IsChecked = true;
                break;
            case "pexels":
                rbSourcePexels.IsChecked = true;
                break;
            case "unsplash":
                rbSourceUnsplash.IsChecked = true;
                break;
            default:
                rbSourceBing.IsChecked = true;
                break;
        }

        txtGlobalRedditSub.Text = string.IsNullOrWhiteSpace(_settings.GlobalSource.RedditSubreddit) ? "wallpapers" : _settings.GlobalSource.RedditSubreddit;
        txtGlobalWallhavenQuery.Text = string.IsNullOrWhiteSpace(_settings.GlobalSource.WallhavenQuery) ? "nature" : _settings.GlobalSource.WallhavenQuery;
        txtGlobalWallhavenApiKey.Text = _settings.GlobalSource.WallhavenApiKey;
        txtGlobalFolderPath.Text = _settings.GlobalSource.LocalFolderPath;
        txtGlobalPexelsApiKey.Text = _settings.GlobalSource.PexelsApiKey;
        txtGlobalPexelsQuery.Text = _settings.GlobalSource.PexelsQuery;
        txtGlobalUnsplashQuery.Text = _settings.GlobalSource.UnsplashQuery;
        txtGlobalUnsplashApiKey.Text = _settings.GlobalSource.UnsplashApiKey;

        UpdateProviderPanels();

        // 5. Populate Monitors List
        var monitors = WallpaperManager.GetMonitors();
        listMonitors.ItemsSource = monitors;

        // 6. General Settings
        txtInterval.Text = Math.Max(1, _settings.IntervalMinutes).ToString();
        chkAutoStart.IsChecked = _settings.AutoStart;

        // 7. Blacklist Count
        UpdateBlacklistCountUI();
    }

    private void UpdateBlacklistCountUI()
    {
        lblBlacklistCount.Text = $"{LocalizationManager.Get("UI_BlacklistInfo")} {BlacklistManager.Instance.Count}";
    }

    private void ApplyLocalization()
    {
        Title = LocalizationManager.Get("UI_Title");
        lblAppTitle.Text = "🖼️ BackgroundSwitch";
        lblAppSubtitle.Text = LocalizationManager.Get("UI_Subtitle");

        lblLanguage.Text = $"🌐 {LocalizationManager.Get("UI_LanguageLabel")}";
        lblModeSection.Text = LocalizationManager.Get("UI_ModeSection");
        rbModeSynced.Content = LocalizationManager.Get("UI_ModeSynced");
        rbModePerMonitor.Content = LocalizationManager.Get("UI_ModePerMonitor");
        rbModeSpan.Content = LocalizationManager.Get("UI_ModeSpan");
        lblScale.Text = LocalizationManager.Get("UI_ScaleLabel");

        lblSourceSection.Text = LocalizationManager.Get("UI_SourceSection");
        rbSourceBing.Content = LocalizationManager.Get("UI_SourceBing");
        rbSourceReddit.Content = LocalizationManager.Get("UI_SourceReddit");
        rbSourceWallhaven.Content = LocalizationManager.Get("UI_SourceWallhaven");
        rbSourceNasa.Content = LocalizationManager.Get("UI_SourceNasa");
        rbSourceLocal.Content = LocalizationManager.Get("UI_SourceLocal");
        rbSourcePexels.Content = LocalizationManager.Get("UI_SourcePexels");
        rbSourceUnsplash.Content = LocalizationManager.Get("UI_SourceUnsplash");

        lblRedditSub.Text = LocalizationManager.Get("UI_RedditSub");
        lblWallhavenQuery.Text = LocalizationManager.Get("UI_WallhavenQuery");
        lblWallhavenApiKey.Text = LocalizationManager.Get("UI_WallhavenApiKey");
        lblLocalFolderPath.Text = LocalizationManager.Get("UI_LocalFolderPath");
        btnBrowse.Content = LocalizationManager.Get("UI_BrowseBtn");
        lblPexelsApiKey.Text = LocalizationManager.Get("UI_PexelsApiKey");
        lblPexelsQuery.Text = LocalizationManager.Get("UI_PexelsQuery");
        lblUnsplashQuery.Text = LocalizationManager.Get("UI_UnsplashQuery");
        lblUnsplashApiKey.Text = LocalizationManager.Get("UI_UnsplashApiKey");

        lblPerMonitorSection.Text = LocalizationManager.Get("UI_PerMonitorSection");
        lblGeneralSection.Text = LocalizationManager.Get("UI_GeneralSection");
        lblInterval.Text = LocalizationManager.Get("UI_IntervalLabel");
        lblMinutesUnit.Text = LocalizationManager.Get("UI_MinutesUnit");
        chkAutoStart.Content = LocalizationManager.Get("UI_AutoStart");

        btnClearBlacklist.Content = LocalizationManager.Get("UI_ClearBlacklistBtn");
        btnChangeNow.Content = LocalizationManager.Get("UI_ChangeNowBtn");
        btnSave.Content = LocalizationManager.Get("UI_SaveBtn");

        UpdateProviderPanels();
        UpdateBlacklistCountUI();
    }

    private void UpdateProviderPanels()
    {
        if (panelGlobalReddit == null || panelGlobalWallhaven == null || panelGlobalLocal == null ||
            panelGlobalPexels == null || panelGlobalUnsplash == null || txtProviderInfo == null) return;

        panelGlobalReddit.Visibility = Visibility.Collapsed;
        panelGlobalWallhaven.Visibility = Visibility.Collapsed;
        panelGlobalLocal.Visibility = Visibility.Collapsed;
        panelGlobalPexels.Visibility = Visibility.Collapsed;
        panelGlobalUnsplash.Visibility = Visibility.Collapsed;

        if (rbSourceReddit.IsChecked == true)
        {
            panelGlobalReddit.Visibility = Visibility.Visible;
            txtProviderInfo.Text = LocalizationManager.Get("UI_RedditDesc");
        }
        else if (rbSourceWallhaven.IsChecked == true)
        {
            panelGlobalWallhaven.Visibility = Visibility.Visible;
            txtProviderInfo.Text = LocalizationManager.Get("UI_WallhavenDesc");
        }
        else if (rbSourceNasa.IsChecked == true)
        {
            txtProviderInfo.Text = LocalizationManager.Get("UI_NasaDesc");
        }
        else if (rbSourceLocal.IsChecked == true)
        {
            panelGlobalLocal.Visibility = Visibility.Visible;
            txtProviderInfo.Text = "📁 Quét ảnh trực tiếp từ thư mục trên máy tính của bạn.";
        }
        else if (rbSourcePexels.IsChecked == true)
        {
            panelGlobalPexels.Visibility = Visibility.Visible;
            txtProviderInfo.Text = "📷 Tự động tìm kiếm và tải ảnh chất lượng cao từ Pexels API theo từ khóa.";
        }
        else if (rbSourceUnsplash.IsChecked == true)
        {
            panelGlobalUnsplash.Visibility = Visibility.Visible;
            txtProviderInfo.Text = LocalizationManager.Get("UI_UnsplashDesc");
        }
        else
        {
            // Default Bing Daily
            txtProviderInfo.Text = LocalizationManager.Get("UI_BingDesc");
        }
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || cbLanguage?.SelectedItem is not ComboBoxItem selectedItem) return;

        var lang = selectedItem.Tag?.ToString() ?? "bilingual";
        _settings.Language = lang;
        App.UpdateAppLanguage(lang);
        ApplyLocalization();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || cardPerMonitor == null) return;

        if (rbModePerMonitor.IsChecked == true)
        {
            cardPerMonitor.Visibility = Visibility.Visible;
        }
        else
        {
            cardPerMonitor.Visibility = Visibility.Collapsed;
        }
    }

    private void Scale_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;

        if (cbScale?.SelectedItem is ComboBoxItem selectedItem &&
            Enum.TryParse<WallpaperScale>(selectedItem.Tag?.ToString(), out var scale))
        {
            _settings.Scale = scale;
        }
    }

    private void GlobalSource_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateProviderPanels();
    }

    private void BtnBrowseGlobalFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtGlobalFolderPath.Text = dialog.SelectedPath;
        }
    }

    private void BtnClearBlacklist_Click(object sender, RoutedEventArgs e)
    {
        var confirmMsg = LocalizationManager.Get("Msg_ClearBlacklistConfirm");
        var result = System.Windows.MessageBox.Show(confirmMsg, "BackgroundSwitch", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            BlacklistManager.Instance.Clear();
            UpdateBlacklistCountUI();
            System.Windows.MessageBox.Show(LocalizationManager.Get("Msg_ClearBlacklistDone"), "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(txtInterval.Text, out int interval) && interval > 0)
        {
            _settings.IntervalMinutes = interval;
        }
        else
        {
            System.Windows.MessageBox.Show("Interval must be a valid positive number (minutes).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 1. Save Mode
        if (rbModePerMonitor.IsChecked == true)
        {
            _settings.Mode = WallpaperMode.PerMonitor;
        }
        else if (rbModeSpan.IsChecked == true)
        {
            _settings.Mode = WallpaperMode.Span;
        }
        else
        {
            _settings.Mode = WallpaperMode.Synced;
        }

        // 2. Save Scale
        if (cbScale.SelectedItem is ComboBoxItem selectedScale &&
            Enum.TryParse<WallpaperScale>(selectedScale.Tag?.ToString(), out var scaleVal))
        {
            _settings.Scale = scaleVal;
        }

        // 3. Save Global Source
        if (rbSourceReddit.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Reddit";
        }
        else if (rbSourceWallhaven.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Wallhaven";
        }
        else if (rbSourceNasa.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Nasa";
        }
        else if (rbSourceLocal.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Local";
        }
        else if (rbSourcePexels.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Pexels";
        }
        else if (rbSourceUnsplash.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Unsplash";
        }
        else
        {
            _settings.GlobalSource.Type = "BingDaily";
        }

        _settings.GlobalSource.RedditSubreddit = string.IsNullOrWhiteSpace(txtGlobalRedditSub.Text) ? "wallpapers" : txtGlobalRedditSub.Text.Trim();
        _settings.GlobalSource.WallhavenQuery = string.IsNullOrWhiteSpace(txtGlobalWallhavenQuery.Text) ? "nature" : txtGlobalWallhavenQuery.Text.Trim();
        _settings.GlobalSource.WallhavenApiKey = txtGlobalWallhavenApiKey.Text.Trim();
        _settings.GlobalSource.LocalFolderPath = txtGlobalFolderPath.Text.Trim();
        _settings.GlobalSource.PexelsApiKey = txtGlobalPexelsApiKey.Text.Trim();
        _settings.GlobalSource.PexelsQuery = string.IsNullOrWhiteSpace(txtGlobalPexelsQuery.Text) ? "nature" : txtGlobalPexelsQuery.Text.Trim();
        _settings.GlobalSource.UnsplashQuery = string.IsNullOrWhiteSpace(txtGlobalUnsplashQuery.Text) ? "landscape" : txtGlobalUnsplashQuery.Text.Trim();
        _settings.GlobalSource.UnsplashApiKey = txtGlobalUnsplashApiKey.Text.Trim();

        // 4. Save Language
        if (cbLanguage.SelectedItem is ComboBoxItem selectedLang)
        {
            _settings.Language = selectedLang.Tag?.ToString() ?? "bilingual";
        }

        // 5. Save AutoStart
        _settings.AutoStart = chkAutoStart.IsChecked ?? false;
        ManageAutoStart(_settings.AutoStart);

        // 6. Update Monitors Config Cache
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

        // 7. Persist & Restart Scheduler
        _settings.Save();
        _scheduler.UpdateSettings(_settings);
        _scheduler.Start();

        System.Windows.MessageBox.Show(LocalizationManager.Get("Msg_SaveSuccess"), "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
        Hide();
        ((App)System.Windows.Application.Current).TrimWorkingSetMemory();
    }

    private async void BtnChangeNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _scheduler.ChangeWallpaperAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isRealClose)
        {
            e.Cancel = true;
            Hide();
            ((App)System.Windows.Application.Current).TrimWorkingSetMemory();
        }
    }

    public void ForceClose()
    {
        _isRealClose = true;
        Close();
    }
}