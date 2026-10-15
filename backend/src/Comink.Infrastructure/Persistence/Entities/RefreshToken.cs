// Entity ánh xạ sang bảng `refresh_tokens` (xem docs/03_database_design.md mục 3.1).
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Refresh token cho phiên đăng nhập.
/// Database chỉ lưu băm của token; mọi token xoay vòng từ một lần đăng nhập dùng chung một FamilyId.
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
