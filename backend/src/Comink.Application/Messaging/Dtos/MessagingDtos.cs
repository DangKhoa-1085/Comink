// DTO và request cho module Nhắn tin (docs/05_api_contract.md mục 8).
namespace Comink.Application.Messaging.Dtos;

/// <summary>Yêu cầu mở/tạo hội thoại 1–1.</summary>
public record CreateDirectConversationRequest(Guid UserId);

/// <summary>
/// Yêu cầu tạo nhóm chat (NC GĐ 3): tên nhóm + danh sách thành viên mời (không gồm người tạo).
/// Tổng thành viên phải trong khoảng 3–10 và tất cả phải là bạn bè của người tạo.
/// </summary>
public record CreateGroupConversationRequest(string Name, IReadOnlyList<Guid> ParticipantIds);

/// <summary>Yêu cầu gửi tin nhắn. <c>ClientMessageId</c> do client sinh để chống gửi trùng.</summary>
public record SendMessageRequest(Guid ClientMessageId, string? Content, IReadOnlyList<Guid>? MediaIds);

/// <summary>
/// Yêu cầu đánh dấu đã đọc tới một tin (NC Giai đoạn 7). Server chỉ **tiến** mốc,
/// không bao giờ lùi (docs/05_api_contract.md mục 8.9).
/// </summary>
public record MarkReadRequest(Guid MessageId);

/// <summary>
/// Trạng thái online của một người. <c>LastSeenAt</c> là thời điểm ngắt kết nối gần nhất,
/// chỉ có khi đang offline và đã từng kết nối.
/// </summary>
public record PresenceDto(Guid UserId, bool IsOnline, DateTimeOffset? LastSeenAt);

/// <summary>Mốc đọc đã được ghi nhận (dùng cho sự kiện <c>MessagesRead</c>).</summary>
public record ReadReceiptDto(Guid ConversationId, Guid UserId, Guid MessageId, DateTimeOffset ReadAt);

/// <summary>
/// Tín hiệu "đang nhập" (NC Giai đoạn 7). Không lưu DB — chỉ chuyển tiếp trong hội thoại.
/// </summary>
public record TypingSignalDto(Guid ConversationId, Guid UserId, bool IsTyping);

/// <summary>Một thành viên hội thoại (thông tin hiển thị + trạng thái online + mốc đọc).</summary>
public record ConversationMemberDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAt);

/// <summary>Tệp đính kèm của tin nhắn.</summary>
public record MessageAttachmentDto(Guid MediaId, string Url, string? ThumbnailUrl, int? Width, int? Height);

/// <summary>Một tin nhắn trả về client.</summary>
public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Type,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    Guid ClientMessageId,
    DateTimeOffset CreatedAt);

/// <summary>Hội thoại đầy đủ (chi tiết).</summary>
public record ConversationDto(
    Guid Id,
    string Type,
    string? Title,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    IReadOnlyList<ConversationMemberDto> Members,
    MessageDto? LastMessage);

/// <summary>Người còn lại trong hội thoại 1–1 (dùng cho danh sách hội thoại).</summary>
public record ConversationPeerDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    bool IsOnline,
    DateTimeOffset? LastSeenAt);

/// <summary>Xem trước tin cuối trong danh sách hội thoại.</summary>
public record ConversationLastMessageDto(Guid Id, Guid SenderId, string Preview, string Type, DateTimeOffset CreatedAt);

/// <summary>Một dòng trong danh sách hội thoại. <c>Peer</c> chỉ có với chat 1–1; nhóm dùng <c>Title</c>.</summary>
public record ConversationSummaryDto(
    Guid Id,
    string Type,
    string? Title,
    ConversationPeerDto? Peer,
    ConversationLastMessageDto? LastMessage,
    int UnreadCount,
    int MemberCount);
