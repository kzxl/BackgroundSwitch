using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using H.NotifyIcon;
using Microsoft.Win32;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace BackgroundSwitch;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Global\\BackgroundSwitch_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private Scheduler? _scheduler;
    private MainWindow? _mainWindow;
    private AppSettings _settings = new();

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Single Instance Check
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            System.Windows.MessageBox.Show("BackgroundSwitch is already running in the System Tray.", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 2. Load Settings & Localization
        _settings = AppSettings.Load();
        UpdateAppLanguage(_settings.Language);

        _scheduler = new Scheduler(_settings);
        _scheduler.OnError += msg =>
        {
            Debug.WriteLine($"[App] Scheduler error: {msg}");
        };

        // 3. Initialize System Tray Icon
        InitTrayIcon();

        // 4. Check Startup Arguments
        bool isHiddenStart = e.Args.Any(a => a.Equals("--hidden", StringComparison.OrdinalIgnoreCase) || a.Equals("/autostart", StringComparison.OrdinalIgnoreCase));

        _scheduler.Start(isBootDelayed: isHiddenStart);

        if (!isHiddenStart)
        {
            ShowMainWindow();
        }
        else
        {
            TrimWorkingSetMemory();
        }
    }

    public static void UpdateAppLanguage(string? lang)
    {
        LocalizationManager.CurrentLanguage = lang?.ToLowerInvariant() switch
        {
            "vi" => AppLanguage.Vietnamese,
            "en" => AppLanguage.English,
            _ => AppLanguage.Bilingual
        };
    }

    private void InitTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = CreateAppTrayIcon(),
            ToolTipText = "BackgroundSwitch — Auto Wallpaper Changer",
            Visibility = Visibility.Visible
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        var contextMenu = new ContextMenu();
        contextMenu.Opened += (_, _) => PopulateContextMenu(contextMenu);
        PopulateContextMenu(contextMenu);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.ForceCreate();
    }

    private static Icon CreateAppTrayIcon()
    {
        try
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Fill Accent Circle (#89B4FA)
            using var brush = new SolidBrush(Color.FromArgb(137, 180, 250));
            g.FillEllipse(brush, 2, 2, 28, 28);

            // Draw Monitor Icon (#11111B)
            using var pen = new Pen(Color.FromArgb(17, 17, 27), 2f);
            g.DrawRectangle(pen, 7, 7, 18, 12);
            g.DrawLine(pen, 16, 19, 16, 23);
            g.DrawLine(pen, 11, 23, 21, 23);

            var hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            return SystemIcons.Information;
        }
    }

    public void PopulateContextMenu(ContextMenu menu)
    {
        menu.Items.Clear();

        // 1. Next Background (Bold)
        var menuNext = new MenuItem
        {
            Header = $"⏭️ {LocalizationManager.Get("Menu_Next")}",
            FontWeight = FontWeights.Bold
        };
        menuNext.Click += async (_, _) =>
        {
            if (_scheduler != null) await _scheduler.ChangeWallpaperAsync();
        };
        menu.Items.Add(menuNext);

        // 2. Previous Background
        var menuPrev = new MenuItem
        {
            Header = $"⏮️ {LocalizationManager.Get("Menu_Previous")}",
            IsEnabled = _scheduler?.History.CanGoBack ?? false
        };
        menuPrev.Click += async (_, _) =>
        {
            if (_scheduler != null) await _scheduler.PreviousWallpaperAsync();
        };
        menu.Items.Add(menuPrev);

        // 3. Pause / Resume Toggle
        bool isPaused = _scheduler?.IsPaused ?? false;
        var pauseText = isPaused ? $"▶️ {LocalizationManager.Get("Menu_Resume")}" : $"⏸️ {LocalizationManager.Get("Menu_Pause")}";
        var menuPause = new MenuItem { Header = pauseText };
        menuPause.Click += (_, _) =>
        {
            _scheduler?.TogglePause();
        };
        menu.Items.Add(menuPause);

        menu.Items.Add(new Separator());

        // 4. Save Picture As...
        var menuSaveAs = new MenuItem { Header = $"💾 {LocalizationManager.Get("Menu_SaveAs")}" };
        menuSaveAs.Click += (_, _) => SaveCurrentPictureAs();
        menu.Items.Add(menuSaveAs);

        // 5. View Current Picture (Open in Explorer)
        var menuViewCurrent = new MenuItem { Header = $"🔍 {LocalizationManager.Get("Menu_ViewCurrent")}" };
        menuViewCurrent.Click += (_, _) => OpenCurrentPictureInExplorer();
        menu.Items.Add(menuViewCurrent);

        // 6. Never Show Again (Blacklist)
        var menuNeverShowAgain = new MenuItem { Header = $"🚫 {LocalizationManager.Get("Menu_NeverShowAgain")}" };
        menuNeverShowAgain.Click += async (_, _) =>
        {
            if (_scheduler != null)
            {
                await _scheduler.BlacklistCurrentAsync();
            }
        };
        menu.Items.Add(menuNeverShowAgain);

        // 7. Open Cache Folder
        var menuOpenCache = new MenuItem { Header = $"📂 {LocalizationManager.Get("Menu_OpenCache")}" };
        menuOpenCache.Click += (_, _) => OpenCacheFolder();
        menu.Items.Add(menuOpenCache);

        menu.Items.Add(new Separator());

        // 8. Multi-Monitor Submenu (If multiple monitors exist)
        var monitors = WallpaperManager.GetMonitors();
        if (monitors.Count > 1)
        {
            var menuMulti = new MenuItem { Header = $"🖥️ {LocalizationManager.Get("Menu_MultiMonitor")}" };

            foreach (var monitor in monitors)
            {
                var mItem = new MenuItem { Header = $"{LocalizationManager.Get("Menu_MonitorNext")} {monitor.FriendlyName}" };
                uint idx = monitor.Index;
                mItem.Click += async (_, _) =>
                {
                    if (_scheduler != null) await _scheduler.ChangeWallpaperForMonitorAsync(idx);
                };
                menuMulti.Items.Add(mItem);
            }

            var mSyncAll = new MenuItem { Header = $"🔄 {LocalizationManager.Get("Menu_SyncAll")}" };
            mSyncAll.Click += async (_, _) =>
            {
                if (_scheduler != null) await _scheduler.ChangeWallpaperAsync();
            };
            menuMulti.Items.Add(new Separator());
            menuMulti.Items.Add(mSyncAll);

            menu.Items.Add(menuMulti);
            menu.Items.Add(new Separator());
        }

        // 9. Clear Background
        var menuClear = new MenuItem { Header = $"🧹 {LocalizationManager.Get("Menu_ClearBackground")}" };
        menuClear.Click += (_, _) => _scheduler?.ClearWallpaper();
        menu.Items.Add(menuClear);

        // 10. Settings...
        var menuSettings = new MenuItem { Header = $"⚙️ {LocalizationManager.Get("Menu_Settings")}" };
        menuSettings.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(menuSettings);

        menu.Items.Add(new Separator());

        // 11. Exit
        var menuExit = new MenuItem { Header = $"❌ {LocalizationManager.Get("Menu_Exit")}" };
        menuExit.Click += (_, _) => ExitApplication();
        menu.Items.Add(menuExit);
    }

    private void SaveCurrentPictureAs()
    {
        var currentImg = _scheduler?.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            System.Windows.MessageBox.Show("No active wallpaper to save.", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
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
                System.Windows.MessageBox.Show("Wallpaper saved successfully!", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenCurrentPictureInExplorer()
    {
        var currentImg = _scheduler?.GetCurrentActiveWallpaperPath();
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
            OpenCacheFolder();
        }
    }

    private static void OpenCacheFolder()
    {
        var cachePath = Path.Combine(Path.GetTempPath(), "BackgroundSwitch");
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        try
        {
            Process.Start("explorer.exe", cachePath);
        }
        catch { }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow(_settings, _scheduler!);
            _mainWindow.Closed += (_, _) =>
            {
                _mainWindow = null;
                TrimWorkingSetMemory();
            };
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    public void TrimWorkingSetMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
        }
        catch { }
    }

    public void ExitApplication()
    {
        _scheduler?.Stop();
        _scheduler?.Dispose();

        if (_trayIcon != null)
        {
            _trayIcon.Visibility = Visibility.Collapsed;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _mainWindow?.Close();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
