// DTO và request/response cho tính năng Comink Locket.
namespace Comink.Application.Social.Dtos;

/// <summary>Yêu cầu đăng một khoảnh khắc Locket mới.</summary>
public record CreateLocketRequest(
    Guid MediaAssetId,
    string? Caption,
    string AudienceType = "AllFriends",
    IReadOnlyList<Guid>? RecipientIds = null);

/// <summary>Yêu cầu thả hoặc đổi cảm xúc lên một Locket.</summary>
public record ReactLocketRequest(string Emoji);

/// <summary>Yêu cầu trả lời nhanh một Locket vào đoạn chat 1–1.</summary>
public record ReplyLocketRequest(string Content);

/// <summary>Thông tin tối thiểu của tác giả Locket.</summary>
public record LocketAuthorDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? AvatarUrl);

/// <summary>Thống kê từng cảm xúc trên Locket.</summary>
public record LocketReactionSummaryDto(
    string Emoji,
    int Count,
    bool ReactedByMe);

/// <summary>Một khoảnh khắc Locket còn hiệu lực trong 24 giờ.</summary>
public record LocketDto(
    Guid Id,
    LocketAuthorDto Author,
    string ImageUrl,
    string? Caption,
    string AudienceType,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsMine,
    IReadOnlyList<LocketReactionSummaryDto> Reactions);

/// <summary>Kết quả trả lời Locket vào đoạn chat 1–1.</summary>
public record LocketReplyResultDto(Guid ConversationId, Guid MessageId);
