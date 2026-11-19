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

    /// <summary>
    /// Người dùng có đang là thành viên còn hiệu lực của hội thoại không.
    /// Dùng cho tín hiệu "đang nhập": hub phải kiểm tra quyền chứ không tin id client gửi.
    /// </summary>
    Task<bool> IsMemberAsync(
        Guid currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Những người **cùng ít nhất một hội thoại còn hiệu lực** với người này.
    /// Là phạm vi được phép thấy trạng thái online (quyết định D2, phase7 mục 2) — không phát toàn cục.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetCoMemberUserIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu đã đọc tới <paramref name="messageId"/> (NC Giai đoạn 7). Mốc chỉ tiến, không lùi;
    /// khi tiến thật thì phát <c>MessagesRead</c> tới mọi thành viên còn hiệu lực.
    /// Không phải thành viên → <c>CONVERSATION_NOT_FOUND</c>; tin không thuộc hội thoại →
    /// <c>MESSAGE_NOT_FOUND</c>.
    /// </summary>
    Task<OperationResult<ReadReceiptDto>> MarkReadAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}

/// <summary>Nghiệp vụ tin nhắn.</summary>
public interface IMessageService
{
    /// <summary>
    /// Gửi tin nhắn. Trùng <c>ClientMessageId</c> thì trả tin đã lưu (không tạo bản ghi mới)
    /// và <c>WasCreated = false</c>. <c>ReplyToMessageId</c> (NC Giai đoạn 7) phải là tin thuộc
    /// cùng hội thoại và chưa bị thu hồi.
    /// </summary>
    Task<OperationResult<MessageSendResult>> SendAsync(
        Guid currentUserId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sửa nội dung tin (NC Giai đoạn 7): chỉ người gửi, trong 24 giờ, tin chưa bị thu hồi và
    /// không phải tin hệ thống/cuộc gọi. Khi sửa thật thì phát <c>MessageUpdated</c>.
    /// </summary>
    Task<OperationResult<MessageDto>> EditAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi mềm tin (NC Giai đoạn 7): chỉ người gửi, trong 24 giờ. Xoá nội dung + tệp hiển thị,
    /// xoá cảm xúc và ghim liên quan, phát <c>MessageUpdated</c> (+ <c>PinnedMessagesChanged</c>
    /// nếu tin đang được ghim).
    /// </summary>
    Task<OperationResult<MessageDto>> RecallAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm một cảm xúc lên tin (idempotent theo bộ ba tin/người/loại). Phát <c>MessageUpdated</c>.
    /// </summary>
    Task<OperationResult<MessageDto>> AddReactionAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        string reaction,
        CancellationToken cancellationToken = default);

    /// <summary>Bỏ một cảm xúc của chính mình (idempotent). Phát <c>MessageUpdated</c> khi có thay đổi.</summary>
    Task<OperationResult<MessageDto>> RemoveReactionAsync(
        Guid currentUserId,
        Guid conversationId,
        Guid messageId,
        string reaction,
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

    /// <summary>
    /// Phát <c>MessagesRead</c> tới các thành viên (qua <c>user:{id}</c> và group hội thoại)
    /// để người gửi thấy "Đã đọc" ngay.
    /// </summary>
    Task MessageReadAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        ReadReceiptDto receipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát <c>PresenceChanged</c> cho **những người được phép thấy** trạng thái của người dùng
    /// (cùng hội thoại — quyết định D2). Gửi qua <c>user:{id}</c>.
    /// </summary>
    Task PresenceChangedAsync(
        IReadOnlyList<Guid> audienceUserIds,
        PresenceDto presence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát <c>MessageUpdated</c> khi một tin vừa bị sửa/thu hồi/đổi cảm xúc (NC Giai đoạn 7).
    /// Cùng chiến lược với <c>MessageCreated</c>: group hội thoại + <c>user:{id}</c> từng thành viên.
    /// </summary>
    Task MessageUpdatedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        MessageDto message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát <c>PinnedMessagesChanged</c> khi danh sách ghim của hội thoại thay đổi
    /// (ghim/bỏ ghim, hoặc thu hồi một tin đang ghim).
    /// </summary>
    Task PinnedMessagesChangedAsync(
        Guid conversationId,
        IReadOnlyList<Guid> memberUserIds,
        PinnedMessagesDto pinned,
        CancellationToken cancellationToken = default);
}
