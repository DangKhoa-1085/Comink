// Quy tắc nghiệp vụ cho tính năng Comink Locket (thời hạn 24 giờ, quyền xem, cảm xúc hợp lệ).
using Comink.Domain.Enums;

namespace Comink.Domain.Social;

/// <summary>Quy tắc miền thuần của Comink Locket.</summary>
public static class LocketRules
{
    /// <summary>Độ dài tối đa của chú thích nổi trên ảnh Locket.</summary>
    public const int MaxCaptionLength = 100;

    /// <summary>Độ dài tối đa của tin nhắn trả lời nhanh Locket.</summary>
    public const int MaxReplyLength = 1000;

    /// <summary>Mỗi Locket chỉ hiển thị trong 24 giờ kể từ lúc đăng.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Danh mục cảm xúc hợp lệ khi thả trên một ảnh Locket.</summary>
    public static readonly IReadOnlyList<string> AllowedReactions =
        ["❤️", "😂", "😮", "🔥", "🥹"];

    /// <summary>Kiểm tra emoji có thuộc danh mục Locket cho phép hay không.</summary>
    public static bool IsValidReaction(string? emoji) =>
        !string.IsNullOrWhiteSpace(emoji) && AllowedReactions.Contains(emoji.Trim(), StringComparer.Ordinal);

    /// <summary>Kiểm tra Locket đã hết hạn 24h chưa.</summary>
    public static bool IsExpired(DateTimeOffset expiresAt, DateTimeOffset now) => expiresAt <= now;

    /// <summary>Kiểm tra một người dùng có quyền xem Locket này tại thời điểm hiện tại hay không.</summary>
    public static bool CanView(
        Guid viewerId,
        Guid authorId,
        bool isFriend,
        LocketAudience audience,
        bool isExplicitRecipient,
        DateTimeOffset expiresAt,
        DateTimeOffset? deletedAt,
        DateTimeOffset now)
    {
        if (deletedAt is not null || IsExpired(expiresAt, now))
        {
            return false;
        }

        if (viewerId == authorId)
        {
            return true;
        }

        if (!isFriend)
        {
            return false;
        }

        return audience == LocketAudience.AllFriends || isExplicitRecipient;
    }
}
