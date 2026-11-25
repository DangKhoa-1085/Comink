// Entity ánh xạ bảng `user_devices` (docs/03_database_design.md mục 3.1).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>Token FCM của một thiết bị đăng nhập.</summary>
public class UserDevice
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Web, Android hoặc iOS.</summary>
    public string Platform { get; set; } = string.Empty;

    public string PushToken { get; set; } = string.Empty;
    public string? DeviceName { get; set; }

    /// <summary>False khi FCM báo token hết hạn/không còn hợp lệ.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
