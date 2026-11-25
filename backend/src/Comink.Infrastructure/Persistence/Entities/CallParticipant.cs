// Entity ánh xạ bảng `call_participants` (docs/03_database_design.md mục 3.4).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Trạng thái của một người trong cuộc gọi (khóa ghép call + user).</summary>
public class CallParticipant
{
    public Guid CallId { get; set; }
    public Call Call { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public CallParticipantStatus Status { get; set; } = CallParticipantStatus.Invited;

    public DateTimeOffset InvitedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? JoinedAt { get; set; }

    public DateTimeOffset? LeftAt { get; set; }
}
