// Hợp đồng nghiệp vụ nhắn tin (docs/05_api_contract.md mục 8).
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;

namespace Comink.Application.Messaging.Abstractions;

/// <summary>Nghiệp vụ hội thoại.</summary>
public interface IConversationService
{
    /// <summary>
    /// Mở hội thoại 1–1 với một người; đã có thì trả hội thoại cũ (idempotent).
    /// Chỉ bạn bè mới mở được (quyết định D1 ở docs/tasks/phase3_messaging.md).
    /// <c>WasCreated = false</c> nghĩa là hội thoại đã tồn tại (API trả 200 thay vì 201).
    /// </summary>
    Task<OperationResult<ConversationOpenResult>> OpenDirectAsync(
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách hội thoại của tôi, sắp theo tin mới nhất.</summary>
    Task<OperationResult<PagedList<ConversationSummaryDto>>> GetMyConversationsAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo nhóm chat (NC GĐ 3 — docs/tasks/phase3_messaging.md mục 7).
    /// Tổng thành viên 3–10, tất cả người được mời phải là bạn bè; người tạo là <c>Owner</c>.
    /// </summary>
    Task<OperationResult<ConversationDto>> CreateGroupAsync(
        Guid currentUserId,
        CreateGroupConversationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một hội thoại; không phải thành viên thì trả CONVERSATION_NOT_FOUND.</summary>
    Task<OperationResult<ConversationDto>> GetByIdAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Mã các hội thoại mà người dùng còn là thành viên — dùng khi kết nối SignalR.</summary>
    Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Nghiệp vụ tin nhắn.</summary>
public interface IMessageService
{
    /// <summary>
    /// Gửi tin nhắn. Trùng <c>ClientMessageId</c> thì trả tin đã lưu (không tạo bản ghi mới)
    /// và <c>WasCreated = false</c>.
    /// </summary>
    Task<OperationResult<MessageSendResult>> SendAsync(
        Guid currentUserId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lịch sử tin nhắn theo con trỏ: <paramref name="before"/> để tải cũ hơn (mới nhất trước),
    /// <paramref name="after"/> để đồng bộ tin bị lỡ (cũ nhất trước). Không truyền thì lấy tin mới nhất.
    /// </summary>
    Task<OperationResult<PagedList<MessageDto>>> GetHistoryAsync(
        Guid currentUserId,
        Guid conversationId,
        string? before,
        string? after,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Kết quả gửi tin: có phải bản ghi mới hay trả lại tin đã tồn tại.</summary>
public record MessageSendResult(MessageDto Message, bool WasCreated);

/// <summary>Kết quả mở hội thoại: có phải vừa tạo mới hay đã tồn tại.</summary>
public record ConversationOpenResult(ConversationDto Conversation, bool WasCreated);

/// <summary>
/// Phát sự kiện thời gian thực. Định nghĩa ở Application để Infrastructure không phụ thuộc
/// SignalR/Api (AGENTS.md mục 8.1); cài đặt nằm ở tầng Api.
/// </summary>
public interface IChatNotifier
{
    /// <summary>
    /// Phát <c>MessageCreated</c> tới các thành viên của hội thoại.
    /// <para>
    /// Gửi tới <c>user:{id}</c> của **từng thành viên** (nhóm được tham gia ngay khi kết nối),
    /// không chỉ group <c>conversation:{id}</c>: hội thoại có thể được tạo **sau** khi client đã
    /// kết nối, khi đó client chưa nằm trong group hội thoại và sẽ không nhận được tin
    /// (lỗi thật đã gặp khi kiểm thử GĐ 3 — phải tải lại trang mới thấy tin).
    /// </para>
    /// </summary>
    Task MessageCreatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default);
}
