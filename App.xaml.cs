using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using H.NotifyIcon;
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
            // Another instance is already running
            System.Windows.MessageBox.Show("BackgroundSwitch is already running in the System Tray.", "BackgroundSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 2. Load Settings & Init Scheduler
        _settings = AppSettings.Load();
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

    private void InitTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = SystemIcons.Information,
            ToolTipText = "BackgroundSwitch - Auto Wallpaper Changer"
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        var contextMenu = new ContextMenu();

        var menuOpen = new MenuItem { Header = "⚙️ Cài đặt & Cấu hình" };
        menuOpen.Click += (_, _) => ShowMainWindow();

        var menuChangeNow = new MenuItem { Header = "🔄 Đổi hình nền ngay" };
        menuChangeNow.Click += async (_, _) =>
        {
            if (_scheduler != null)
            {
                await _scheduler.ChangeWallpaperAsync();
            }
        };

        var menuExit = new MenuItem { Header = "❌ Thoát" };
        menuExit.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(menuOpen);
        contextMenu.Items.Add(menuChangeNow);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(menuExit);

        _trayIcon.ContextMenu = contextMenu;
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
