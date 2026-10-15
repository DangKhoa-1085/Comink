// Fake trong bộ nhớ cho các cổng của tầng Application, dùng trong unit test.
using Comink.Application.Identity.Abstractions;

namespace Comink.Application.Tests.Fakes;

/// <summary>
/// Bản giả của <see cref="IIdentityService"/>: lưu người dùng trong bộ nhớ,
/// cho phép dựng sẵn các tình huống (trùng email, tài khoản bị khóa, mật khẩu sai).
/// </summary>
public class FakeIdentityService : IIdentityService
{
    private readonly Dictionary<Guid, (string UserName, string Email, string Password, IdentityUserData Data)> _users = [];

    /// <summary>Mã lỗi mà <see cref="CreateUserAsync"/> sẽ trả về; mặc định là thành công.</summary>
    public IdentityFailure NextCreateFailure { get; set; } = IdentityFailure.None;

    /// <summary>Số lần <see cref="CreateUserAsync"/> được gọi.</summary>
    public int CreateCallCount { get; private set; }

    /// <summary>Thêm sẵn một người dùng với mật khẩu cho trước.</summary>
    public IdentityUserData AddUser(
        string userName,
        string email,
        string password,
        string displayName = "Người dùng thử",
        string accountStatus = "Active",
        IReadOnlyList<string>? roles = null)
    {
        var id = Guid.CreateVersion7();
        var data = new IdentityUserData(
            id,
            userName,
            email,
            displayName,
            Bio: string.Empty,
            AvatarUrl: null,
            CoverUrl: null,
            Location: null,
            DateOfBirth: null,
            FollowerCount: 0,
            FollowingCount: 0,
            FriendCount: 0,
            Language: "vi",
            AccountStatus: accountStatus,
            CreatedAt: new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero));

        _users[id] = (userName, email, password, data);
        Roles[id] = roles ?? ["User"];
        return data;
    }

    /// <summary>Role gán cho từng người dùng.</summary>
    public Dictionary<Guid, IReadOnlyList<string>> Roles { get; } = [];

    /// <summary>Đổi trạng thái tài khoản (mô phỏng admin khóa/mở khóa tài khoản).</summary>
    public void SetAccountStatus(Guid userId, string accountStatus)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            throw new InvalidOperationException($"Không tìm thấy người dùng {userId} trong fake.");
        }

        _users[userId] = (user.UserName, user.Email, user.Password, user.Data with { AccountStatus = accountStatus });
    }

    public Task<IdentityOperationResult> CreateUserAsync(
        string userName,
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        CreateCallCount++;

        if (NextCreateFailure != IdentityFailure.None)
        {
            return Task.FromResult(IdentityOperationResult.Fail(NextCreateFailure));
        }

        // Kiểm tra trùng giống hành vi của Identity để test luồng thất bại.
        if (_users.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(IdentityOperationResult.Fail(IdentityFailure.DuplicateEmail));
        }

        if (_users.Values.Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(IdentityOperationResult.Fail(IdentityFailure.DuplicateUserName));
        }

        AddUser(userName, email, password, displayName);
        return Task.FromResult(IdentityOperationResult.Ok());
    }

    public Task<IdentityUserData?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.TryGetValue(userId, out var user) ? user.Data : null);

    public Task<Guid?> ValidateCredentialsAsync(
        string emailOrUserName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var match = _users.FirstOrDefault(u =>
            string.Equals(u.Value.Email, emailOrUserName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Value.UserName, emailOrUserName, StringComparison.OrdinalIgnoreCase));

        // Không tiết lộ tài khoản có tồn tại: sai tài khoản và sai mật khẩu đều trả null.
        if (match.Value.Data is null || match.Value.Password != password)
        {
            return Task.FromResult<Guid?>(null);
        }

        // Tài khoản bị khóa cũng không cho đăng nhập (giống IdentityService thật).
        if (!string.Equals(match.Value.Data.AccountStatus, "Active", StringComparison.Ordinal))
        {
            return Task.FromResult<Guid?>(null);
        }

        return Task.FromResult<Guid?>(match.Key);
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.TryGetValue(userId, out var roles) ? roles : (IReadOnlyList<string>)[]);

    public Task<IdentityOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        string? bio,
        string? location,
        DateOnly? dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            return Task.FromResult(IdentityOperationResult.Fail(IdentityFailure.NotFound));
        }

        var updated = user.Data with
        {
            DisplayName = displayName ?? user.Data.DisplayName,
            Bio = bio ?? user.Data.Bio,
            Location = location ?? user.Data.Location,
            DateOfBirth = dateOfBirth ?? user.Data.DateOfBirth
        };

        _users[userId] = (user.UserName, user.Email, user.Password, updated);
        return Task.FromResult(IdentityOperationResult.Ok());
    }

    public Task<IdentityOperationResult> UpdateLanguageAsync(
        Guid userId,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(userId, out var user))
        {
            return Task.FromResult(IdentityOperationResult.Fail(IdentityFailure.NotFound));
        }

        _users[userId] = (user.UserName, user.Email, user.Password, user.Data with { Language = language });
        return Task.FromResult(IdentityOperationResult.Ok());
    }

    public Task<IReadOnlyList<AdminUserData>> ListUsersAsync(
        string? search,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _users.Values.Select(u => u.Data).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLowerInvariant().Contains(term) ||
                u.UserName.ToLowerInvariant().Contains(term) ||
                u.DisplayName.ToLowerInvariant().Contains(term));
        }

        var result = query
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Take(limit)
            .Select(u => new AdminUserData(
                u.Id,
                u.UserName,
                u.Email,
                u.DisplayName,
                u.AccountStatus,
                u.CreatedAt))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AdminUserData>>(result);
    }
}
