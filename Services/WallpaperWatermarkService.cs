using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using BackgroundSwitch.Models;

namespace BackgroundSwitch.Services;

public static class WallpaperWatermarkService
{
    private static readonly string OverlayDir = Path.Combine(Path.GetTempPath(), "BackgroundSwitch", "DesktopOverlays");

    static WallpaperWatermarkService()
    {
        try
        {
            if (!Directory.Exists(OverlayDir))
            {
                Directory.CreateDirectory(OverlayDir);
            }
        }
        catch { }
    }

    /// <summary>
    /// Tạo một bản sao hình nền có vẽ thông tin Tên tác phẩm & Tác giả ở góc dưới màn hình (Spotlight style).
    /// </summary>
    public static string CreateWatermarkedWallpaper(string originalImagePath, WallpaperMetadata metadata)
    {
        if (string.IsNullOrEmpty(originalImagePath) || !File.Exists(originalImagePath))
        {
            return originalImagePath;
        }

        if (metadata == null || (string.IsNullOrWhiteSpace(metadata.Title) && string.IsNullOrWhiteSpace(metadata.Author)))
        {
            return originalImagePath;
        }

        try
        {
            // Dọn dẹp các file overlay cũ hơn 1 ngày
            CleanupOldOverlays();

            using var original = new Bitmap(originalImagePath);

            // Clone sang định dạng 24bppRgb để render chất lượng cao
            using var canvas = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Vẽ ảnh gốc full resolution
                g.DrawImage(original, 0, 0, original.Width, original.Height);

                // Tỉ lệ font scale theo độ phân giải màn hình (chuẩn hóa theo 1080p)
                float scale = Math.Max(0.8f, original.Width / 1920.0f);

                string titleText = metadata.DisplayTitle;
                string authorText = !string.IsNullOrWhiteSpace(metadata.Author)
                    ? $"Ảnh bởi: {metadata.Author}"
                    : (!string.IsNullOrWhiteSpace(metadata.Provider) ? $"Nguồn: {metadata.Provider}" : string.Empty);

                using var titleFont = new Font("Segoe UI", 16f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                using var authorFont = new Font("Segoe UI", 12.5f * scale, FontStyle.Regular, GraphicsUnit.Pixel);

                var titleSize = g.MeasureString(titleText, titleFont);
                var authorSize = !string.IsNullOrEmpty(authorText) ? g.MeasureString(authorText, authorFont) : SizeF.Empty;

                float contentWidth = Math.Max(titleSize.Width, authorSize.Width);
                float contentHeight = titleSize.Height + (authorSize.Height > 0 ? authorSize.Height + (4 * scale) : 0);

                // Kích thước badge nền mờ (Frosted pill)
                float padH = 16f * scale;
                float padV = 10f * scale;
                float boxWidth = contentWidth + (padH * 2);
                float boxHeight = contentHeight + (padV * 2);

                // Vị trí: Góc dưới bên phải (cách phải 40px, cách đáy 60px để không dính Taskbar)
                float marginX = 40f * scale;
                float marginY = 60f * scale;
                float boxX = original.Width - boxWidth - marginX;
                float boxY = original.Height - boxHeight - marginY;

                var boxRect = new RectangleF(boxX, boxY, boxWidth, boxHeight);
                float cornerRadius = 10f * scale;

                // 1. Vẽ nền bo góc màu tối bán trong suốt (Spotlight Glass Badge)
                using (var path = GetRoundedRectanglePath(boxRect, cornerRadius))
                {
                    using var bgBrush = new SolidBrush(Color.FromArgb(160, 15, 17, 26)); // Dark semi-transparent
                    g.FillPath(bgBrush, path);

                    using var borderPen = new Pen(Color.FromArgb(70, 255, 255, 255), 1.2f * scale);
                    g.DrawPath(borderPen, path);
                }

                // 2. Vẽ Tiêu đề ảnh (Title)
                float textX = boxX + padH;
                float textY = boxY + padV;

                // Text shadow nhẹ tạo chiều sâu
                using var shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                g.DrawString(titleText, titleFont, shadowBrush, textX + (1.2f * scale), textY + (1.2f * scale));

                using var titleBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
                g.DrawString(titleText, titleFont, titleBrush, textX, textY);

                // 3. Vẽ Tác giả (Author) nếu có
                if (!string.IsNullOrEmpty(authorText))
                {
                    float authorY = textY + titleSize.Height + (3 * scale);
                    using var authorShadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
                    g.DrawString(authorText, authorFont, authorShadow, textX + (1f * scale), authorY + (1f * scale));

                    using var authorBrush = new SolidBrush(Color.FromArgb(225, 190, 200, 220)); // Soft pastel accent
                    g.DrawString(authorText, authorFont, authorBrush, textX, authorY);
                }
            }

            string outPath = Path.Combine(OverlayDir, $"wp_overlay_{Guid.NewGuid():N}.jpg");

            // Lưu với chất lượng JPEG 95%
            var encoder = GetEncoder(ImageFormat.Jpeg);
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

            canvas.Save(outPath, encoder, encoderParams);
            return outPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperWatermarkService] Lỗi khi tạo watermark: {ex.Message}");
            return originalImagePath;
        }
    }

    private static GraphicsPath GetRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return codecs[0];
    }

    private static void CleanupOldOverlays()
    {
        try
        {
            if (!Directory.Exists(OverlayDir)) return;

            var threshold = DateTime.UtcNow.AddHours(-12);
            foreach (var file in Directory.GetFiles(OverlayDir, "wp_overlay_*.jpg"))
            {
                try
                {
                    if (File.GetCreationTimeUtc(file) < threshold)
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
