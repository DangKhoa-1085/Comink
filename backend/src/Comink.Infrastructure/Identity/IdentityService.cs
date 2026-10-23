// Cài đặt cổng IIdentityService trên nền ASP.NET Core Identity.
// Tầng Application chỉ thấy dữ liệu trung lập (IdentityUserData), không thấy entity Identity.
using Comink.Application.Identity.Abstractions;
using Comink.Domain.Admin;
using Comink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comink.Infrastructure.Identity;

/// <summary>
/// Bọc Identity (UserManager, RoleManager) và ánh xạ sang dữ liệu của tầng Application.
/// </summary>
public class IdentityService : IIdentityService
{
    /// <summary>Tên role mặc định cho mọi tài khoản mới (docs/01_COMINK_SPEC.md mục 3.6).</summary>
    public const string DefaultRole = "User";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IdentityOperationResult> CreateUserAsync(
        string userName,
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            EmailConfirmed = false, // xác minh email là mục [NC] — Giai đoạn 7
            AccountStatus = "Active",
            CreatedAt = now,
            Profile = new UserProfile
            {
                UserId = Guid.Empty, // EF gán theo quan hệ 1-1
                DisplayName = displayName
            },
            Settings = new UserSettings
            {
                UserId = Guid.Empty
            }
        };

        var result = await CreateUserSafelyAsync(user, password);
        if (!result.Succeeded)
        {
            return MapIdentityErrors(result);
        }

        // Gán role mặc định; nếu bước này lỗi thì tài khoản vẫn dùng được nhưng thiếu quyền,
        // nên xoá tài khoản vừa tạo để tránh trạng thái nửa vời.
        var roleResult = await _userManager.AddToRoleAsync(user, DefaultRole);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return MapIdentityErrors(roleResult);
        }

        return IdentityOperationResult.Ok();
    }

    /// <summary>
    /// Tạo người dùng và bắt lỗi vi phạm ràng buộc duy nhất ở tầng database.
    /// Cần thiết vì khi hai request đăng ký cùng lúc, kiểm tra của Identity có thể
    /// không thấy nhau, và PostgreSQL sẽ ném lỗi 23505 (unique violation).
    /// </summary>
    private async Task<IdentityResult> CreateUserSafelyAsync(ApplicationUser user, string password)
    {
        try
        {
            return await _userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "EmailIndex"))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityFailureCode.DuplicateEmail),
                Description = "Email đã được sử dụng."
            });
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, "UserNameIndex"))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityFailureCode.DuplicateUserName),
                Description = "Tên đăng nhập đã được sử dụng."
            });
        }
    }

    /// <summary>
    /// Kiểm tra chuỗi exception có phải lỗi vi phạm ràng buộc duy nhất của PostgreSQL
    /// trên chỉ mục <paramref name="indexName"/> hay không.
    /// </summary>
    private static bool IsUniqueViolation(Exception exception, string indexName)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgres.ConstraintName == indexName)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<IdentityUserData?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : Map(user);
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateCredentialsAsync(
        string emailOrUserName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Cho phép đăng nhập bằng email hoặc tên đăng nhập.
        var user = emailOrUserName.Contains('@')
            ? await _userManager.FindByEmailAsync(emailOrUserName)
            : await _userManager.FindByNameAsync(emailOrUserName);

        // Không tiết lộ tài khoản có tồn tại: sai tài khoản và sai mật khẩu đều trả null.
        if (user is null)
        {
            return null;
        }

        // Chỉ sai mật khẩu mới trả null. Tài khoản bị khóa mà mật khẩu đúng vẫn trả id để
        // AuthService trả đúng mã lỗi AUTH_ACCOUNT_LOCKED (403) theo hợp đồng API mục 2 —
        // nhưng người dò tài khoản không nhận được thông tin gì nếu không biết mật khẩu.
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        return passwordValid ? user.Id : null;
    }

    /// <inheritdoc />
    public async Task<string?> GetAccountStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Chỉ đọc một cột theo khóa chính: đường dẫn này chạy ở MỌI request đã xác thực
        // (JwtBearerEvents.OnTokenValidated) nên phải nhẹ, không Include hồ sơ/cài đặt.
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AccountStatus)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IdentityOperationResult> SetAccountStatusAsync(
        Guid userId,
        string accountStatus,
        string? reason,
        Guid? changedBy,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return IdentityOperationResult.Fail(IdentityFailure.NotFound);
        }

        var now = _timeProvider.GetUtcNow();
        user.AccountStatus = accountStatus;
        user.UpdatedAt = now;

        if (string.Equals(accountStatus, AdminUserRules.LockedStatus, StringComparison.Ordinal))
        {
            user.LockedReason = reason;
            user.LockedAt = now;
            user.LockedBy = changedBy;
        }
        else
        {
            // Mở khóa: xóa dấu vết khóa để trạng thái và cột `locked_*` luôn nhất quán.
            user.LockedReason = null;
            user.LockedAt = null;
            user.LockedBy = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return IdentityOperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToArray();
    }

    /// <inheritdoc />
    public async Task<IdentityOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        string? bio,
        string? location,
        DateOnly? dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return IdentityOperationResult.Fail(IdentityFailure.NotFound);
        }

        // Chỉ cập nhật trường được gửi lên (null = giữ nguyên).
        if (displayName is not null) profile.DisplayName = displayName;
        if (bio is not null) profile.Bio = bio;
        if (location is not null) profile.Location = location;
        if (dateOfBirth is not null) profile.DateOfBirth = dateOfBirth;
        profile.UpdatedAt = _timeProvider.GetUtcNow();

        await _db.SaveChangesAsync(cancellationToken);
        return IdentityOperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<IdentityOperationResult> UpdateLanguageAsync(
        Guid userId,
        string language,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is null)
        {
            return IdentityOperationResult.Fail(IdentityFailure.NotFound);
        }

        settings.Language = language;
        settings.UpdatedAt = _timeProvider.GetUtcNow();

        await _db.SaveChangesAsync(cancellationToken);
        return IdentityOperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserData>> ListUsersAsync(
        string? search,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                (u.Profile != null && u.Profile.DisplayName.ToLower().Contains(term)));
        }

        // Phân trang con trỏ theo (created_at, id) giảm dần — không dùng OFFSET.
        if (cursorCreatedAt is not null && cursorId is not null)
        {
            query = query.Where(u =>
                u.CreatedAt < cursorCreatedAt ||
                (u.CreatedAt == cursorCreatedAt && u.Id.CompareTo(cursorId.Value) < 0));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return users
            .Select(u => new AdminUserData(
                u.Id,
                u.UserName ?? string.Empty,
                u.Email ?? string.Empty,
                u.Profile?.DisplayName ?? string.Empty,
                u.AccountStatus,
                u.LockedReason,
                u.LockedAt,
                u.CreatedAt))
            .ToArray();
    }

    private static IdentityUserData Map(ApplicationUser user) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.Profile?.DisplayName ?? string.Empty,
            user.Profile?.Bio ?? string.Empty,
            user.Profile?.AvatarUrl,
            user.Profile?.CoverUrl,
            user.Profile?.Location,
            user.Profile?.DateOfBirth,
            user.Profile?.FollowerCount ?? 0,
            user.Profile?.FollowingCount ?? 0,
            user.Profile?.FriendCount ?? 0,
            user.Settings?.Language ?? "vi",
            user.AccountStatus,
            user.LockedReason,
            user.LockedAt,
            user.CreatedAt);

    /// <summary>Ánh xạ lỗi của Identity sang mã lỗi trung lập của Application.</summary>
    private static IdentityOperationResult MapIdentityErrors(IdentityResult result)
    {
        var codes = result.Errors.Select(e => e.Code).ToArray();
        var detail = string.Join("; ", result.Errors.Select(e => e.Description));

        if (codes.Contains(nameof(IdentityFailureCode.DuplicateEmail)))
        {
            return IdentityOperationResult.Fail(IdentityFailure.DuplicateEmail, detail);
        }

        if (codes.Contains(nameof(IdentityFailureCode.DuplicateUserName)))
        {
            return IdentityOperationResult.Fail(IdentityFailure.DuplicateUserName, detail);
        }

        if (codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal)))
        {
            return IdentityOperationResult.Fail(IdentityFailure.PasswordPolicyViolation, detail);
        }

        if (codes.Contains(nameof(IdentityFailureCode.InvalidEmail)) ||
            codes.Contains(nameof(IdentityFailureCode.InvalidUserName)))
        {
            return IdentityOperationResult.Fail(IdentityFailure.InvalidUserData, detail);
        }

        return IdentityOperationResult.Fail(IdentityFailure.Other, detail);
    }

    /// <summary>Tên mã lỗi của Identity dùng để so khớp (tránh phụ thuộc chuỗi rải rác).</summary>
    private enum IdentityFailureCode
    {
        DuplicateEmail,
        DuplicateUserName,
        InvalidEmail,
        InvalidUserName
    }
}
