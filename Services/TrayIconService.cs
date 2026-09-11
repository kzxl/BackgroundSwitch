using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BackgroundSwitch.Services;

namespace BackgroundSwitch.Services;

public class TrayIconService : IDisposable
{
    private readonly Scheduler _scheduler;
    private readonly Action _showMainWindowAction;
    private readonly Action _exitAppAction;
    private NotifyIcon? _trayIcon;
    private bool _disposed;

    public TrayIconService(Scheduler scheduler, Action showMainWindowAction, Action exitAppAction)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _showMainWindowAction = showMainWindowAction ?? throw new ArgumentNullException(nameof(showMainWindowAction));
        _exitAppAction = exitAppAction ?? throw new ArgumentNullException(nameof(exitAppAction));

        InitTrayIcon();
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

        _trayIcon.DoubleClick += (_, _) => _showMainWindowAction();
    }

    private void PopulateContextMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        // 1. Next Background (Bold)
        var menuNext = new ToolStripMenuItem($"⏭️ {LocalizationManager.Get("Menu_Next")}")
        {
            Font = new Font(menu.Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuNext.Click += async (_, _) => await _scheduler.ChangeWallpaperAsync();
        menu.Items.Add(menuNext);

        // 2. Previous Background
        var canGoBack = _scheduler.History.CanGoBack;
        var menuPrev = new ToolStripMenuItem($"⏮️ {LocalizationManager.Get("Menu_Previous")}")
        {
            Enabled = canGoBack,
            ForeColor = canGoBack ? Color.FromArgb(205, 214, 244) : Color.FromArgb(108, 112, 134)
        };
        menuPrev.Click += async (_, _) => await _scheduler.PreviousWallpaperAsync();
        menu.Items.Add(menuPrev);

        // 3. Pause / Resume Toggle
        bool isPaused = _scheduler.IsPaused;
        var pauseText = isPaused ? $"▶️ {LocalizationManager.Get("Menu_Resume")}" : $"⏸️ {LocalizationManager.Get("Menu_Pause")}";
        var menuPause = new ToolStripMenuItem(pauseText)
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuPause.Click += (_, _) => _scheduler.TogglePause();
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
        menuNeverShowAgain.Click += async (_, _) => await _scheduler.BlacklistCurrentAsync();
        menu.Items.Add(menuNeverShowAgain);

        // 7. Open Cache Folder
        var menuOpenCache = new ToolStripMenuItem($"📂 {LocalizationManager.Get("Menu_OpenCache")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuOpenCache.Click += (_, _) => OpenCacheFolder();
        menu.Items.Add(menuOpenCache);

        menu.Items.Add(new ToolStripSeparator());

        // 8. Multi-Monitor Submenu
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
                mItem.Click += async (_, _) => await _scheduler.ChangeWallpaperForMonitorAsync(idx);
                menuMulti.DropDownItems.Add(mItem);
            }

            var mSyncAll = new ToolStripMenuItem($"🔄 {LocalizationManager.Get("Menu_SyncAll")}")
            {
                ForeColor = Color.FromArgb(166, 227, 161)
            };
            mSyncAll.Click += async (_, _) => await _scheduler.ChangeWallpaperAsync();
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
        menuClear.Click += (_, _) => _scheduler.ClearWallpaper();
        menu.Items.Add(menuClear);

        // 10. Settings...
        var menuSettings = new ToolStripMenuItem($"⚙️ {LocalizationManager.Get("Menu_Settings")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuSettings.Click += (_, _) => _showMainWindowAction();
        menu.Items.Add(menuSettings);

        menu.Items.Add(new ToolStripSeparator());

        // 11. Exit
        var menuExit = new ToolStripMenuItem($"❌ {LocalizationManager.Get("Menu_Exit")}")
        {
            ForeColor = Color.FromArgb(243, 139, 168)
        };
        menuExit.Click += (_, _) => _exitAppAction();
        menu.Items.Add(menuExit);
    }

    private void SaveCurrentPictureAs()
    {
        var currentImg = _scheduler.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            System.Windows.MessageBox.Show("No active wallpaper to save.", "BackgroundSwitch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
                System.Windows.MessageBox.Show("Wallpaper saved successfully!", "BackgroundSwitch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not save image: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void OpenCurrentPictureInExplorer()
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
            OpenCacheFolder();
        }
    }

    public static void OpenCacheFolder()
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

    private static Icon CreateAppTrayIcon()
    {
        try
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var brush = new SolidBrush(Color.FromArgb(137, 180, 250));
            g.FillEllipse(brush, 2, 2, 28, 28);

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

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
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
