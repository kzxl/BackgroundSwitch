using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using BackgroundSwitch.Models;
using BackgroundSwitch.Services;
using BackgroundSwitch.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace BackgroundSwitch;

public partial class App : Application
{
    private const string MutexName = "Global\\BackgroundSwitch_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;
    private TrayIconService? _trayIconService;
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

        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "BackgroundSwitch_error.log"), $"[{DateTime.Now}] [Unhandled] {ev.ExceptionObject}\n"); } catch { }
        };
        DispatcherUnhandledException += (_, ev) =>
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "BackgroundSwitch_error.log"), $"[{DateTime.Now}] [Dispatcher] {ev.Exception}\n"); } catch { }
            ev.Handled = true;
        };

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

        // 3. Initialize System Tray Service
        _trayIconService = new TrayIconService(_scheduler, ShowMainWindow, ExitApplication);

        // 4. Check Startup Arguments
        bool isHiddenStart = e.Args.Any(a => 
            a.Equals("--hidden", StringComparison.OrdinalIgnoreCase) || 
            a.Equals("/autostart", StringComparison.OrdinalIgnoreCase));

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

    public void ShowMainWindow()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            return;
        }

        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            var viewModel = new MainViewModel(_settings, _scheduler!, () =>
            {
                _mainWindow?.Hide();
                TrimWorkingSetMemory();
            });

            _mainWindow = new MainWindow(viewModel);
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

        if (_trayIconService != null)
        {
            _trayIconService.Dispose();
            _trayIconService = null;
        }

        if (_mainWindow != null)
        {
            _mainWindow.ForceClose();
            _mainWindow = null;
        }

        if (_settings.ClearCacheOnExit)
        {
            try
            {
                var activeFiles = _scheduler?.CurrentWallpapers.Values.ToList();
                CacheManager.ClearAllCache(activeFiles);
            }
            catch { }
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
