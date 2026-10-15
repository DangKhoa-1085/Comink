// Fake trong bộ nhớ cho cổng ITokenService, mô phỏng đúng cơ chế xoay vòng refresh token
// (kể cả phát hiện dùng lại token cũ) để test luồng nghiệp vụ mà không cần database.
using Comink.Application.Identity.Abstractions;

namespace Comink.Application.Tests.Fakes;

/// <summary>Bản giả của <see cref="ITokenService"/> lưu refresh token trong bộ nhớ.</summary>
public class FakeTokenService : ITokenService
{
    private sealed record Entry(Guid UserId, Guid FamilyId, DateTimeOffset ExpiresAt, bool Revoked);

    private readonly Dictionary<string, Entry> _tokens = [];
    private int _counter;

    public int AccessTokenLifetimeMinutes => 15;

    /// <summary>Thời điểm "hiện tại" giả lập, cho phép test token hết hạn.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Số lần thu hồi cả họ token đã xảy ra.</summary>
    public int FamilyRevocations { get; private set; }

    /// <summary>Các token đã bị thu hồi.</summary>
    public IReadOnlyCollection<string> RevokedTokens =>
        _tokens.Where(kv => kv.Value.Revoked).Select(kv => kv.Key).ToArray();

    public AccessTokenResult CreateAccessToken(Guid userId, string userName, IEnumerable<string> roles) =>
        new($"access-token-{userId}-{++_counter}", Now.AddMinutes(AccessTokenLifetimeMinutes));

    public Task<RefreshTokenResult> IssueRefreshTokenAsync(
        Guid userId,
        Guid? familyId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var family = familyId ?? Guid.CreateVersion7();
        var raw = $"refresh-{family}-{++_counter}";
        var expiresAt = Now.AddDays(30);

        _tokens[raw] = new Entry(userId, family, expiresAt, Revoked: false);
        return Task.FromResult(new RefreshTokenResult(raw, expiresAt, family));
    }

    public Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryGetValue(refreshToken, out var entry))
        {
            return Task.FromResult(new RefreshRotationResult(RefreshOutcome.Invalid, Guid.Empty, null));
        }

        // Dùng lại token đã thu hồi ⇒ thu hồi toàn bộ họ, giống JwtTokenService thật.
        if (entry.Revoked)
        {
            RevokeFamily(entry.FamilyId);
            return Task.FromResult(new RefreshRotationResult(RefreshOutcome.Reused, entry.UserId, null));
        }

        if (entry.ExpiresAt <= Now)
        {
            return Task.FromResult(new RefreshRotationResult(RefreshOutcome.Invalid, entry.UserId, null));
        }

        _tokens[refreshToken] = entry with { Revoked = true };

        var raw = $"refresh-{entry.FamilyId}-{++_counter}";
        var expiresAt = Now.AddDays(30);
        _tokens[raw] = new Entry(entry.UserId, entry.FamilyId, expiresAt, Revoked: false);

        return Task.FromResult(new RefreshRotationResult(
            RefreshOutcome.Rotated,
            entry.UserId,
            new RefreshTokenResult(raw, expiresAt, entry.FamilyId)));
    }

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Đăng xuất idempotent: token không tồn tại cũng coi như xong.
        if (_tokens.TryGetValue(refreshToken, out var entry))
        {
            _tokens[refreshToken] = entry with { Revoked = true };
        }

        return Task.CompletedTask;
    }

    private void RevokeFamily(Guid familyId)
    {
        foreach (var key in _tokens.Where(kv => kv.Value.FamilyId == familyId).Select(kv => kv.Key).ToArray())
        {
            _tokens[key] = _tokens[key] with { Revoked = true };
        }

        FamilyRevocations++;
    }
}
