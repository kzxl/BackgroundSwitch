using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZeroWall.Services;

public class LiveWallpaperService
{
    private const int WM_SPAWN_WORKER = 0x052C;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus; // 0: Offline, 1: Online, 255: Unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    /// <summary>
    /// Obtains the WorkerW window handle behind desktop icons.
    /// Sends 0x052C to Progman causing Windows DWM to create the WorkerW canvas.
    /// </summary>
    public static IntPtr GetDesktopWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        // Send 0x052C to Progman to split icons from background
        SendMessageTimeout(progman, WM_SPAWN_WORKER, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out _);

        IntPtr workerw = IntPtr.Zero;

        EnumWindows((toplevelHwnd, lParam) =>
        {
            IntPtr shell = FindWindowEx(toplevelHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell != IntPtr.Zero)
            {
                // The WorkerW directly behind SHELLDLL_DefView is the sibling to receive our canvas
                workerw = FindWindowEx(IntPtr.Zero, toplevelHwnd, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        return workerw;
    }

    /// <summary>
    /// Attaches an existing window (e.g. video player or canvas) directly onto the desktop behind icons.
    /// </summary>
    public static bool AttachWindowToDesktop(IntPtr windowHandle, int x, int y, int width, int height)
    {
        if (windowHandle == IntPtr.Zero) return false;

        IntPtr workerw = GetDesktopWorkerW();
        if (workerw == IntPtr.Zero) return false;

        SetParent(windowHandle, workerw);
        MoveWindow(windowHandle, x, y, width, height, true);
        return true;
    }

    /// <summary>
    /// Restores the window back to standard top-level desktop.
    /// </summary>
    public static bool DetachWindowFromDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return false;
        SetParent(windowHandle, IntPtr.Zero);
        return true;
    }

    /// <summary>
    /// Determines whether a foreground application is currently in full-screen mode (e.g. games, presentation).
    /// Used to pause live wallpaper rendering to preserve GPU & CPU.
    /// </summary>
    public static bool IsFullScreenAppActive()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        // Ignore desktop and taskbar windows
        IntPtr desktopHwnd = FindWindow("Progman", null);
        IntPtr shellHwnd = FindWindow("Shell_TrayWnd", null);
        if (fg == desktopHwnd || fg == shellHwnd) return false;

        if (GetWindowRect(fg, out var rect))
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (rect.Left <= screen.Bounds.Left &&
                    rect.Top <= screen.Bounds.Top &&
                    rect.Right >= screen.Bounds.Right &&
                    rect.Bottom >= screen.Bounds.Bottom)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if device is running on battery power (AC offline).
    /// </summary>
    public static bool IsRunningOnBattery()
    {
        if (GetSystemPowerStatus(out var status))
        {
            return status.ACLineStatus == 0;
        }
        return false;
    }

    #region Managed Session Controller

    private ZeroWall.Views.LiveWallpaperWindow? _activeWindow;
    private System.Windows.Threading.DispatcherTimer? _monitorTimer;

    public bool IsActive => _activeWindow != null;
    public bool IsPaused { get; private set; }
    public bool PauseOnFullscreen { get; set; } = true;
    public bool PauseOnBattery { get; set; } = true;
    public double Volume { get; set; } = 0.0;
    public string? CurrentVideoPath { get; private set; }

    public event Action<bool>? StateChanged;

    public bool Start(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            return false;

        Stop();

        CurrentVideoPath = videoPath;
        _activeWindow = new ZeroWall.Views.LiveWallpaperWindow();
        _activeWindow.Show();

        var helper = new System.Windows.Interop.WindowInteropHelper(_activeWindow);
        var primaryScreen = Screen.PrimaryScreen ?? Screen.AllScreens[0];

        bool attached = AttachWindowToDesktop(
            helper.Handle,
            primaryScreen.Bounds.Left,
            primaryScreen.Bounds.Top,
            primaryScreen.Bounds.Width,
            primaryScreen.Bounds.Height);

        if (!attached)
        {
            _activeWindow.Close();
            _activeWindow = null;
            return false;
        }

        _activeWindow.OpenVideo(videoPath, Volume);

        _monitorTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _monitorTimer.Tick += (_, _) =>
        {
            if (_activeWindow == null) return;

            bool shouldPause = (PauseOnFullscreen && IsFullScreenAppActive()) ||
                               (PauseOnBattery && IsRunningOnBattery());

            if (shouldPause && !IsPaused)
            {
                IsPaused = true;
                _activeWindow.Pause();
                StateChanged?.Invoke(false);
            }
            else if (!shouldPause && IsPaused)
            {
                IsPaused = false;
                _activeWindow.Play();
                StateChanged?.Invoke(true);
            }
        };
        _monitorTimer.Start();

        StateChanged?.Invoke(true);
        return true;
    }

    public void Stop()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;

        if (_activeWindow != null)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(_activeWindow);
            DetachWindowFromDesktop(helper.Handle);
            _activeWindow.Close();
            _activeWindow = null;
        }

        IsPaused = false;
        StateChanged?.Invoke(false);
    }

    #endregion
}
