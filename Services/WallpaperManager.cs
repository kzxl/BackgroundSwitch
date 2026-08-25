using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BackgroundSwitch.Models;

namespace BackgroundSwitch.Services;

public class MonitorInfoItem
{
    public uint Index { get; set; }
    public string MonitorId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
}

public static class WallpaperManager
{
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

                // Match with Screen for device name
                var matchingScreen = allScreens.FirstOrDefault(s => s.Bounds.Width == w && s.Bounds.Height == h) 
                                      ?? (i < allScreens.Length ? allScreens[i] : null);

                var deviceName = matchingScreen?.DeviceName ?? $"\\\\.\\DISPLAY{i + 1}";
                var isPrimary = matchingScreen?.Primary ?? (i == 0);

                list.Add(new MonitorInfoItem
                {
                    Index = i,
                    MonitorId = monitorId,
                    DeviceName = deviceName,
                    FriendlyName = $"Màn hình {i + 1}{(isPrimary ? " (Chính)" : "")} - {w}x{h}",
                    Width = w,
                    Height = h,
                    IsPrimary = isPrimary
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Error getting monitors: {ex.Message}");
            
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
                    IsPrimary = screens[i].Primary
                });
            }
        }

        return list;
    }

    public static void SetWallpaper(string? monitorId, string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath)) return;

        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            desktopWallpaper.SetWallpaper(string.IsNullOrWhiteSpace(monitorId) ? null : monitorId, imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Failed to set wallpaper: {ex.Message}");
        }
    }

    public static void SetPosition(WallpaperScale scale)
    {
        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            desktopWallpaper.SetPosition((int)scale);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Failed to set position: {ex.Message}");
        }
    }
}
