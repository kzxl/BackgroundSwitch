using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZeroWall.Models;
using Microsoft.Win32;

namespace ZeroWall.Services;

public class MonitorInfoItem
{
    public uint Index { get; set; }
    public string MonitorId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public bool IsPrimary { get; set; }
    public string CurrentWallpaper { get; set; } = string.Empty;
}

public static class WallpaperManager
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorID);
        [return: MarshalAs(UnmanagedType.U4)]
        uint GetMonitorDevicePathCount();
        [return: MarshalAs(UnmanagedType.Struct)]
        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        void SetBackgroundColor([MarshalAs(UnmanagedType.U4)] uint color);
        [return: MarshalAs(UnmanagedType.U4)]
        uint GetBackgroundColor();
        void SetPosition([MarshalAs(UnmanagedType.I4)] int position);
        [return: MarshalAs(UnmanagedType.I4)]
        int GetPosition();
        void SetSlideshow([MarshalAs(UnmanagedType.Interface)] object items);
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetSlideshow();
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, int direction);
        [return: MarshalAs(UnmanagedType.I4)]
        int GetStatus();
        bool Enable();
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaper
    {
    }

    public static List<MonitorInfoItem> GetMonitors()
    {
        var list = new List<MonitorInfoItem>();
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            uint count = desktopWallpaper.GetMonitorDevicePathCount();
            var allScreens = Screen.AllScreens;

            for (uint i = 0; i < count; i++)
            {
                desktopWallpaper.GetMonitorDevicePathAt(i, out var monitorId);
                var rect = desktopWallpaper.GetMonitorRECT(monitorId);
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;

                var matchingScreen = allScreens.FirstOrDefault(s => s.Bounds.Width == w && s.Bounds.Height == h) 
                                      ?? (i < allScreens.Length ? allScreens[i] : null);

                var deviceName = matchingScreen?.DeviceName ?? $"\\\\.\\DISPLAY{i + 1}";
                var isPrimary = matchingScreen?.Primary ?? (i == 0);

                string currentWp = string.Empty;
                try
                {
                    currentWp = desktopWallpaper.GetWallpaper(monitorId) ?? string.Empty;
                }
                catch { }

                list.Add(new MonitorInfoItem
                {
                    Index = i,
                    MonitorId = monitorId,
                    DeviceName = deviceName,
                    FriendlyName = $"Màn hình {i + 1}{(isPrimary ? " (Chính)" : "")} - {w}x{h}",
                    Width = w,
                    Height = h,
                    Left = rect.Left,
                    Top = rect.Top,
                    Right = rect.Right,
                    Bottom = rect.Bottom,
                    IsPrimary = isPrimary,
                    CurrentWallpaper = currentWp
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Error getting monitors via COM: {ex.Message}");
            
            // Fallback to Screen.AllScreens
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                list.Add(new MonitorInfoItem
                {
                    Index = (uint)i,
                    MonitorId = string.Empty,
                    DeviceName = screens[i].DeviceName,
                    FriendlyName = $"Màn hình {i + 1}{(screens[i].Primary ? " (Chính)" : "")} - {screens[i].Bounds.Width}x{screens[i].Bounds.Height}",
                    Width = screens[i].Bounds.Width,
                    Height = screens[i].Bounds.Height,
                    Left = screens[i].Bounds.Left,
                    Top = screens[i].Bounds.Top,
                    Right = screens[i].Bounds.Right,
                    Bottom = screens[i].Bounds.Bottom,
                    IsPrimary = screens[i].Primary,
                    CurrentWallpaper = string.Empty
                });
            }
        }

        return list;
    }

    public static string GetWallpaper(string? monitorId)
    {
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            if (!string.IsNullOrWhiteSpace(monitorId))
            {
                return desktopWallpaper.GetWallpaper(monitorId) ?? string.Empty;
            }

            uint count = desktopWallpaper.GetMonitorDevicePathCount();
            if (count > 0)
            {
                desktopWallpaper.GetMonitorDevicePathAt(0, out var firstId);
                return desktopWallpaper.GetWallpaper(firstId) ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] COM GetWallpaper error: {ex.Message}");
        }
        return string.Empty;
    }

    public static bool SetWallpaper(string? monitorId, string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Image file not found: {imagePath}");
            return false;
        }

        bool comSuccess = false;

        // 1. Try Windows COM IDesktopWallpaper
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();

            if (!string.IsNullOrWhiteSpace(monitorId))
            {
                // CRITICAL: When setting per-monitor wallpaper, ONLY call COM SetWallpaper.
                // NEVER call SystemParametersInfo here as it will broadcast and overwrite ALL monitors!
                desktopWallpaper.SetWallpaper(monitorId, imagePath);
                return true;
            }
            else
            {
                // Synced mode: set on each monitor explicitly
                uint count = desktopWallpaper.GetMonitorDevicePathCount();
                if (count > 0)
                {
                    for (uint i = 0; i < count; i++)
                    {
                        desktopWallpaper.GetMonitorDevicePathAt(i, out var mId);
                        if (!string.IsNullOrEmpty(mId))
                        {
                            desktopWallpaper.SetWallpaper(mId, imagePath);
                        }
                    }
                    comSuccess = true;
                }
                else
                {
                    desktopWallpaper.SetWallpaper(null, imagePath);
                    comSuccess = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] COM SetWallpaper error: {ex.Message}");
        }

        // If COM succeeded for Synced mode, we're done
        if (comSuccess)
        {
            return true;
        }

        // 2. Fallback to Win32 SystemParametersInfo ONLY when monitorId is null (Synced mode fallback)
        if (string.IsNullOrWhiteSpace(monitorId))
        {
            try
            {
                int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                return result != 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperManager] SystemParametersInfo error: {ex.Message}");
            }
        }

        return false;
    }

    public static void SetPosition(WallpaperScale scale, bool refreshImmediately = false)
    {
        // 1. Update Windows Registry for scale style
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                string style = "10"; // Fill
                string tile = "0";
                switch (scale)
                {
                    case WallpaperScale.Center: style = "0"; tile = "0"; break;
                    case WallpaperScale.Tile: style = "0"; tile = "1"; break;
                    case WallpaperScale.Stretch: style = "2"; tile = "0"; break;
                    case WallpaperScale.Fit: style = "6"; tile = "0"; break;
                    case WallpaperScale.Fill: style = "10"; tile = "0"; break;
                    case WallpaperScale.Span: style = "22"; tile = "0"; break;
                }
                key.SetValue("WallpaperStyle", style);
                key.SetValue("TileWallpaper", tile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Registry scale error: {ex.Message}");
        }

        // 2. Apply via COM
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            desktopWallpaper.SetPosition((int)scale);

            if (refreshImmediately)
            {
                RefreshCurrentWallpapers();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] COM SetPosition error: {ex.Message}");
        }
    }

    public static void RefreshCurrentWallpapers()
    {
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            uint count = desktopWallpaper.GetMonitorDevicePathCount();
            if (count > 0)
            {
                for (uint i = 0; i < count; i++)
                {
                    desktopWallpaper.GetMonitorDevicePathAt(i, out var mId);
                    if (!string.IsNullOrEmpty(mId))
                    {
                        string current = desktopWallpaper.GetWallpaper(mId);
                        if (!string.IsNullOrEmpty(current) && File.Exists(current))
                        {
                            desktopWallpaper.SetWallpaper(mId, current);
                        }
                    }
                }
            }
            else
            {
                string current = desktopWallpaper.GetWallpaper(null);
                if (!string.IsNullOrEmpty(current) && File.Exists(current))
                {
                    desktopWallpaper.SetWallpaper(null, current);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Error refreshing wallpapers: {ex.Message}");
        }
    }
}
