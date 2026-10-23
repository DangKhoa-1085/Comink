// Cài đặt cổng ITokenService: sinh JWT và quản lý refresh token xoay vòng.
using System.Security.Cryptography;
using System.Text;
using Comink.Application.Identity.Abstractions;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Comink.Infrastructure.Identity;

/// <summary>Cấu hình JWT, đọc từ mục <c>Jwt</c> trong cấu hình.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "comink-api";
    public string Audience { get; set; } = "comink-clients";

    /// <summary>Bắt buộc dài tối thiểu 32 ký tự — kiểm tra lúc khởi động.</summary>
    public string Secret { get; set; } = string.Empty;

    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}

/// <summary>
/// Sinh access token (JWT) và quản lý refresh token.
/// Refresh token chỉ lưu dạng băm SHA-256; token thô chỉ trả cho client một lần.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, ApplicationDbContext db, TimeProvider timeProvider)
    {
        _options = options.Value;
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public int AccessTokenLifetimeMinutes => _options.AccessTokenExpiryMinutes;

    /// <inheritdoc />
    public AccessTokenResult CreateAccessToken(Guid userId, string userName, IEnumerable<string> roles)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenResult> IssueRefreshTokenAsync(
        Guid userId,
        Guid? familyId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var rawToken = GenerateRawToken();
        var family = familyId ?? Guid.CreateVersion7();
        var expiresAt = now.AddDays(_options.RefreshTokenExpiryDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            FamilyId = family,
            DeviceName = Truncate(deviceName, 100),
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 512),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new RefreshTokenResult(rawToken, expiresAt, family);
    }

    /// <inheritdoc />
    public async Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var hash = HashToken(refreshToken);

        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return new RefreshRotationResult(RefreshOutcome.Invalid, Guid.Empty, null);
        }

        // Token đã bị thu hồi mà vẫn được dùng lại → coi như bị đánh cắp:
        // thu hồi toàn bộ họ token để vô hiệu hóa kẻ tấn công và cả phiên hợp lệ.
        if (existing.RevokedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            return new RefreshRotationResult(RefreshOutcome.Reused, existing.UserId, null);
        }

        if (existing.ExpiresAt <= now)
        {
            return new RefreshRotationResult(RefreshOutcome.Invalid, existing.UserId, null);
        }

        var rawToken = GenerateRawToken();
        var expiresAt = now.AddDays(_options.RefreshTokenExpiryDays);
        var newTokenId = Guid.CreateVersion7();

        existing.RevokedAt = now;
        existing.ReplacedByTokenId = newTokenId;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = newTokenId,
            UserId = existing.UserId,
            TokenHash = HashToken(rawToken),
            FamilyId = existing.FamilyId,
            DeviceName = Truncate(deviceName, 100) ?? existing.DeviceName,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 512),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshRotationResult(
            RefreshOutcome.Rotated,
            existing.UserId,
            new RefreshTokenResult(rawToken, expiresAt, existing.FamilyId));
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // Đăng xuất là thao tác idempotent: token không tồn tại cũng coi như đã đăng xuất.
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        if (tokens.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return tokens.Count;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        if (tokens.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Sinh chuỗi ngẫu nhiên 64 byte, mã hóa base64url (không có ký tự gây rối URL).</summary>
    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    /// <summary>Băm SHA-256 của token; database chỉ lưu giá trị này.</summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : (value.Length <= maxLength ? value : value[..maxLength]);
}
