// Hợp đồng nghiệp vụ cuộc gọi (docs/05_api_contract.md mục 9).
using Comink.Application.Calls.Dtos;
using Comink.Application.Common;

namespace Comink.Application.Calls.Abstractions;

/// <summary>Loại sự kiện nhận từ nhà cung cấp gọi (đã chuẩn hoá, không phụ thuộc LiveKit).</summary>
public enum CallProviderEventType
{
    RoomStarted = 0,
    RoomFinished = 1,
    ParticipantJoined = 2,
    ParticipantLeft = 3
}

/// <summary>Sự kiện nhà cung cấp gọi đã chuẩn hoá (từ webhook).</summary>
public record CallProviderEvent(
    CallProviderEventType Type,
    string RoomName,
    string? ParticipantIdentity = null);

/// <summary>Nghiệp vụ cuộc gọi.</summary>
public interface ICallService
{
    /// <summary>Bắt đầu cuộc gọi 1–1 (trong hội thoại) hoặc nhóm (tạo hội thoại ẩn).</summary>
    Task<OperationResult<CallDto>> StartAsync(
        Guid currentUserId,
        StartCallRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cấp token ngắn hạn để vào room; chỉ thành viên mới được cấp.</summary>
    Task<OperationResult<CallTokenDto>> GetTokenAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Người nhận đồng ý → cuộc gọi chuyển sang đang diễn ra.</summary>
    Task<OperationResult<CallDto>> AnswerAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Người nhận từ chối; mọi người từ chối thì cuộc gọi kết thúc.</summary>
    Task<OperationResult<CallDto>> DeclineAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Kết thúc cuộc gọi (ghi thời lượng, tin nhắn loại Call, phát sự kiện).</summary>
    Task<OperationResult<CallDto>> EndAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết cuộc gọi (chỉ thành viên).</summary>
    Task<OperationResult<CallDto>> GetByIdAsync(
        Guid currentUserId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Lịch sử cuộc gọi của tôi.</summary>
    Task<OperationResult<PagedList<CallSummaryDto>>> GetHistoryAsync(
        Guid currentUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Xử lý sự kiện từ nhà cung cấp (webhook): đóng cuộc gọi treo, cập nhật người vào/rời.</summary>
    Task HandleProviderEventAsync(
        CallProviderEvent providerEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Phát sự kiện cuộc gọi qua SignalR. Định nghĩa ở Application để Infrastructure không phụ thuộc
/// SignalR/Api; cài đặt nằm ở tầng Api (giống <c>IChatNotifier</c>).
/// </summary>
public interface ICallNotifier
{
    Task CallIncomingAsync(
        IReadOnlyList<Guid> inviteeIds,
        CallDto call,
        CancellationToken cancellationToken = default);

    Task CallUpdatedAsync(
        IReadOnlyList<Guid> memberIds,
        CallDto call,
        CancellationToken cancellationToken = default);

    Task CallEndedAsync(
        IReadOnlyList<Guid> memberIds,
        CallEndedDto ended,
        CancellationToken cancellationToken = default);
}
