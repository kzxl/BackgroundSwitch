using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace BackgroundSwitch;

public partial class App : Application
{
    private const string MutexName = "Global\\BackgroundSwitch_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;
    private NotifyIcon? _trayIcon;
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
            MessageBox.Show("BackgroundSwitch is already running in the System Tray.", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // 3. Initialize System Tray Icon (Native WinForms NotifyIcon with Dark Theme)
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
        var contextMenu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            Font = new Font("Segoe UI", 9.5f),
            ShowImageMargin = false
        };

        contextMenu.Opening += (_, _) => PopulateContextMenu(contextMenu);

        _trayIcon = new NotifyIcon
        {
            Icon = CreateAppTrayIcon(),
            Text = "BackgroundSwitch — Auto Wallpaper Changer",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
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

    public void PopulateContextMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        // 1. Next Background (Bold)
        var menuNext = new ToolStripMenuItem($"⏭️ {LocalizationManager.Get("Menu_Next")}")
        {
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuNext.Click += async (_, _) =>
        {
            if (_scheduler != null) await _scheduler.ChangeWallpaperAsync();
        };
        menu.Items.Add(menuNext);

        // 2. Previous Background
        var menuPrev = new ToolStripMenuItem($"⏮️ {LocalizationManager.Get("Menu_Previous")}")
        {
            Enabled = _scheduler?.History.CanGoBack ?? false,
            ForeColor = (_scheduler?.History.CanGoBack ?? false) ? Color.FromArgb(205, 214, 244) : Color.FromArgb(108, 112, 134)
        };
        menuPrev.Click += async (_, _) =>
        {
            if (_scheduler != null) await _scheduler.PreviousWallpaperAsync();
        };
        menu.Items.Add(menuPrev);

        // 3. Pause / Resume Toggle
        bool isPaused = _scheduler?.IsPaused ?? false;
        var pauseText = isPaused ? $"▶️ {LocalizationManager.Get("Menu_Resume")}" : $"⏸️ {LocalizationManager.Get("Menu_Pause")}";
        var menuPause = new ToolStripMenuItem(pauseText)
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuPause.Click += (_, _) =>
        {
            _scheduler?.TogglePause();
        };
        menu.Items.Add(menuPause);

        menu.Items.Add(new ToolStripSeparator());

        // 4. Save Picture As...
        var menuSaveAs = new ToolStripMenuItem($"💾 {LocalizationManager.Get("Menu_SaveAs")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuSaveAs.Click += (_, _) => SaveCurrentPictureAs();
        menu.Items.Add(menuSaveAs);

        // 5. View Current Picture (Open in Explorer)
        var menuViewCurrent = new ToolStripMenuItem($"🔍 {LocalizationManager.Get("Menu_ViewCurrent")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuViewCurrent.Click += (_, _) => OpenCurrentPictureInExplorer();
        menu.Items.Add(menuViewCurrent);

        // 6. Never Show Again (Blacklist)
        var menuNeverShowAgain = new ToolStripMenuItem($"🚫 {LocalizationManager.Get("Menu_NeverShowAgain")}")
        {
            ForeColor = Color.FromArgb(243, 139, 168)
        };
        menuNeverShowAgain.Click += async (_, _) =>
        {
            if (_scheduler != null)
            {
                await _scheduler.BlacklistCurrentAsync();
            }
        };
        menu.Items.Add(menuNeverShowAgain);

        // 7. Open Cache Folder
        var menuOpenCache = new ToolStripMenuItem($"📂 {LocalizationManager.Get("Menu_OpenCache")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuOpenCache.Click += (_, _) => OpenCacheFolder();
        menu.Items.Add(menuOpenCache);

        menu.Items.Add(new ToolStripSeparator());

        // 8. Multi-Monitor Submenu (If multiple monitors exist)
        var monitors = WallpaperManager.GetMonitors();
        if (monitors.Count > 1)
        {
            var menuMulti = new ToolStripMenuItem($"🖥️ {LocalizationManager.Get("Menu_MultiMonitor")}")
            {
                ForeColor = Color.FromArgb(137, 180, 250),
                DropDown = new ToolStripDropDownMenu
                {
                    Renderer = new DarkMenuRenderer(),
                    ShowImageMargin = false
                }
            };

            foreach (var monitor in monitors)
            {
                var mItem = new ToolStripMenuItem($"{LocalizationManager.Get("Menu_MonitorNext")} {monitor.FriendlyName}")
                {
                    ForeColor = Color.FromArgb(205, 214, 244)
                };
                uint idx = monitor.Index;
                mItem.Click += async (_, _) =>
                {
                    if (_scheduler != null) await _scheduler.ChangeWallpaperForMonitorAsync(idx);
                };
                menuMulti.DropDownItems.Add(mItem);
            }

            var mSyncAll = new ToolStripMenuItem($"🔄 {LocalizationManager.Get("Menu_SyncAll")}")
            {
                ForeColor = Color.FromArgb(166, 227, 161)
            };
            mSyncAll.Click += async (_, _) =>
            {
                if (_scheduler != null) await _scheduler.ChangeWallpaperAsync();
            };
            menuMulti.DropDownItems.Add(new ToolStripSeparator());
            menuMulti.DropDownItems.Add(mSyncAll);

            menu.Items.Add(menuMulti);
            menu.Items.Add(new ToolStripSeparator());
        }

        // 9. Clear Background
        var menuClear = new ToolStripMenuItem($"🧹 {LocalizationManager.Get("Menu_ClearBackground")}")
        {
            ForeColor = Color.FromArgb(166, 173, 200)
        };
        menuClear.Click += (_, _) => _scheduler?.ClearWallpaper();
        menu.Items.Add(menuClear);

        // 10. Settings...
        var menuSettings = new ToolStripMenuItem($"⚙️ {LocalizationManager.Get("Menu_Settings")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuSettings.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(menuSettings);

        menu.Items.Add(new ToolStripSeparator());

        // 11. Exit
        var menuExit = new ToolStripMenuItem($"❌ {LocalizationManager.Get("Menu_Exit")}")
        {
            ForeColor = Color.FromArgb(243, 139, 168)
        };
        menuExit.Click += (_, _) => ExitApplication();
        menu.Items.Add(menuExit);
    }

    private void SaveCurrentPictureAs()
    {
        var currentImg = _scheduler?.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            MessageBox.Show("No active wallpaper to save.", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Wallpaper saved successfully!", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _trayIcon.Visible = false;
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
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(37, 37, 56);
            public override Color MenuBorder => Color.FromArgb(62, 62, 88);
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => Color.FromArgb(62, 62, 88);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 88);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 88);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(46, 46, 68);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(46, 46, 68);
            public override Color ImageMarginGradientBegin => Color.FromArgb(37, 37, 56);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(37, 37, 56);
            public override Color ImageMarginGradientEnd => Color.FromArgb(37, 37, 56);
            public override Color SeparatorDark => Color.FromArgb(62, 62, 88);
            public override Color SeparatorLight => Color.FromArgb(62, 62, 88);
        }
    }
}
