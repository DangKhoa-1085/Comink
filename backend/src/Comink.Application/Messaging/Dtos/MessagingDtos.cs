// DTO và request cho module Nhắn tin (docs/05_api_contract.md mục 8).
namespace Comink.Application.Messaging.Dtos;

/// <summary>Yêu cầu mở/tạo hội thoại 1–1.</summary>
public record CreateDirectConversationRequest(Guid UserId);

/// <summary>Yêu cầu gửi tin nhắn. <c>ClientMessageId</c> do client sinh để chống gửi trùng.</summary>
public record SendMessageRequest(Guid ClientMessageId, string? Content, IReadOnlyList<Guid>? MediaIds);

/// <summary>Một thành viên hội thoại (thông tin hiển thị).</summary>
public record ConversationMemberDto(Guid UserId, string UserName, string DisplayName, string? AvatarUrl);

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
public record ConversationPeerDto(Guid UserId, string UserName, string DisplayName, string? AvatarUrl);

/// <summary>Xem trước tin cuối trong danh sách hội thoại.</summary>
public record ConversationLastMessageDto(Guid Id, Guid SenderId, string Preview, string Type, DateTimeOffset CreatedAt);

/// <summary>Một dòng trong danh sách hội thoại.</summary>
public record ConversationSummaryDto(
    Guid Id,
    string Type,
    string? Title,
    ConversationPeerDto Peer,
    ConversationLastMessageDto? LastMessage,
    int UnreadCount);
