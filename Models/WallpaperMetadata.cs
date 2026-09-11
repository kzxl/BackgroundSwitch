namespace BackgroundSwitch.Models;

public class WallpaperMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : "Không rõ tên tác phẩm";
    public string DisplayAuthor => !string.IsNullOrWhiteSpace(Author) ? Author : "Không rõ tác giả";

    public string DisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Author))
            {
                return $"{Title} • {Author}";
            }
            if (!string.IsNullOrWhiteSpace(Title))
            {
                return Title;
            }
            if (!string.IsNullOrWhiteSpace(Author))
            {
                return $"Ảnh bởi {Author}";
            }
            return "Không có thông tin";
        }
    }
}
