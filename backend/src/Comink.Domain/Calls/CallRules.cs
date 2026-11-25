// Quy tắc nghiệp vụ của cuộc gọi (docs/tasks/phase4_calls.md mục 3–4).
using Comink.Domain.Enums;

namespace Comink.Domain.Calls;

/// <summary>
/// Vòng đời cuộc gọi và các ràng buộc cơ bản.
/// Trạng thái cuối (terminal) không được chuyển tiếp nữa — mọi thao tác trên đó phải bị từ chối.
/// </summary>
public static class CallRules
{
    /// <summary>Thời gian đổ chuông tối đa (giây) trước khi coi là không trả lời.</summary>
    public const int RingTimeoutSeconds = 30;

    /// <summary>Thời hạn token vào room (giây) — token ngắn hạn theo yêu cầu roadmap #4.</summary>
    public const int JoinTokenTtlSeconds = 600;

    /// <summary>Số người tối đa trong một cuộc gọi nhóm (kể cả người gọi).</summary>
    public const int MaxParticipants = 10;

    /// <summary>Cuộc gọi đã kết thúc (không cấp token, không đổi trạng thái nữa).</summary>
    public static bool IsTerminal(CallStatus status) => status is
        CallStatus.Completed or CallStatus.Declined or CallStatus.Cancelled or CallStatus.Missed or CallStatus.Failed;

    /// <summary>Chỉ được trả lời khi còn đổ chuông.</summary>
    public static bool CanAnswer(CallStatus status) => status == CallStatus.Ringing;

    /// <summary>Chỉ cấp token khi cuộc gọi chưa kết thúc.</summary>
    public static bool CanRequestToken(CallStatus status) => !IsTerminal(status);

    /// <summary>
    /// Thời lượng cuộc gọi tính từ lúc có người đầu tiên tham gia tới lúc kết thúc.
    /// Trả null khi chưa đủ mốc; không bao giờ trả số âm.
    /// </summary>
    public static int? ComputeDurationSeconds(DateTimeOffset? answeredAt, DateTimeOffset? endedAt)
    {
        if (answeredAt is null || endedAt is null)
        {
            return null;
        }

        var seconds = (int)Math.Round((endedAt.Value - answeredAt.Value).TotalSeconds, MidpointRounding.AwayFromZero);
        return seconds < 0 ? 0 : seconds;
    }

    /// <summary>
    /// Trạng thái + lý do kết thúc khi cuộc gọi dừng lại.
    /// <para>
    /// Thứ tự ưu tiên: đã có người tham gia → <c>Completed</c>; người gọi huỷ khi chưa ai vào →
    /// <c>Cancelled</c>; hết giờ đổ chuông → <c>Missed</c>; tất cả từ chối → <c>Declined</c>.
    /// </para>
    /// </summary>
    public static (CallStatus Status, CallEndReason EndReason) ResolveEnd(
        bool anyoneJoined,
        bool cancelledByInitiator,
        bool allDeclined,
        bool timedOut,
        bool failed = false)
    {
        if (failed)
        {
            return (CallStatus.Failed, CallEndReason.Failed);
        }

        if (anyoneJoined)
        {
            return (CallStatus.Completed, CallEndReason.Completed);
        }

        if (timedOut)
        {
            return (CallStatus.Missed, CallEndReason.NoAnswer);
        }

        if (cancelledByInitiator)
        {
            return (CallStatus.Cancelled, CallEndReason.Cancelled);
        }

        if (allDeclined)
        {
            return (CallStatus.Declined, CallEndReason.Declined);
        }

        return (CallStatus.Completed, CallEndReason.Completed);
    }

    /// <summary>Danh sách người được mời của cuộc gọi nhóm phải hợp lệ (≥2 người, không trùng, ≤10).</summary>
    public static bool AreParticipantsValid(IReadOnlyCollection<Guid> participantIds) =>
        participantIds.Count >= 2
        && participantIds.Count <= MaxParticipants
        && participantIds.Distinct().Count() == participantIds.Count;
}
