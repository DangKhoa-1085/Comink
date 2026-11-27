// DTO và request cho module Nhắn tin (docs/05_api_contract.md mục 8).
namespace Comink.Application.Messaging.Dtos;

/// <summary>Yêu cầu mở/tạo hội thoại 1–1.</summary>
public record CreateDirectConversationRequest(Guid UserId);

/// <summary>
/// Yêu cầu tạo nhóm chat (NC GĐ 3): tên nhóm + danh sách thành viên mời (không gồm người tạo).
/// Tổng thành viên phải trong khoảng 3–10 và tất cả phải là bạn bè của người tạo.
/// </summary>
public record CreateGroupConversationRequest(string Name, IReadOnlyList<Guid> ParticipantIds);

/// <summary>Yêu cầu cập nhật thông tin nhóm chat (tên nhóm và/hoặc ảnh đại diện).</summary>
public record UpdateGroupConversationRequest(string? Title, string? AvatarUrl);

/// <summary>Yêu cầu thêm thành viên mới (phải là bạn bè) vào nhóm chat đã tạo.</summary>
public record AddGroupMembersRequest(IReadOnlyList<Guid> MemberIds);

/// <summary>
/// Yêu cầu gửi tin nhắn. <c>ClientMessageId</c> do client sinh để chống gửi trùng.
/// <c>ReplyToMessageId</c> (NC Giai đoạn 7) trỏ tới tin được phản hồi trong cùng hội thoại.
/// </summary>
public record SendMessageRequest(
    Guid ClientMessageId,
    string? Content,
    IReadOnlyList<Guid>? MediaIds,
    Guid? ReplyToMessageId = null);

/// <summary>Yêu cầu chỉnh sửa nội dung tin nhắn (NC Giai đoạn 7 — 24 giờ, chỉ người gửi).</summary>
public record EditMessageRequest(string? Content);

/// <summary>Yêu cầu thả một cảm xúc lên tin nhắn. <c>Reaction</c> là khoá trong <c>ReactionCatalog</c>.</summary>
public record ReactionRequest(string Reaction);

/// <summary>Yêu cầu chuyển tiếp một tin sang hội thoại đích (NC Ưu tiên 2).</summary>
public record ForwardMessageRequest(Guid SourceMessageId, Guid ClientMessageId);

/// <summary>Yêu cầu tắt/bật thông báo một hội thoại (NC Ưu tiên 2).</summary>
public record MuteConversationRequest(string? Duration);

/// <summary>Trạng thái tắt thông báo sau khi đổi; <c>MutedUntil = null</c> nghĩa là đang bật.</summary>
public record MuteStateDto(bool IsMuted, DateTimeOffset? MutedUntil);

/// <summary>Một kết quả tìm kiếm tin nhắn (chỉ trong hội thoại người dùng là thành viên).</summary>
public record MessageSearchResultDto(
    Guid MessageId,
    Guid ConversationId,
    string? ConversationTitle,
    string ConversationType,
    string? PeerDisplayName,
    Guid SenderId,
    string SenderDisplayName,
    string? ContentPreview,
    DateTimeOffset CreatedAt);

/// <summary>Cửa sổ tin quanh một tin đích (dùng khi nhảy từ kết quả tìm kiếm tới tin).</summary>
public record MessageContextDto(
    IReadOnlyList<MessageDto> Items,
    bool HasMoreBefore,
    bool HasMoreAfter);

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

/// <summary>Một thành viên hội thoại (thông tin hiển thị + trạng thái online + mốc đọc + vai trò).</summary>
public record ConversationMemberDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAt,
    string Role = "Member");

/// <summary>Tệp đính kèm của tin nhắn.</summary>
public record MessageAttachmentDto(Guid MediaId, string Url, string? ThumbnailUrl, int? Width, int? Height);

/// <summary>Ảnh chụp tối thiểu của tin được phản hồi, hiển thị trong bong bóng trích dẫn.</summary>
/// <param name="IsDeleted">Tin gốc đã bị thu hồi — client hiện "Tin nhắn đã bị thu hồi".</param>
public record MessageQuoteDto(
    Guid MessageId,
    Guid SenderId,
    string SenderDisplayName,
    string? ContentPreview,
    string Type,
    bool IsDeleted);

/// <summary>Nguồn của một tin chuyển tiếp (tên người gửi gốc, không lộ nội dung cũ).</summary>
public record ForwardedFromDto(Guid MessageId, Guid SenderId, string SenderDisplayName);

/// <summary>Tổng hợp cảm xúc của một tin: một dòng cho mỗi loại đang có.</summary>
public record ReactionSummaryDto(string Reaction, string Emoji, int Count, bool ReactedByMe);

/// <summary>Một tin nhắn trả về client.</summary>
public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Type,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    Guid ClientMessageId,
    DateTimeOffset CreatedAt,
    MessageQuoteDto? ReplyTo,
    ForwardedFromDto? ForwardedFrom,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt,
    IReadOnlyList<ReactionSummaryDto> Reactions);

/// <summary>Hội thoại đầy đủ (chi tiết).</summary>
public record ConversationDto(
    Guid Id,
    string Type,
    string? Title,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    IReadOnlyList<ConversationMemberDto> Members,
    MessageDto? LastMessage,
    bool IsMuted,
    DateTimeOffset? MutedUntil);

/// <summary>Người còn lại trong hội thoại 1–1 (dùng cho danh sách hội thoại).</summary>
public record ConversationPeerDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? AvatarUrl,
    bool IsOnline,
    DateTimeOffset? LastSeenAt);

/// <summary>Xem trước tin cuối trong danh sách hội thoại.</summary>
/// <param name="IsDeleted">Tin cuối đã bị thu hồi — client hiện "Tin nhắn đã bị thu hồi".</param>
public record ConversationLastMessageDto(
    Guid Id,
    Guid SenderId,
    string Preview,
    string Type,
    DateTimeOffset CreatedAt,
    bool IsDeleted);

/// <summary>Một tin đang được ghim trong hội thoại.</summary>
public record PinnedMessageDto(
    MessageDto Message,
    Guid PinnedByUserId,
    string PinnedByDisplayName,
    DateTimeOffset PinnedAt);

/// <summary>Danh sách ghim hiện tại của hội thoại (payload sự kiện <c>PinnedMessagesChanged</c>).</summary>
public record PinnedMessagesDto(Guid ConversationId, IReadOnlyList<PinnedMessageDto> Pinned);

/// <summary>Một dòng trong danh sách hội thoại. <c>Peer</c> chỉ có với chat 1–1; nhóm dùng <c>Title</c>.</summary>
public record ConversationSummaryDto(
    Guid Id,
    string Type,
    string? Title,
    ConversationPeerDto? Peer,
    ConversationLastMessageDto? LastMessage,
    int UnreadCount,
    int MemberCount,
    bool IsMuted,
    DateTimeOffset? MutedUntil,
    string? AvatarUrl = null);
