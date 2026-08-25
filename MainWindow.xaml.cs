using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using Microsoft.Win32;

namespace BackgroundSwitch;

public partial class MainWindow : Window
{
    private AppSettings _settings;
    private readonly Scheduler _scheduler;
    private bool _isExplicitClose;

    public MainWindow()
    {
        InitializeComponent();

        // Set default icon for tray
        MyNotifyIcon.Icon = System.Drawing.SystemIcons.Information;

        _settings = AppSettings.Load();
        LoadSettingsToUI();

        _scheduler = new Scheduler(_settings);
        _scheduler.OnError += msg =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        };
        _scheduler.Start();
    }

    private void LoadSettingsToUI()
    {
        if (string.Equals(_settings.SourceType, "Pexels", StringComparison.OrdinalIgnoreCase))
        {
            rbPexels.IsChecked = true;
            panelLocal.Visibility = Visibility.Collapsed;
            panelPexels.Visibility = Visibility.Visible;
        }
        else
        {
            rbLocal.IsChecked = true;
            panelLocal.Visibility = Visibility.Visible;
            panelPexels.Visibility = Visibility.Collapsed;
        }

        txtFolderPath.Text = _settings.LocalFolderPath;
        txtApiKey.Text = _settings.PexelsApiKey;
        txtQuery.Text = _settings.PexelsQuery;
        txtInterval.Text = _settings.IntervalMinutes.ToString();
        chkAutoStart.IsChecked = _settings.AutoStart;

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "--hidden")
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void Source_Changed(object sender, RoutedEventArgs e)
    {
        if (panelLocal == null || panelPexels == null) return;

        if (rbPexels.IsChecked == true)
        {
            panelLocal.Visibility = Visibility.Collapsed;
            panelPexels.Visibility = Visibility.Visible;
        }
        else
        {
            panelLocal.Visibility = Visibility.Visible;
            panelPexels.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtFolderPath.Text = dialog.SelectedPath;
        }
    }

    private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSave_Click(sender, e);
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
            System.Windows.MessageBox.Show("Interval must be a valid positive number (minutes).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.SourceType = rbPexels.IsChecked == true ? "Pexels" : "Local";
        _settings.LocalFolderPath = txtFolderPath.Text.Trim();
        _settings.PexelsApiKey = txtApiKey.Text.Trim();
        _settings.PexelsQuery = txtQuery.Text.Trim();
        _settings.AutoStart = chkAutoStart.IsChecked ?? false;

        _settings.Save();
        ManageAutoStart(_settings.AutoStart);

        _scheduler.UpdateSettings(_settings);
        _scheduler.Start();

        System.Windows.MessageBox.Show("Settings saved and scheduler started!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        WindowState = WindowState.Minimized;
    }

    private async void BtnChangeNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _scheduler.ChangeWallpaperAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not change wallpaper: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ManageAutoStart(bool enable)
    {
        try
        {
            using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (rk == null) return;

            const string appName = "BackgroundSwitch";
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
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
            System.Windows.MessageBox.Show("Could not set AutoStart: " + ex.Message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isExplicitClose)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
    }

    private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void MenuChangeNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _scheduler.ChangeWallpaperAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not change wallpaper: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitClose = true;
        _scheduler.Dispose();
        MyNotifyIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}