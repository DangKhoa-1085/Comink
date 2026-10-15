// Cổng (port) cho nghiệp vụ tài khoản. Tầng Application sở hữu luồng nghiệp vụ;
// chi tiết ASP.NET Core Identity nằm ở Infrastructure (quy tắc phụ thuộc: Application không tham chiếu Infrastructure).
namespace Comink.Application.Identity.Abstractions;

/// <summary>
/// Dữ liệu người dùng ở dạng trung lập với công nghệ, dùng trong tầng Application.
/// </summary>
public record IdentityUserData(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string Bio,
    string? AvatarUrl,
    string? CoverUrl,
    string? Location,
    DateOnly? DateOfBirth,
    int FollowerCount,
    int FollowingCount,
    int FriendCount,
    string Language,
    string AccountStatus,
    DateTimeOffset CreatedAt);

/// <summary>Kết quả thực hiện một thao tác ở kho tài khoản.</summary>
public enum IdentityFailure
{
    None = 0,

    /// <summary>Email đã được dùng.</summary>
    DuplicateEmail,

    /// <summary>Tên đăng nhập đã được dùng.</summary>
    DuplicateUserName,

    /// <summary>Mật khẩu không đạt chính sách của Identity.</summary>
    PasswordPolicyViolation,

    /// <summary>Dữ liệu người dùng không hợp lệ (email sai định dạng...).</summary>
    InvalidUserData,

    /// <summary>Không tìm thấy người dùng.</summary>
    NotFound,

    /// <summary>Lỗi khác chưa phân loại.</summary>
    Other
}

/// <summary>Kết quả của một thao tác ghi.</summary>
public record IdentityOperationResult(bool Succeeded, IdentityFailure Error, string? ErrorDetail = null)
{
    public static IdentityOperationResult Ok() => new(true, IdentityFailure.None);

    public static IdentityOperationResult Fail(IdentityFailure error, string? detail = null) =>
        new(false, error, detail);
}

/// <summary>
/// Hợp đồng truy cập kho tài khoản (ASP.NET Core Identity).
/// Cài đặt ở Infrastructure. Không trả entity Identity ra ngoài.
/// </summary>
public interface IIdentityService
{
    /// <summary>Tạo tài khoản mới cùng hồ sơ và cài đặt mặc định.</summary>
    Task<IdentityOperationResult> CreateUserAsync(
        string userName,
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy dữ liệu người dùng theo id (kèm hồ sơ và cài đặt).</summary>
    Task<IdentityUserData?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mật khẩu và trả về id người dùng.
    /// Không tiết lộ tài khoản có tồn tại hay không: mọi trường hợp sai đều trả về null.
    /// </summary>
    Task<Guid?> ValidateCredentialsAsync(
        string emailOrUserName,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách role của người dùng.</summary>
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật hồ sơ; trường nào null thì giữ nguyên.</summary>
    Task<IdentityOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        string? bio,
        string? location,
        DateOnly? dateOfBirth,
        CancellationToken cancellationToken = default);

    /// <summary>Cập nhật ngôn ngữ giao diện (<c>vi</c> hoặc <c>en</c>).</summary>
    Task<IdentityOperationResult> UpdateLanguageAsync(
        Guid userId,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách tài khoản cho trang quản trị, sắp xếp theo (created_at, id) giảm dần.
    /// Trả về tối đa <paramref name="limit"/> bản ghi, bắt đầu sau <paramref name="cursor"/>.
    /// </summary>
    Task<IReadOnlyList<AdminUserData>> ListUsersAsync(
        string? search,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Một dòng trong danh sách tài khoản của trang quản trị.</summary>
public record AdminUserData(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string AccountStatus,
    DateTimeOffset CreatedAt);
