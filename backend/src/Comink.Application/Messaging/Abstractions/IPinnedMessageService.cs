// Hợp đồng nghiệp vụ ghim tin nhắn (NC Giai đoạn 7 — Ưu tiên 2, quyết định D3).
using Comink.Application.Common;
using Comink.Application.Messaging.Dtos;

namespace Comink.Application.Messaging.Abstractions;

/// <summary>
/// Ghim/bỏ ghim tin trong hội thoại. Giới hạn 10 tin/hội thoại, kiểm tra ở tầng ứng dụng
/// (docs/03 mục 7 #4). Mọi thay đổi phát <c>PinnedMessagesChanged</c> tới thành viên.
/// </summary>
public interface IPinnedMessageService
{
    /// <summary>Danh sách ghim hiện tại; không phải thành viên → CONVERSATION_NOT_FOUND.</summary>
    Task<OperationResult<PinnedMessagesDto>> GetAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghim một tin còn hiệu lực. Ghim lặp lại là idempotent; đã đủ 10 tin → MESSAGE_PIN_LIMIT_REACHED.
    /// </summary>
    Task<OperationResult<PinnedMessagesDto>> PinAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>Bỏ ghim (idempotent — không có thì trả danh sách hiện tại, không phát sự kiện).</summary>
    Task<OperationResult<PinnedMessagesDto>> UnpinAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}
