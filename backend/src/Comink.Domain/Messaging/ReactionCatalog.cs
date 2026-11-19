// Danh mục cảm xúc cố định cho tin nhắn (NC Giai đoạn 7 — Ưu tiên 2, quyết định D2).
// API trao đổi bằng KHOÁ chữ (like/love/...) để tránh lỗi URL-encode và lệch variation selector
// của emoji giữa web và app; cột `message_reactions.emoji` vẫn lưu ký tự emoji.
namespace Comink.Domain.Messaging;

/// <summary>Một cảm xúc trong danh mục: khoá API + ký tự emoji hiển thị.</summary>
public record ReactionDefinition(string Key, string Emoji);

/// <summary>Danh mục 6 cảm xúc được phép thả trên một tin.</summary>
public static class ReactionCatalog
{
    public const string Like = "like";
    public const string Love = "love";
    public const string Haha = "haha";
    public const string Wow = "wow";
    public const string Sad = "sad";
    public const string Angry = "angry";

    /// <summary>Toàn bộ cảm xúc hợp lệ, theo thứ tự hiển thị trên giao diện.</summary>
    public static readonly IReadOnlyList<ReactionDefinition> All =
    [
        new(Like, "👍"),
        new(Love, "❤️"),
        new(Haha, "😂"),
        new(Wow, "😮"),
        new(Sad, "😢"),
        new(Angry, "😡")
    ];

    /// <summary>Các khoá hợp lệ (dùng cho validator).</summary>
    public static readonly IReadOnlyList<string> Keys = All.Select(item => item.Key).ToList();

    /// <summary>Khoá có nằm trong danh mục không (không phân biệt hoa/thường).</summary>
    public static bool IsValid(string? key) => TryGetEmoji(key, out _);

    /// <summary>Quy đổi khoá sang emoji lưu DB; khoá lạ trả <c>null</c>.</summary>
    public static string? ToEmoji(string? key) => TryGetEmoji(key, out var emoji) ? emoji : null;

    /// <summary>Quy đổi khoá sang emoji lưu DB, không phân biệt hoa/thường.</summary>
    public static bool TryGetEmoji(string? key, out string emoji)
    {
        emoji = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim().ToLowerInvariant();
        foreach (var definition in All)
        {
            if (definition.Key == normalized)
            {
                emoji = definition.Emoji;
                return true;
            }
        }

        return false;
    }
}
