using System.IO;
using System.Text.RegularExpressions;
using ZeroWall.Models;

namespace ZeroWall.Services;

public class CacheStats
{
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public double TotalMegabytes => Math.Round(TotalBytes / (1024.0 * 1024.0), 1);
    public string FormattedText => $"{TotalMegabytes:F1} MB ({FileCount} ảnh)";
}

public static class CacheManager
{
    public static readonly string RootTempDirectory = Path.Combine(Path.GetTempPath(), "ZeroWall");

    /// <summary>
    /// Giới hạn số lượng file trong thư mục theo cơ chế cuốn chiếu FIFO (First-In, First-Out).
    /// </summary>
    public static void EnforceRollingLimit(string directoryPath, int maxFiles, ICollection<string>? excludeFiles = null)
    {
        if (maxFiles <= 0 || !Directory.Exists(directoryPath)) return;

        try
        {
            var dir = new DirectoryInfo(directoryPath);
            var files = dir.GetFiles("*.*")
                .Where(f => IsImageFile(f.Extension))
                .ToList();

            if (files.Count <= maxFiles) return;

            // Tập hợp các file cần loại trừ (file đang làm hình nền hoặc vừa tải xong)
            var excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excludeFiles != null)
            {
                foreach (var ex in excludeFiles)
                {
                    if (!string.IsNullOrEmpty(ex)) excludedPaths.Add(ex);
                }
            }

            // Sắp xếp các file theo thời gian tạo/chỉnh sửa từ cũ nhất đến mới nhất
            var sortedFiles = files
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            int toRemoveCount = files.Count - maxFiles;

            foreach (var file in sortedFiles)
            {
                if (toRemoveCount <= 0) break;

                if (!excludedPaths.Contains(file.FullName))
                {
                    try
                    {
                        file.Delete();
                        toRemoveCount--;
                    }
                    catch
                    {
                        // File có thể đang bị lock bởi tiến trình khác, bỏ qua
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheManager] Lỗi khi dọn dẹp cuốn chiếu: {ex.Message}");
        }
    }

    /// <summary>
    /// Thống kê tổng dung lượng và số lượng ảnh trong toàn bộ thư mục tạm ZeroWall.
    /// </summary>
    public static CacheStats GetCacheStats()
    {
        var stats = new CacheStats();
        if (!Directory.Exists(RootTempDirectory)) return stats;

        try
        {
            var dir = new DirectoryInfo(RootTempDirectory);
            foreach (var file in dir.GetFiles("*.*", SearchOption.AllDirectories))
            {
                if (IsImageFile(file.Extension))
                {
                    stats.FileCount++;
                    stats.TotalBytes += file.Length;
                }
            }
        }
        catch { }

        return stats;
    }

    /// <summary>
    /// Xóa toàn bộ file tạm trong thư mục Cache, loại trừ các file đang hoạt động. Trả về số file đã xóa.
    /// </summary>
    public static int ClearAllCache(ICollection<string>? excludeFiles = null)
    {
        if (!Directory.Exists(RootTempDirectory)) return 0;

        int deletedCount = 0;
        var excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludeFiles != null)
        {
            foreach (var ex in excludeFiles)
            {
                if (!string.IsNullOrEmpty(ex)) excludedPaths.Add(ex);
            }
        }

        try
        {
            var dir = new DirectoryInfo(RootTempDirectory);
            foreach (var file in dir.GetFiles("*.*", SearchOption.AllDirectories))
            {
                if (!excludedPaths.Contains(file.FullName))
                {
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return deletedCount;
    }

    /// <summary>
    /// Lưu ảnh vào bộ sưu tập Yêu thích với tên file đặt theo Metadata chuẩn.
    /// </summary>
    public static string? SaveToFavorites(string sourceImagePath, WallpaperMetadata? metadata, string targetFolder)
    {
        if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            return null;
        }

        try
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            string ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            string safeTitle = metadata != null && !string.IsNullOrWhiteSpace(metadata.Title)
                ? SanitizeFileName(metadata.Title)
                : "Wallpaper";

            string safeAuthor = metadata != null && !string.IsNullOrWhiteSpace(metadata.Author)
                ? SanitizeFileName(metadata.Author)
                : string.Empty;

            string fileName;
            if (!string.IsNullOrEmpty(safeAuthor))
            {
                fileName = $"{safeTitle} - {safeAuthor}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            }
            else
            {
                fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            }

            string destinationPath = Path.Combine(targetFolder, fileName);
            File.Copy(sourceImagePath, destinationPath, true);
            return destinationPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheManager] Lỗi khi lưu vào Favorites: {ex.Message}");
            return null;
        }
    }

    private static bool IsImageFile(string ext)
    {
        var e = ext.ToLowerInvariant();
        return e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".webp" || e == ".bmp";
    }

    private static string SanitizeFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string sanitized = Regex.Replace(name, $"[{invalidChars}]", "_");
        if (sanitized.Length > 40)
        {
            sanitized = sanitized.Substring(0, 40).Trim();
        }
        return sanitized;
    }
}
