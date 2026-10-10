// ApplicationUser — mở rộng ASP.NET Core Identity
using Microsoft.AspNetCore.Identity;

namespace Comink.Infrastructure.Identity;

/// <summary>
/// Người dùng hệ thống, mở rộng từ IdentityUser với khóa UUID.
/// Ánh xạ sang bảng `users` (EFCore.NamingConventions tự chuyển sang snake_case).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    // Trạng thái tài khoản (Active/Locked/Deleted) — tách khỏi lockout của Identity
    public string AccountStatus { get; set; } = "Active";

    // Lý do và thông tin khóa tài khoản (do admin)
    public string? LockedReason { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public Guid? LockedBy { get; set; }

    // Lần cuối hoạt động (cập nhật thưa để thống kê)
    public DateTimeOffset? LastActiveAt { get; set; }

    // Thời điểm tạo tài khoản
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public UserProfile? Profile { get; set; }
    public UserSettings? Settings { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// Hồ sơ cá nhân (1-1 với ApplicationUser).
/// Ánh xạ sang bảng `user_profiles`.
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }

    // URL avatar/cover lưu dư (để tránh join khi đọc)
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }

    // Khóa ngoại tới media_assets (để quản lý, tùy chọn)
    public Guid? AvatarMediaId { get; set; }
    public Guid? CoverMediaId { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? Location { get; set; }

    // Bộ đếm denormalized (cập nhật nguyên tử trong transaction)
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int FriendCount { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Cài đặt người dùng (1-1 với ApplicationUser).
/// Ánh xạ sang bảng `user_settings`.
/// </summary>
public class UserSettings
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Language { get; set; } = "vi";

    // Các cài đặt quyền riêng tư [NC] — có sẵn trong schema nhưng chưa dùng
    public string ProfileVisibility { get; set; } = "Everyone";
    public string DefaultPostVisibility { get; set; } = "Friends";
    public string WhoCanMessage { get; set; } = "Everyone";
    public string WhoCanCall { get; set; } = "Friends";
    public bool ShowOnlineStatus { get; set; } = true;
    public bool ShowReadReceipts { get; set; } = true;

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Refresh token cho phiên đăng nhập.
/// Ánh xạ sang bảng `refresh_tokens`.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    // Chỉ lưu băm, không lưu token gốc
    public string TokenHash { get; set; } = string.Empty;

    // Mọi token xoay vòng từ cùng một lần đăng nhập chung một family_id
    public Guid FamilyId { get; set; }

    // Thông tin thiết bị (cho quản lý phiên [NC])
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;
}
