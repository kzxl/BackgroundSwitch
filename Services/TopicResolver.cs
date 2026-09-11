using System.Collections.Concurrent;
using BackgroundSwitch.Models;

namespace BackgroundSwitch.Services;

public static class TopicResolver
{
    private static readonly ConcurrentDictionary<string, int> _sequentialIndices = new();
    private static readonly Random _random = new();

    /// <summary>
    /// Phân tích danh sách các topic từ chuỗi raw (phân tách bởi dấu phẩy, chấm phẩy hoặc dấu gạch đứng).
    /// </summary>
    public static List<string> ParseTopics(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return new List<string>();
        }

        return rawInput
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Chọn ra 1 topic đang hoạt động theo chế độ Random hoặc Sequential.
    /// </summary>
    public static string ResolveTopic(string? rawInput, TopicSelectionMode mode, string keyScope = "default", string fallback = "nature")
    {
        var topics = ParseTopics(rawInput);
        if (topics.Count == 0)
        {
            return fallback;
        }

        if (topics.Count == 1)
        {
            return topics[0];
        }

        if (mode == TopicSelectionMode.Sequential)
        {
            // Tăng chỉ số tuần tự an toàn đa luồng (Round-Robin)
            int nextIndex = _sequentialIndices.AddOrUpdate(
                keyScope,
                0,
                (_, oldIdx) => (oldIdx + 1) % topics.Count);

            int safeIndex = Math.Abs(nextIndex) % topics.Count;
            return topics[safeIndex];
        }
        else
        {
            // Ngẫu nhiên
            lock (_random)
            {
                return topics[_random.Next(topics.Count)];
            }
        }
    }

    /// <summary>
    /// Thêm hoặc bớt một tag/chip vào chuỗi danh sách topic hiện tại (Toggle tag).
    /// Nếu tag đã có thì loại bỏ, nếu chưa có thì thêm vào cuối.
    /// </summary>
    public static string ToggleTopic(string? currentInput, string tagToToggle)
    {
        if (string.IsNullOrWhiteSpace(tagToToggle))
        {
            return currentInput?.Trim() ?? string.Empty;
        }

        var list = ParseTopics(currentInput);
        var existing = list.FirstOrDefault(t => string.Equals(t, tagToToggle.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            list.Remove(existing);
        }
        else
        {
            list.Add(tagToToggle.Trim());
        }

        return string.Join(", ", list);
    }
}
