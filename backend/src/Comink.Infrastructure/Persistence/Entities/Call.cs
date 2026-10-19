// Entity ánh xạ bảng `calls` (docs/03_database_design.md mục 3.4).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Một cuộc gọi, luôn gắn với một hội thoại (<c>Direct</c> cho 1–1, <c>Group</c> ẩn cho gọi nhóm).
/// <see cref="RoomName"/> là tên room ở LiveKit và là duy nhất.
/// </summary>
public class Call
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid InitiatorId { get; set; }
    public ApplicationUser Initiator { get; set; } = null!;

    public CallType Type { get; set; } = CallType.Audio;

    public CallStatus Status { get; set; } = CallStatus.Ringing;

    /// <summary>Tên room ở nhà cung cấp (LiveKit); duy nhất.</summary>
    public string RoomName { get; set; } = string.Empty;

    /// <summary>Lúc bắt đầu đổ chuông.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Lúc có người đầu tiên tham gia.</summary>
    public DateTimeOffset? AnsweredAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Tính từ <see cref="AnsweredAt"/> đến <see cref="EndedAt"/>.</summary>
    public int? DurationSeconds { get; set; }

    public CallEndReason? EndReason { get; set; }

    public ICollection<CallParticipant> Participants { get; set; } = new List<CallParticipant>();
}
