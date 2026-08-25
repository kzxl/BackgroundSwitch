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

    public MainWindow(AppSettings settings, Scheduler scheduler)
    {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        LoadSettingsToUI();
    }

    private void LoadSettingsToUI()
    {
        // 1. Monitor Mode
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

        // 2. Scale Selection
        var scaleTag = _settings.Scale.ToString();
        foreach (ComboBoxItem item in cbScale.Items)
        {
            if (string.Equals(item.Tag?.ToString(), scaleTag, StringComparison.OrdinalIgnoreCase))
            {
                cbScale.SelectedItem = item;
                break;
            }
        }

        // 3. Global Source
        var sourceType = _settings.GlobalSource.Type;
        if (string.Equals(sourceType, "Local", StringComparison.OrdinalIgnoreCase))
        {
            rbSourceLocal.IsChecked = true;
            panelGlobalLocal.Visibility = Visibility.Visible;
            panelGlobalPexels.Visibility = Visibility.Collapsed;
            txtBingInfo.Visibility = Visibility.Collapsed;
        }
        else if (string.Equals(sourceType, "Pexels", StringComparison.OrdinalIgnoreCase))
        {
            rbSourcePexels.IsChecked = true;
            panelGlobalLocal.Visibility = Visibility.Collapsed;
            panelGlobalPexels.Visibility = Visibility.Visible;
            txtBingInfo.Visibility = Visibility.Collapsed;
        }
        else
        {
            rbSourceBing.IsChecked = true;
            panelGlobalLocal.Visibility = Visibility.Collapsed;
            panelGlobalPexels.Visibility = Visibility.Collapsed;
            txtBingInfo.Visibility = Visibility.Visible;
        }

        txtGlobalFolderPath.Text = _settings.GlobalSource.LocalFolderPath;
        txtGlobalPexelsApiKey.Text = _settings.GlobalSource.PexelsApiKey;
        txtGlobalPexelsQuery.Text = _settings.GlobalSource.PexelsQuery;

        // 4. Populate Monitors List
        var monitors = WallpaperManager.GetMonitors();
        listMonitors.ItemsSource = monitors;

        // 5. General Settings
        txtInterval.Text = Math.Max(1, _settings.IntervalMinutes).ToString();
        chkAutoStart.IsChecked = _settings.AutoStart;
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (cardPerMonitor == null) return;

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
        if (cbScale?.SelectedItem is ComboBoxItem selectedItem &&
            Enum.TryParse<WallpaperScale>(selectedItem.Tag?.ToString(), out var scale))
        {
            _settings.Scale = scale;
        }
    }

    private void GlobalSource_Changed(object sender, RoutedEventArgs e)
    {
        if (panelGlobalLocal == null || panelGlobalPexels == null || txtBingInfo == null) return;

        if (rbSourceLocal.IsChecked == true)
        {
            panelGlobalLocal.Visibility = Visibility.Visible;
            panelGlobalPexels.Visibility = Visibility.Collapsed;
            txtBingInfo.Visibility = Visibility.Collapsed;
        }
        else if (rbSourcePexels.IsChecked == true)
        {
            panelGlobalLocal.Visibility = Visibility.Collapsed;
            panelGlobalPexels.Visibility = Visibility.Visible;
            txtBingInfo.Visibility = Visibility.Collapsed;
        }
        else
        {
            panelGlobalLocal.Visibility = Visibility.Collapsed;
            panelGlobalPexels.Visibility = Visibility.Collapsed;
            txtBingInfo.Visibility = Visibility.Visible;
        }
    }

    private void BtnBrowseGlobalFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtGlobalFolderPath.Text = dialog.SelectedPath;
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
            System.Windows.MessageBox.Show("Tần suất đổi phải là số nguyên dương (phút).", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (rbSourceLocal.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Local";
        }
        else if (rbSourcePexels.IsChecked == true)
        {
            _settings.GlobalSource.Type = "Pexels";
        }
        else
        {
            _settings.GlobalSource.Type = "BingDaily";
        }

        _settings.GlobalSource.LocalFolderPath = txtGlobalFolderPath.Text.Trim();
        _settings.GlobalSource.PexelsApiKey = txtGlobalPexelsApiKey.Text.Trim();
        _settings.GlobalSource.PexelsQuery = string.IsNullOrWhiteSpace(txtGlobalPexelsQuery.Text) ? "nature" : txtGlobalPexelsQuery.Text.Trim();

        // 4. Save AutoStart
        _settings.AutoStart = chkAutoStart.IsChecked ?? false;
        ManageAutoStart(_settings.AutoStart);

        // 5. Update Monitors Config Cache
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

        // 6. Persist & Restart Scheduler
        _settings.Save();
        _scheduler.UpdateSettings(_settings);
        _scheduler.Start();

        System.Windows.MessageBox.Show("Cài đặt đã được lưu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
            System.Windows.MessageBox.Show($"Lỗi đổi hình nền: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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