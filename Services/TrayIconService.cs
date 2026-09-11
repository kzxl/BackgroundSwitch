using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BackgroundSwitch.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace BackgroundSwitch.Services;

public class TrayIconService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly Scheduler _scheduler;
    private readonly Action _showMainWindowAction;
    private readonly Action _exitAppAction;

    private NotifyIcon? _trayIcon;
    private Icon? _loadedIcon;
    private bool _disposed;

    private readonly Action<bool> _onPauseChangedHandler;
    private readonly Action _onWallpaperChangedHandler;

    public TrayIconService(Scheduler scheduler, Action showMainWindowAction, Action exitAppAction)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _showMainWindowAction = showMainWindowAction ?? throw new ArgumentNullException(nameof(showMainWindowAction));
        _exitAppAction = exitAppAction ?? throw new ArgumentNullException(nameof(exitAppAction));

        _onPauseChangedHandler = _ => UpdateTrayTooltip();
        _onWallpaperChangedHandler = UpdateTrayTooltip;

        _scheduler.OnPauseStateChanged += _onPauseChangedHandler;
        _scheduler.OnWallpaperChanged += _onWallpaperChangedHandler;

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

        _loadedIcon = LoadAppIcon();

        _trayIcon = new NotifyIcon
        {
            Icon = _loadedIcon,
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        UpdateTrayTooltip();

        _trayIcon.DoubleClick += (_, _) => _showMainWindowAction();
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null) return;

        try
        {
            string stateKey = _scheduler.IsPaused ? "Tray_TooltipPaused" : "Tray_TooltipActive";
            string tooltip = LocalizationManager.Get(stateKey);

            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 60) + "...";
            }

            _trayIcon.Text = tooltip;
        }
        catch { }
    }

    private void PopulateContextMenu(ContextMenuStrip menu)
    {
        // 1. Recursive dispose old items to prevent WinForms GDI handle leaks
        ClearAndDisposeMenu(menu);

        // 2. Next Background (Bold)
        var menuNext = new ToolStripMenuItem($"⏭️ {LocalizationManager.Get("Menu_Next")}")
        {
            Font = new Font(menu.Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuNext.Click += async (_, _) => await _scheduler.ChangeWallpaperAsync();
        menu.Items.Add(menuNext);

        // 3. Previous Background
        var canGoBack = _scheduler.History.CanGoBack;
        var menuPrev = new ToolStripMenuItem($"⏮️ {LocalizationManager.Get("Menu_Previous")}")
        {
            Enabled = canGoBack,
            ForeColor = canGoBack ? Color.FromArgb(205, 214, 244) : Color.FromArgb(108, 112, 134)
        };
        menuPrev.Click += async (_, _) => await _scheduler.PreviousWallpaperAsync();
        menu.Items.Add(menuPrev);

        // 4. Pause / Resume Toggle
        bool isPaused = _scheduler.IsPaused;
        var pauseText = isPaused ? $"▶️ {LocalizationManager.Get("Menu_Resume")}" : $"⏸️ {LocalizationManager.Get("Menu_Pause")}";
        var menuPause = new ToolStripMenuItem(pauseText)
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuPause.Click += (_, _) => _scheduler.TogglePause();
        menu.Items.Add(menuPause);

        menu.Items.Add(new ToolStripSeparator());

        // 5. Save Picture As...
        var menuSaveAs = new ToolStripMenuItem($"💾 {LocalizationManager.Get("Menu_SaveAs")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuSaveAs.Click += (_, _) => SaveCurrentPictureAs();
        menu.Items.Add(menuSaveAs);

        // 6. View Current Picture (Open in Explorer)
        var menuViewCurrent = new ToolStripMenuItem($"🔍 {LocalizationManager.Get("Menu_ViewCurrent")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuViewCurrent.Click += (_, _) => OpenCurrentPictureInExplorer();
        menu.Items.Add(menuViewCurrent);

        // 7. Never Show Again (Blacklist)
        var menuNeverShowAgain = new ToolStripMenuItem($"🚫 {LocalizationManager.Get("Menu_NeverShowAgain")}")
        {
            ForeColor = Color.FromArgb(243, 139, 168)
        };
        menuNeverShowAgain.Click += async (_, _) => await _scheduler.BlacklistCurrentAsync();
        menu.Items.Add(menuNeverShowAgain);

        // 8. Open Cache Folder
        var menuOpenCache = new ToolStripMenuItem($"📂 {LocalizationManager.Get("Menu_OpenCache")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuOpenCache.Click += (_, _) => OpenCacheFolder();
        menu.Items.Add(menuOpenCache);

        menu.Items.Add(new ToolStripSeparator());

        // 9. Multi-Monitor Submenu
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

        // 10. Clear Background
        var menuClear = new ToolStripMenuItem($"🧹 {LocalizationManager.Get("Menu_ClearBackground")}")
        {
            ForeColor = Color.FromArgb(166, 173, 200)
        };
        menuClear.Click += (_, _) => _scheduler.ClearWallpaper();
        menu.Items.Add(menuClear);

        // 11. Settings...
        var menuSettings = new ToolStripMenuItem($"⚙️ {LocalizationManager.Get("Menu_Settings")}")
        {
            ForeColor = Color.FromArgb(205, 214, 244)
        };
        menuSettings.Click += (_, _) => _showMainWindowAction();
        menu.Items.Add(menuSettings);

        menu.Items.Add(new ToolStripSeparator());

        // 12. Exit
        var menuExit = new ToolStripMenuItem($"❌ {LocalizationManager.Get("Menu_Exit")}")
        {
            ForeColor = Color.FromArgb(243, 139, 168)
        };
        menuExit.Click += (_, _) => _exitAppAction();
        menu.Items.Add(menuExit);
    }

    private static void ClearAndDisposeMenu(ContextMenuStrip menu)
    {
        for (int i = menu.Items.Count - 1; i >= 0; i--)
        {
            var item = menu.Items[i];
            if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
            {
                ClearAndDisposeDropDownItems(dropDownItem);
            }
            item.Dispose();
        }
        menu.Items.Clear();
    }

    private static void ClearAndDisposeDropDownItems(ToolStripDropDownItem container)
    {
        for (int i = container.DropDownItems.Count - 1; i >= 0; i--)
        {
            var sub = container.DropDownItems[i];
            if (sub is ToolStripDropDownItem subDrop && subDrop.HasDropDownItems)
            {
                ClearAndDisposeDropDownItems(subDrop);
            }
            sub.Dispose();
        }
        container.DropDownItems.Clear();
    }

    private void SaveCurrentPictureAs()
    {
        var currentImg = _scheduler.GetCurrentActiveWallpaperPath();
        if (string.IsNullOrEmpty(currentImg) || !File.Exists(currentImg))
        {
            MessageBox.Show(LocalizationManager.Get("Msg_NoActiveWallpaper"), LocalizationManager.Get("Msg_SaveTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationManager.Get("Msg_SaveTitle"),
            Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png|All Files (*.*)|*.*",
            FileName = $"Wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(currentImg, saveDialog.FileName, true);
                MessageBox.Show(LocalizationManager.Get("Msg_SaveSuccess"), LocalizationManager.Get("Msg_SaveTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errMsg = string.Format(LocalizationManager.Get("Msg_SaveError"), ex.Message);
                MessageBox.Show(errMsg, LocalizationManager.Get("Msg_SaveTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
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

    private static Icon LoadAppIcon()
    {
        // 1. Try extracting associated icon from current PE process executable (Fastest & 100% accurate in dev and single-file publish)
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var assocIcon = Icon.ExtractAssociatedIcon(processPath);
                if (assocIcon != null)
                {
                    return assocIcon;
                }
            }
        }
        catch { }

        // 2. Try loading from WPF pack application resource stream
        try
        {
            var resUri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(resUri);
            if (streamInfo != null)
            {
                using (streamInfo.Stream)
                {
                    return new Icon(streamInfo.Stream, 32, 32);
                }
            }
        }
        catch { }

        // 3. Try loading from filesystem candidates
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "app.ico"),
                Path.Combine(baseDir, "assets", "app.ico"),
                Path.Combine(baseDir, "assets", "app_icon_32.png"),
                Path.Combine(baseDir, "assets", "app_icon.png")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        return new Icon(path, 32, 32);
                    }
                    else
                    {
                        using var img = new Bitmap(path);
                        var hIcon = img.GetHicon();
                        try
                        {
                            using var temp = Icon.FromHandle(hIcon);
                            return (Icon)temp.Clone();
                        }
                        finally
                        {
                            DestroyIcon(hIcon); // Explicitly release native Win32 GDI handle
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIconService] Error loading icon: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _scheduler.OnPauseStateChanged -= _onPauseChangedHandler;
            _scheduler.OnWallpaperChanged -= _onWallpaperChangedHandler;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                if (_trayIcon.ContextMenuStrip != null)
                {
                    ClearAndDisposeMenu(_trayIcon.ContextMenuStrip);
                    _trayIcon.ContextMenuStrip.Dispose();
                }
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_loadedIcon != null)
            {
                _loadedIcon.Dispose();
                _loadedIcon = null;
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
