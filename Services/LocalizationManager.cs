namespace BackgroundSwitch.Services;

public enum AppLanguage
{
    Bilingual = 0, // Song ngữ: "Next Background (Ảnh tiếp theo)"
    Vietnamese = 1,
    English = 2
}

public static class LocalizationManager
{
    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Bilingual;

    private static readonly Dictionary<string, (string en, string vi)> Strings = new()
    {
        // Tray Context Menu Items
        { "Menu_Next", ("Next Background", "Ảnh tiếp theo") },
        { "Menu_Previous", ("Previous Background", "Ảnh trước đó") },
        { "Menu_Pause", ("Pause Changer", "Tạm dừng đổi ảnh") },
        { "Menu_Resume", ("Resume Changer", "Tiếp tục đổi ảnh") },
        { "Menu_SaveAs", ("Save Picture As...", "Lưu ảnh này về máy...") },
        { "Menu_ViewCurrent", ("View Current Picture", "Xem file ảnh gốc") },
        { "Menu_NeverShowAgain", ("Never Show Again", "Không bao giờ hiển thị lại ảnh này") },
        { "Menu_OpenCache", ("Open Cache Folder", "Mở thư mục Cache ảnh") },
        { "Menu_MultiMonitor", ("Multi-Monitor", "Đa màn hình") },
        { "Menu_MonitorNext", ("Next on", "Đổi ảnh") },
        { "Menu_SyncAll", ("Sync All Monitors", "Đồng bộ tất cả màn hình") },
        { "Menu_ClearBackground", ("Clear Background", "Xóa hình nền") },
        { "Menu_Settings", ("Settings...", "Cài đặt & Cấu hình...") },
        { "Menu_Exit", ("Exit", "Thoát") },

        // MainWindow UI Labels
        { "UI_Title", ("BackgroundSwitch — Multi-Monitor Wallpaper Changer", "BackgroundSwitch — Tự Động Đổi Hình Nền Đa Màn Hình") },
        { "UI_Subtitle", ("High-Performance Multi-Monitor Wallpaper Switcher (.NET 10)", "Tự động đổi hình nền đa màn hình & Tối ưu hiệu năng cao (.NET 10)") },
        { "UI_ModeSection", ("🖥️ Display Mode & Scaling", "🖥️ Chế độ Hiển thị & Căn chỉnh") },
        { "UI_ModeSynced", ("Synced (1 for all)", "Đồng bộ (1 ảnh cho tất cả)") },
        { "UI_ModePerMonitor", ("Per-Monitor (Individual)", "Mỗi màn hình 1 ảnh riêng") },
        { "UI_ModeSpan", ("Span across monitors", "Trải rộng (Span)") },
        { "UI_ScaleLabel", ("Scale:", "Căn chỉnh:") },
        { "UI_SourceSection", ("🌐 Wallpaper Source", "🌐 Nguồn hình nền") },

        // Providers
        { "UI_SourceBing", ("Bing Daily 4K", "Bing Daily 4K (Tự động)") },
        { "UI_SourceReddit", ("Reddit Wallpapers", "Reddit Wallpapers (r/...)") },
        { "UI_SourceWallhaven", ("Wallhaven 4K/8K", "Wallhaven 4K/8K") },
        { "UI_SourceNasa", ("NASA APOD Space", "NASA Vũ trụ (APOD)") },
        { "UI_SourceLocal", ("Local Folder", "Thư mục Local") },
        { "UI_SourcePexels", ("Pexels API", "Pexels API") },
        { "UI_SourceUnsplash", ("Unsplash Photos", "Unsplash Photos") },

        // Provider Options & Descriptions
        { "UI_LocalFolderPath", ("Local Folder Path:", "Đường dẫn thư mục ảnh:") },
        { "UI_BrowseBtn", ("📁 Browse", "📁 Chọn") },
        { "UI_PexelsApiKey", ("Pexels API Key:", "Pexels API Key:") },
        { "UI_PexelsQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa chủ đề (Hỗ trợ nhiều từ khóa):") },
        { "UI_RedditSub", ("Subreddits (Multi-sub supported, e.g. wallpapers, EarthPorn, spaceporn):", "Tên Subreddit (Hỗ trợ nhiều sub, vd: wallpapers, EarthPorn, spaceporn):") },
        { "UI_WallhavenQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa tìm kiếm (Hỗ trợ nhiều từ khóa):") },
        { "UI_WallhavenApiKey", ("Wallhaven API Key (Optional for NSFW):", "Wallhaven API Key (Tùy chọn):") },
        { "UI_UnsplashQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa tìm kiếm (Hỗ trợ nhiều từ khóa):") },
        { "UI_UnsplashApiKey", ("Unsplash Access Key (Optional):", "Unsplash Access Key (Tùy chọn):") },
        { "UI_TopicModeLabel", ("Topic Selection Mode:", "Chế độ duyệt chủ đề:") },
        { "UI_TopicModeRandom", ("🔀 Random (Ngẫu nhiên)", "🔀 Ngẫu nhiên") },
        { "UI_TopicModeSequential", ("🔁 Sequential (Lần lượt theo thứ tự)", "🔁 Lần lượt") },
        { "UI_TopicHelp", ("💡 Enter multiple topics separated by commas (e.g. nature, cyberpunk, anime, space)", "💡 Nhập nhiều chủ đề cách nhau bằng dấu phẩy (vd: nature, cyberpunk, anime, space)") },
        { "UI_ClearTopicsBtn", ("🧹 Clear All", "🧹 Xóa hết") },

        { "UI_LocalDesc", ("📁 Scan and set wallpapers directly from your local computer folder.", "📁 Quét và đặt hình nền trực tiếp từ thư mục trên máy tính của bạn.") },
        { "UI_PexelsDesc", ("📷 High-resolution photography from Pexels API by keywords (Default API key configured).", "📷 Tự động tải ảnh chất lượng cao từ Pexels API theo từ khóa (Đã cài sẵn API key mặc định).") },
        { "UI_BingDesc", ("✨ Automatically downloads Microsoft Bing's Ultra HD wallpaper every day. No API key required.", "✨ Tự động tải hình ảnh Ultra HD chất lượng cao mỗi ngày của Microsoft Bing. Không cần API key.") },
        { "UI_RedditDesc", ("✨ Fetches high-resolution wallpapers from Reddit communities. No API key required.", "✨ Tự động lấy ảnh độ phân giải cao từ cộng đồng Reddit. Không cần API key.") },
        { "UI_WallhavenDesc", ("✨ World's largest 2K/4K/8K desktop wallpaper collection by tags & categories.", "✨ Kho hình nền 2K/4K/8K từ Wallhaven theo từ khóa và chủ đề.") },
        { "UI_NasaDesc", ("✨ NASA's official Astronomy Picture of the Day (James Webb & Hubble telescopes).", "✨ Ảnh thiên văn và vũ trụ chính thức mỗi ngày của NASA (kính James Webb & Hubble).") },
        { "UI_UnsplashDesc", ("✨ High-resolution artistic landscape photography from top photographers.", "✨ Nhiếp ảnh nghệ thuật phong cảnh độ phân giải cao từ các nhiếp ảnh gia hàng đầu.") },

        // General
        { "UI_PerMonitorSection", ("🎛️ Per-Monitor Configuration", "🎛️ Cấu hình từng Màn hình") },
        { "UI_GeneralSection", ("⚙️ General Settings", "⚙️ Cài đặt Chung & Vận hành") },
        { "UI_IntervalLabel", ("Change Interval (Minutes):", "Tần suất đổi (Phút):") },
        { "UI_MinutesUnit", ("minutes", "phút") },
        { "UI_AutoStart", ("Start with Windows (100% silent & zero-flicker)", "Khởi động cùng Windows (Chạy ngầm 100% không chớp màn hình)") },
        { "UI_LanguageLabel", ("Language:", "Ngôn ngữ:") },
        { "UI_BlacklistInfo", ("Blacklisted Images:", "Số ảnh đã chặn (Blacklist):") },
        { "UI_ClearBlacklistBtn", ("🧹 Clear Blacklist", "🧹 Xóa danh sách chặn") },
        { "UI_ChangeNowBtn", ("🔄 Change Wallpaper Now", "🔄 Đổi hình nền ngay") },
        { "UI_SaveBtn", ("💾 Save & Apply", "💾 Lưu & Áp dụng") },

        // Messages
        { "Msg_SaveSuccess", ("Settings saved successfully!", "Cài đặt đã được lưu thành công!") },
        { "Msg_BlacklistSuccess", ("Image added to Blacklist and switched to next wallpaper.", "Đã thêm ảnh vào danh sách chặn và chuyển sang ảnh tiếp theo.") },
        { "Msg_ClearBlacklistConfirm", ("Are you sure you want to clear all blacklisted images?", "Bạn có chắc chắn muốn xóa toàn bộ danh sách ảnh đã chặn không?") },
        { "Msg_ClearBlacklistDone", ("Blacklist cleared successfully!", "Đã xóa danh sách chặn thành công!") }
    };

    public static string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var pair))
        {
            return key;
        }

        return CurrentLanguage switch
        {
            AppLanguage.Vietnamese => pair.vi,
            AppLanguage.English => pair.en,
            _ => $"{pair.en} ({pair.vi})" // Bilingual mode
        };
    }

    public static string GetEn(string key) => Strings.TryGetValue(key, out var pair) ? pair.en : key;
    public static string GetVi(string key) => Strings.TryGetValue(key, out var pair) ? pair.vi : key;
}
