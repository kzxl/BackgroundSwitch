using System.Runtime.InteropServices;

namespace BackgroundSwitch.Services;

public static class WallpaperManager
{
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
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaper
    {
    }

    public static void SetWallpaper(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;

        try
        {
            var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            // Setting monitorID to null applies the wallpaper to all monitors in Windows
            desktopWallpaper.SetWallpaper(null, imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperManager] Failed to set wallpaper: {ex.Message}");
        }
    }
}
