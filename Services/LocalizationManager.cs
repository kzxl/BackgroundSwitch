using System.Text.RegularExpressions;

namespace BackgroundSwitch.Services;

public enum AppLanguage
{
    Bilingual = 0, // Song ngữ chuẩn: "Tiếng Việt (English)"
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
        { "Tray_TooltipActive", ("ZeroWall — Active", "ZeroWall — Đang chạy") },
        { "Tray_TooltipPaused", ("ZeroWall — Paused", "ZeroWall — Đang tạm dừng") },
        { "Msg_NoActiveWallpaper", ("No active wallpaper to save.", "Không có hình nền nào đang hoạt động để lưu.") },
        { "Msg_SaveSuccess", ("Wallpaper saved successfully!", "Đã lưu hình nền thành công!") },
        { "Msg_SaveError", ("Could not save image: {0}", "Không thể lưu hình ảnh: {0}") },
        { "Msg_SaveTitle", ("Save Wallpaper", "Lưu Hình Nền") },

        // MainWindow Navigation & Header
        { "UI_Title", ("ZeroWall — Dynamic Wallpaper Studio", "ZeroWall — Trình Quản Lý Hình Nền Đa Năng") },
        { "UI_Subtitle", ("ZeroUniverse Dynamic Wallpaper & Screensaver Studio (.NET 10)", "Studio hình nền đa màn hình & screensaver thế hệ mới ZeroUniverse (.NET 10)") },
        { "UI_NavSources", ("🌐 Sources", "🌐 Nguồn Hình Nền") },
        { "UI_NavDisplays", ("🖥️ Displays & Layout", "🖥️ Màn Hình & Bố Cục") },
        { "UI_NavSettings", ("⚙️ Schedule & Settings", "⚙️ Lịch Trình & Cài Đặt") },
        { "UI_PexelsDefaultBadge", ("● Pexels API Default", "● Pexels API Mặc Định") },
        { "UI_LanguageLabel", ("Language:", "Ngôn ngữ:") },

        // Live Preview Hero Bar
        { "UI_PreviewPlaceholder", ("📷 Preview", "📷 Xem trước") },
        { "UI_FavoriteBtn", ("❤️ Favorite", "❤️ Yêu thích") },
        { "UI_SavePictureBtn", ("💾 Save Picture", "💾 Lưu ảnh") },
        { "UI_ViewFileBtn", ("🔍 View File", "🔍 Xem file") },
        { "UI_BlacklistBtn", ("🚫 Blacklist", "🚫 Chặn ảnh này") },

        // Tab 0: Sources - Section & Card Headers
        { "UI_SelectSourceTitle", ("SELECT WALLPAPER SOURCE", "CHỌN NGUỒN CUNG CẤP HÌNH NỀN") },
        { "UI_SourceSection", ("🌐 Wallpaper Source", "🌐 Nguồn hình nền") },
        { "UI_SourceBing", ("Bing Daily 4K", "Bing Daily 4K") },
        { "UI_SourceReddit", ("Reddit Wallpapers", "Reddit Wallpapers") },
        { "UI_SourceWallhaven", ("Wallhaven 4K/8K", "Wallhaven 4K/8K") },
        { "UI_SourceNasa", ("NASA APOD Space", "NASA APOD Vũ Trụ") },
        { "UI_SourceLocal", ("Local Folder", "Thư mục Local") },
        { "UI_SourcePexels", ("Pexels API", "Pexels API") },
        { "UI_SourceUnsplash", ("Unsplash Photos", "Unsplash Photos") },

        // Source Card Title Headers
        { "UI_CardTitlePexels", ("📷 Pexels API", "📷 Pexels API") },
        { "UI_CardTitleBing", ("🌅 Bing Daily 4K", "🌅 Bing Daily 4K") },
        { "UI_CardTitleWallhaven", ("🎨 Wallhaven", "🎨 Wallhaven") },
        { "UI_CardTitleNasa", ("🚀 NASA APOD", "🚀 NASA APOD") },
        { "UI_CardTitleUnsplash", ("🌄 Unsplash Photos", "🌄 Unsplash Photos") },
        { "UI_CardTitleReddit", ("🤖 Reddit Wallpapers", "🤖 Reddit Wallpapers") },
        { "UI_CardTitleLocal", ("📁 Local Folder", "📁 Thư Mục Local") },

        // Badges on cards
        { "UI_BadgeUltraHd", ("Ultra HD 4K", "Ultra HD 4K") },
        { "UI_BadgeDaily", ("Daily", "Mỗi ngày 1 ảnh") },
        { "UI_BadgeArt", ("4K/8K Art", "Nghệ thuật 4K/8K") },
        { "UI_BadgeSpace", ("Universe", "Vũ Trụ") },
        { "UI_BadgeCommunity", ("Community", "Cộng Đồng") },
        { "UI_BadgeOffline", ("Offline", "Ngoại tuyến") },

        // Card Descriptions
        { "UI_CardPexelsDesc", ("Global photographer community by keywords (Pre-configured API key).", "Kho ảnh nhiếp ảnh gia toàn cầu theo từ khóa (Đã sẵn key).") },
        { "UI_CardBingDesc", ("Official Microsoft Bing Ultra HD wallpaper refreshed daily.", "Hình nền Ultra HD chính thức mỗi ngày của Microsoft Bing.") },
        { "UI_CardWallhavenDesc", ("High-end anime, cyberpunk, and artistic wallpapers in 4K/8K.", "Kho hình nền nghệ thuật, anime và cyberpunk chất lượng đỉnh cao.") },
        { "UI_CardNasaDesc", ("Spectacular deep space imagery from James Webb & Hubble telescopes.", "Ảnh thiên văn kỳ vĩ từ kính viễn vọng không gian James Webb & Hubble.") },
        { "UI_CardUnsplashDesc", ("Artistic landscape & nature photography from world-class creators.", "Nhiếp ảnh phong cảnh & tự nhiên từ các nhiếp ảnh gia hàng đầu thế giới.") },
        { "UI_CardRedditDesc", ("Auto-fetch top wallpapers from r/wallpapers, EarthPorn communities.", "Tự động lấy ảnh độ phân giải cao từ các cộng đồng r/wallpapers, EarthPorn.") },
        { "UI_CardLocalDesc", ("Scan and randomize pictures from your local PC folder.", "Quét và đổi ngẫu nhiên ảnh trong thư mục ảnh trên máy tính của bạn.") },

        // Provider Configuration Panels
        { "UI_ConfigPexelsTitle", ("⚙️ PEXELS API CONFIGURATION", "⚙️ CẤU HÌNH PEXELS API") },
        { "UI_KeyValid", ("🟢 Valid Key (Pre-configured)", "🟢 Key hợp lệ (Đã cài sẵn trong app)") },
        { "UI_QuickTopicHint", ("QUICK TOPIC SUGGESTIONS:", "GỢI Ý CHỦ ĐỀ NHANH:") },
        { "UI_PexelsApiKey", ("Pexels API Key:", "Pexels API Key:") },
        { "UI_PexelsQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa chủ đề (Hỗ trợ nhiều từ khóa):") },
        { "UI_PexelsKeyHelp", ("💡 This API Key is pre-stored in the application. You may leave it blank or customize anytime.", "💡 API Key này đã được lưu sẵn trong ứng dụng. Bạn có thể để trống hoặc thay đổi bất kỳ lúc nào.") },

        { "UI_ConfigWallhavenTitle", ("⚙️ WALLHAVEN 4K/8K CONFIGURATION", "⚙️ CẤU HÌNH WALLHAVEN 4K/8K") },
        { "UI_WallhavenQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa tìm kiếm (Hỗ trợ nhiều từ khóa):") },
        { "UI_WallhavenApiKey", ("Wallhaven API Key (Optional for NSFW):", "Wallhaven API Key (Tùy chọn cho NSFW):") },

        { "UI_ConfigRedditTitle", ("⚙️ REDDIT WALLPAPERS CONFIGURATION", "⚙️ CẤU HÌNH REDDIT WALLPAPERS") },
        { "UI_RedditSub", ("Subreddits (Multi-sub supported, e.g. wallpapers, EarthPorn, spaceporn):", "Tên Subreddit (Hỗ trợ nhiều sub, vd: wallpapers, EarthPorn, spaceporn):") },

        { "UI_ConfigLocalTitle", ("⚙️ LOCAL FOLDER CONFIGURATION", "⚙️ CẤU HÌNH THƯ MỤC ẢNH CỤC BỘ") },
        { "UI_LocalFolderPath", ("Local Folder Path:", "Đường dẫn thư mục ảnh:") },
        { "UI_BrowseBtn", ("📁 Browse", "📁 Chọn") },

        { "UI_ConfigUnsplashTitle", ("⚙️ UNSPLASH PHOTOS CONFIGURATION", "⚙️ CẤU HÌNH UNSPLASH PHOTOS") },
        { "UI_UnsplashQuery", ("Search Keywords (Multi-topic supported):", "Từ khóa tìm kiếm (Hỗ trợ nhiều từ khóa):") },
        { "UI_UnsplashApiKey", ("Unsplash Access Key (Optional):", "Unsplash Access Key (Tùy chọn):") },

        { "UI_BingNoConfigHint", ("✨ Bing Daily automatically syncs 4K wallpapers from Microsoft. No setup required.", "✨ Nguồn Bing Daily tự động đồng bộ ảnh 4K mỗi ngày từ Microsoft. Không cần cấu hình thêm.") },
        { "UI_NasaNoConfigHint", ("✨ NASA APOD fetches daily astronomical photos from James Webb & Hubble via NASA Open API.", "✨ Nguồn NASA APOD tự động lấy ảnh thiên văn hàng ngày từ kính James Webb & Hubble qua NASA Open API.") },

        { "UI_TopicModeLabel", ("Topic Selection Mode:", "Chế độ duyệt chủ đề:") },
        { "UI_TopicModeRandom", ("🔀 Random", "🔀 Ngẫu nhiên") },
        { "UI_TopicModeSequential", ("🔁 Sequential", "🔁 Lần lượt") },
        { "UI_TopicHelp", ("💡 Enter multiple topics separated by commas (e.g. nature, cyberpunk, anime, space)", "💡 Nhập nhiều chủ đề cách nhau bằng dấu phẩy (vd: nature, cyberpunk, anime, space)") },
        { "UI_ClearTopicsBtn", ("🧹 Clear All", "🧹 Xóa hết") },
        { "UI_TestSampleBtn", ("⚡ Test Sample Preview", "⚡ Xem thử 1 ảnh mẫu") },
        { "UI_TestSampleSuccess", ("Sample image loaded for preview (Desktop unchanged).", "Đã tải ảnh mẫu để xem trước (Chưa đổi màn hình).") },
        { "UI_FrequencySliderLabel", ("Adjust Frequency Slider:", "Thanh trượt tùy chỉnh tần suất:") },
        { "UI_NextCountdown", ("Next switch in:", "Tự động đổi sau:") },

        // Provider Summary Descriptions (Used when switching sources)
        { "UI_LocalDesc", ("📁 Scan and set wallpapers directly from your local computer folder.", "📁 Quét và đặt hình nền trực tiếp từ thư mục trên máy tính của bạn.") },
        { "UI_PexelsDesc", ("📷 High-resolution photography from Pexels API by keywords (Default API key configured).", "📷 Tự động tải ảnh chất lượng cao từ Pexels API theo từ khóa (Đã cài sẵn API key mặc định).") },
        { "UI_BingDesc", ("✨ Automatically downloads Microsoft Bing's Ultra HD wallpaper every day. No API key required.", "✨ Tự động tải hình ảnh Ultra HD chất lượng cao mỗi ngày của Microsoft Bing. Không cần API key.") },
        { "UI_RedditDesc", ("✨ Fetches high-resolution wallpapers from Reddit communities. No API key required.", "✨ Tự động lấy ảnh độ phân giải cao từ cộng đồng Reddit. Không cần API key.") },
        { "UI_WallhavenDesc", ("✨ World's largest 2K/4K/8K desktop wallpaper collection by tags & categories.", "✨ Kho hình nền 2K/4K/8K từ Wallhaven theo từ khóa và chủ đề.") },
        { "UI_NasaDesc", ("✨ NASA's official Astronomy Picture of the Day (James Webb & Hubble telescopes).", "✨ Ảnh thiên văn và vũ trụ chính thức mỗi ngày của NASA (kính James Webb & Hubble).") },
        { "UI_UnsplashDesc", ("✨ High-resolution artistic landscape photography from top photographers.", "✨ Nhiếp ảnh nghệ thuật phong cảnh độ phân giải cao từ các nhiếp ảnh gia hàng đầu thế giới.") },

        // Tab 1: Displays & Layout
        { "UI_ModeSection", ("Display Mode & Scaling", "Chế độ Hiển thị & Căn chỉnh") },
        { "UI_ModeSynced", ("Synced (1 for all)", "Đồng bộ (1 ảnh chung)") },
        { "UI_ModePerMonitor", ("Per-Monitor (Individual)", "Mỗi màn hình 1 ảnh riêng") },
        { "UI_ModeSpan", ("Span across monitors", "Trải rộng qua các màn hình") },
        { "UI_ScaleLabel", ("Scale:", "Căn chỉnh:") },
        { "UI_ScaleFill", ("Fill — Crop to screen (Recommended)", "Fill — Cắt vừa khung màn hình (Khuyên dùng)") },
        { "UI_ScaleFit", ("Fit — Maintain aspect ratio", "Fit — Giữ nguyên tỉ lệ ảnh") },
        { "UI_ScaleStretch", ("Stretch — Stretch to fill", "Stretch — Kéo dãn full khung") },
        { "UI_ScaleCenter", ("Center — Center on screen", "Center — Đặt ở chính giữa") },
        { "UI_ScaleTile", ("Tile — Repeat pattern", "Tile — Lặp lại dạng ô cờ") },
        { "UI_ScaleSpan", ("Span — Span across all monitors", "Span — Trải rộng qua các màn hình") },
        { "UI_DetectedMonitorsTitle", ("DISPLAY TOPOLOGY & PER-MONITOR SETUP", "SƠ ĐỒ MÀN HÌNH & CẤU HÌNH ĐỘC LẬP") },
        { "UI_ConnectedStatus", ("Connected", "Đang kết nối") },
        { "UI_MonitorSourceLabel", ("Wallpaper Source:", "Nguồn hình nền:") },
        { "UI_SourceUseGlobal", ("Sync with Global Source", "Dùng chung với Cấu hình chính (Đồng bộ)") },
        { "UI_ChangeThisMonitorBtn", ("🔄 Change this screen", "🔄 Đổi ảnh màn này") },
        { "UI_PrimaryBadge", ("Primary", "Màn chính") },
        { "UI_MonitorKeywordsLabel", ("Keywords / Query:", "Từ khóa tìm kiếm:") },
        { "UI_MonitorFolderLabel", ("Folder Path:", "Đường dẫn thư mục:") },
        { "UI_PerMonitorActiveBanner", ("✨ Per-Monitor mode active: You can assign distinct sources, keywords, and independently rotate wallpapers for each monitor.", "✨ Chế độ riêng từng màn hình đang bật: Bạn có thể chọn nguồn, từ khóa riêng và đổi ảnh độc lập cho từng màn hình.") },
        { "UI_SyncedActiveBanner", ("🔒 Synced mode active: All monitors display the same wallpaper from the Global Source configured in the Sources tab.", "🔒 Chế độ đồng bộ đang bật: Tất cả màn hình dùng chung 1 ảnh theo Cấu hình chính tại Tab Nguồn ảnh.") },
        { "UI_CurrentWallpaperLabel", ("Current:", "Ảnh hiện tại:") },
        { "UI_NoWallpaperAssigned", ("(No wallpaper applied yet)", "(Chưa áp dụng hình nền)") },

        // Tab 2: Schedule & Settings
        { "UI_IntervalSectionTitle", ("AUTOMATIC WALLPAPER SWITCH INTERVAL", "TẦN SUẤT ĐỔI HÌNH NỀN TỰ ĐỘNG") },
        { "UI_IntervalLabel", ("Change Interval (Minutes):", "Tần suất đổi (Phút):") },
        { "UI_MinutesUnit", ("minutes", "phút") },
        { "UI_QuickIntervalTitle", ("QUICK PRESET INTERVALS:", "CHỌN NHANH TẦN SUẤT:") },
        { "UI_Preset15Min", ("15 min", "15 phút") },
        { "UI_Preset30Min", ("30 min", "30 phút") },
        { "UI_Preset1Hour", ("1 hour", "1 giờ") },
        { "UI_Preset2Hour", ("2 hours", "2 giờ") },
        { "UI_Preset4Hour", ("4 hours", "4 giờ") },
        { "UI_Preset24Hour", ("24 hours", "24 giờ") },
        { "UI_AutoStart", ("Start with Windows", "Khởi động cùng Windows") },

        { "UI_MetadataSectionTitle", ("PHOTO METADATA DISPLAY", "HIỂN THỊ TÊN ẢNH & TÁC GIẢ") },
        { "UI_ShowWallpaperInfoOnDesktop", ("Show photo title & author on Desktop (Watermark)", "Hiển thị tên ảnh & tác giả ở góc màn hình (Watermark)") },
        { "UI_SpotlightHelpText", ("💡 Automatically renders an artistic frosted glass badge at the bottom corner of your Desktop in full original resolution.", "💡 Tự động kết xuất thẻ chữ mờ bo góc nghệ thuật (Spotlight Glass Badge) ở góc dưới màn hình Desktop với độ phân giải gốc của ảnh.") },
        { "UI_ShowWallpaperInfoInApp", ("Show photo title & author in App & notifications", "Hiển thị tên ảnh & tác giả trong ứng dụng & thông báo") },
        { "UI_AppMetaHelpText", ("💡 Shows photo title, photographer and provider on Live Preview bar and notification toasts.", "💡 Hiển thị tên tác phẩm, nhiếp ảnh gia và nguồn ảnh trên thanh Live Preview và thông báo khi đổi ảnh.") },

        { "UI_CacheSection", ("Cache & Storage Policy", "Quản Lý Bộ Nhớ Đệm & Lưu Trữ") },
        { "UI_MaxCachedImages", ("Max rolling cache images per source:", "Số lượng ảnh tạm cuốn chiếu tối đa:") },
        { "UI_CacheHelpText", ("💡 Automatically purges oldest cache files (FIFO) when limit is reached to save disk space.", "💡 Tự động xóa ảnh tạm cũ nhất (cuốn chiếu FIFO) khi tải ảnh mới để tránh tốn dung lượng ổ cứng.") },
        { "UI_ClearCacheOnExit", ("Clear temporary cache automatically on exit", "Tự động dọn sạch bộ nhớ đệm tạm khi đóng ứng dụng") },
        { "UI_CurrentCacheUsage", ("Current Cache Usage:", "Dung lượng bộ nhớ đệm tạm hiện tại:") },
        { "UI_ClearCacheNowBtn", ("🧹 Clear Cache", "🧹 Dọn sạch Cache") },
        { "UI_OpenFavoritesFolderBtn", ("📂 Open Favorites", "📂 Mở thư mục Yêu thích") },

        { "UI_BlacklistSectionTitle", ("BLACKLISTED IMAGES", "DANH SÁCH CHẶN (BLACKLIST)") },
        { "UI_BlacklistInfo", ("Blacklisted Images:", "Số ảnh đã chặn (Blacklist):") },
        { "UI_BlacklistHelpText", ("Blocked wallpapers will never appear on your screens again.", "Những ảnh bị chặn sẽ không bao giờ xuất hiện lại trên màn hình của bạn.") },
        { "UI_ClearBlacklistBtn", ("🧹 Clear Blacklist", "🧹 Xóa danh sách chặn") },

        // Bottom Sticky Action Bar
        { "UI_TrayActiveHint", ("⚡ Running in System Tray", "⚡ Đang chạy ngầm trên System Tray") },
        { "UI_TrayActionHint", ("Double-click or right-click tray icon for actions", "Bấm đúp hoặc chuột phải vào icon để thao tác") },
        { "UI_ChangeNowBtn", ("🔄 Change Wallpaper Now", "🔄 Đổi hình nền ngay") },
        { "UI_SaveBtn", ("💾 Save & Apply", "💾 Lưu & Áp dụng") },

        // Messages
        { "Msg_SaveSuccess", ("Settings saved successfully!", "Cài đặt đã được lưu thành công!") },
        { "Msg_FavoriteSaved", ("Added current wallpaper to Favorites collection!", "Đã thêm ảnh hiện tại vào bộ sưu tập Yêu thích!") },
        { "Msg_CacheCleared", ("Temporary cache cleaned up successfully!", "Đã dọn dẹp sạch bộ nhớ đệm tạm thành công!") },
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
            _ => FormatBilingual(pair.en, pair.vi)
        };
    }

    /// <summary>
    /// Thuật toán định dạng song ngữ chuẩn (Bilingual):
    /// - Chỉ giữ 1 icon/emoji đại diện ở đầu chuỗi (không lặp icon)
    /// - Tách tiền tố chung nếu có (ví dụ: "BackgroundSwitch — ", "Fill — ") để không bị lặp từ
    /// - Không lặp từ nếu tiếng Anh và tiếng Việt giống nhau
    /// - Xử lý thông minh phần chú thích trong ngoặc đơn (...)
    /// - Định dạng thanh lịch "Tiếng Việt (English)" cho label/button ngắn
    /// </summary>
    private static string FormatBilingual(string en, string vi)
    {
        if (string.IsNullOrWhiteSpace(en)) return vi ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vi) || string.Equals(en.Trim(), vi.Trim(), StringComparison.OrdinalIgnoreCase))
            return en;

        string emoji = "";
        string cleanEn = en.Trim();
        string cleanVi = vi.Trim();

        // 1. Tách icon/emoji Unicode ở đầu chuỗi (chỉ giữ 1 icon đại diện duy nhất)
        var emojiPattern = @"^([\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF]|[\u2300-\u23FF]|[\u2B50-\u2B55]|[\uFE00-\uFE0F]|[\u200D]|[\u20E3]|[\u2190-\u21FF]|[●•✨📷🌅🎨🚀🌄🤖📁⚙️⏱️🏷️💾❤️🔄🔍🚫🧹🔀🔁💡🎛️🖥️🌐])+\s*";
        var matchEn = Regex.Match(cleanEn, emojiPattern);
        if (matchEn.Success)
        {
            emoji = matchEn.Value.Trim() + " ";
            cleanEn = cleanEn.Substring(matchEn.Length).Trim();
        }

        var matchVi = Regex.Match(cleanVi, emojiPattern);
        if (matchVi.Success)
        {
            if (string.IsNullOrEmpty(emoji))
            {
                emoji = matchVi.Value.Trim() + " ";
            }
            cleanVi = cleanVi.Substring(matchVi.Length).Trim();
        }

        if (string.Equals(cleanEn, cleanVi, StringComparison.OrdinalIgnoreCase))
        {
            return $"{emoji}{cleanVi}".Trim();
        }

        // 2. Tách tiền tố chung nếu cả 2 cùng có dạng "Prefix — " (vd: "BackgroundSwitch — ", "Fill — ")
        string commonPrefix = "";
        int dashVi = cleanVi.IndexOf(" — ");
        int dashEn = cleanEn.IndexOf(" — ");
        if (dashVi > 0 && dashEn > 0)
        {
            string pVi = cleanVi.Substring(0, dashVi).Trim();
            string pEn = cleanEn.Substring(0, dashEn).Trim();
            if (string.Equals(pVi, pEn, StringComparison.OrdinalIgnoreCase))
            {
                commonPrefix = pVi + " — ";
                cleanVi = cleanVi.Substring(dashVi + 3).Trim();
                cleanEn = cleanEn.Substring(dashEn + 3).Trim();
            }
        }

        // 3. Xử lý chú thích trong ngoặc đơn (...) ở cuối chuỗi
        var parenViMatch = Regex.Match(cleanVi, @"\s*\(([^)]+)\)$");
        var parenEnMatch = Regex.Match(cleanEn, @"\s*\(([^)]+)\)$");
        if (parenViMatch.Success && parenEnMatch.Success)
        {
            string mainVi = cleanVi.Substring(0, parenViMatch.Index).Trim();
            string mainEn = cleanEn.Substring(0, parenEnMatch.Index).Trim();
            if (!string.IsNullOrEmpty(mainVi) && !string.IsNullOrEmpty(mainEn))
            {
                return $"{emoji}{commonPrefix}{mainVi} ({mainEn})".Trim();
            }
        }

        // 4. Với chuỗi ngắn (nhãn nút, tab, tiêu đề, options): "Tiếng Việt (English)"
        if (cleanEn.Length <= 45 && cleanVi.Length <= 45 && !cleanEn.Contains('\n') && !cleanVi.Contains('\n'))
        {
            return $"{emoji}{commonPrefix}{cleanVi} ({cleanEn})".Trim();
        }

        // 5. Với các câu mô tả dài, dùng gạch nối "Tiếng Việt — English"
        return $"{emoji}{commonPrefix}{cleanVi} — {cleanEn}".Trim();
    }

    public static string GetEn(string key) => Strings.TryGetValue(key, out var pair) ? pair.en : key;
    public static string GetVi(string key) => Strings.TryGetValue(key, out var pair) ? pair.vi : key;
}
