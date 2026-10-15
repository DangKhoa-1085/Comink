// Test tích hợp trên PostgreSQL thật (Testcontainers) theo yêu cầu ở AGENTS.md mục 10.
// Phạm vi: phần nghiệp vụ quan trọng nhất của Giai đoạn 1 — lưu băm refresh token,
// xoay vòng token, phát hiện dùng lại token cũ và sinh access token JWT đúng claim.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Comink.Application.Identity.Abstractions;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Comink.Api.IntegrationTests;

public class IdentityPersistenceTests : IntegrationTestBase
{
    private const string JwtSecret = "integration_test_secret_must_be_at_least_32_chars_long";

    private readonly TimeProvider _time = TimeProvider.System;

    public IdentityPersistenceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private static JwtTokenService CreateTokenService(ApplicationDbContext db, TimeProvider time) =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "comink-api",
                Audience = "comink-clients",
                Secret = JwtSecret,
                AccessTokenExpiryMinutes = 15,
                RefreshTokenExpiryDays = 30
            }),
            db,
            time);

    /// <summary>Tạo một người dùng tối thiểu để refresh token có khóa ngoại hợp lệ.</summary>
    private static async Task<Guid> SeedUserAsync(ApplicationDbContext db, string userName)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = $"{userName}@example.com",
            AccountStatus = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            Profile = new UserProfile { DisplayName = userName },
            Settings = new UserSettings()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Migration_M0_tao_duoc_extension_va_cot_theo_tai_lieu()
    {
        await using var db = await CreateDbContextAsync();

        // Extension phục vụ tìm kiếm tiếng Việt không dấu (03_database_design.md mục 5.1).
        var extensions = await db.Database
            .SqlQuery<string>($"SELECT extname AS \"Value\" FROM pg_extension WHERE extname IN ('pg_trgm','unaccent')")
            .ToListAsync();
        Assert.Contains("pg_trgm", extensions);
        Assert.Contains("unaccent", extensions);

        // users.updated_at phải tồn tại (đã từng bị thiếu — docs/tasks/phase0_remediation.md).
        var usersUpdatedAt = await db.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name='users' AND column_name='updated_at'")
            .ToListAsync();
        Assert.Equal("updated_at", Assert.Single(usersUpdatedAt));

        // user_settings.notification_settings phải là jsonb.
        var notificationType = await db.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='user_settings' AND column_name='notification_settings'")
            .ToListAsync();
        Assert.Equal("jsonb", Assert.Single(notificationType));

        // user_name phải là varchar(32) theo tài liệu.
        var userNameLength = await db.Database
            .SqlQuery<int>($"SELECT character_maximum_length AS \"Value\" FROM information_schema.columns WHERE table_name='users' AND column_name='user_name'")
            .ToListAsync();
        Assert.Equal(32, Assert.Single(userNameLength));
    }

    [Fact]
    public async Task Refresh_token_luu_dang_bam_va_xoay_vong_tao_dong_moi()
    {
        await using var db = await CreateDbContextAsync();
        var userId = await SeedUserAsync(db, "xoayvong");
        var sut = CreateTokenService(db, _time);

        // Phát token đầu tiên (họ mới).
        var first = await sut.IssueRefreshTokenAsync(userId, null, "xunit", "127.0.0.1", "test", default);
        Assert.False(string.IsNullOrWhiteSpace(first.Token));

        // Không được lưu token thô — database chỉ lưu băm SHA-256 dạng hex.
        var storedToken = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);
        Assert.NotEqual(first.Token, storedToken.TokenHash);
        Assert.Equal(64, storedToken.TokenHash.Length);

        // Xoay vòng: token cũ bị thu hồi, có dòng mới, cùng họ.
        var rotation = await sut.RotateRefreshTokenAsync(first.Token, "xunit", "127.0.0.1", "test", default);
        Assert.Equal(RefreshOutcome.Rotated, rotation.Outcome);
        Assert.NotNull(rotation.NewToken);

        var all = await db.RefreshTokens.Where(t => t.UserId == userId).OrderBy(t => t.CreatedAt).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.NotNull(all[0].RevokedAt);                       // token cũ đã bị thu hồi
        Assert.Equal(all[1].Id, all[0].ReplacedByTokenId);      // có liên kết tới token thay thế
        Assert.Equal(all[0].FamilyId, all[1].FamilyId);         // cùng một họ
        Assert.Null(all[1].RevokedAt);                          // token mới còn hiệu lực
    }

    [Fact]
    public async Task Dung_lai_refresh_token_cu_thu_hoi_toan_bo_ho()
    {
        await using var db = await CreateDbContextAsync();
        var userId = await SeedUserAsync(db, "danhlai");
        var sut = CreateTokenService(db, _time);

        var first = await sut.IssueRefreshTokenAsync(userId, null, null, null, null, default);
        var rotated = await sut.RotateRefreshTokenAsync(first.Token, null, null, null, default);
        Assert.Equal(RefreshOutcome.Rotated, rotated.Outcome);
        var newToken = rotated.NewToken!.Token;

        // Dùng lại token cũ ⇒ phát hiện dùng lại (coi như bị đánh cắp).
        var reused = await sut.RotateRefreshTokenAsync(first.Token, null, null, null, default);
        Assert.Equal(RefreshOutcome.Reused, reused.Outcome);

        // Toàn bộ họ (kể cả token mới nhất) phải bị thu hồi.
        var active = await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null);
        Assert.Equal(0, active);

        var afterRevocation = await sut.RotateRefreshTokenAsync(newToken, null, null, null, default);
        Assert.Equal(RefreshOutcome.Reused, afterRevocation.Outcome);
    }

    [Fact]
    public async Task Refresh_token_het_han_tra_invalid()
    {
        await using var db = await CreateDbContextAsync();
        var userId = await SeedUserAsync(db, "hethan");

        // Đồng hồ giả: phát token ở quá khứ rồi kiểm tra ở tương lai.
        var issuingService = CreateTokenService(db, new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var issued = await issuingService.IssueRefreshTokenAsync(userId, null, null, null, null, default);

        var laterService = CreateTokenService(db, new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await laterService.RotateRefreshTokenAsync(issued.Token, null, null, null, default);

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Revoke_refresh_token_la_idempotent()
    {
        await using var db = await CreateDbContextAsync();
        var userId = await SeedUserAsync(db, "dangxuat");
        var sut = CreateTokenService(db, _time);

        var issued = await sut.IssueRefreshTokenAsync(userId, null, null, null, null, default);
        await sut.RevokeRefreshTokenAsync(issued.Token, default);

        var token = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);
        Assert.NotNull(token.RevokedAt);

        // Gọi lại (kể cả với token không tồn tại) không được ném lỗi.
        await sut.RevokeRefreshTokenAsync("token-khong-ton-tai", default);
        await sut.RevokeRefreshTokenAsync(issued.Token, default);
    }

    [Fact]
    public async Task Access_token_chua_dung_claim_user_va_role()
    {
        await using var db = await CreateDbContextAsync();
        var userId = Guid.CreateVersion7();
        var sut = CreateTokenService(db, _time);

        var result = sut.CreateAccessToken(userId, "khoa.nguyen", ["User", "Admin"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal("comink-api", token.Issuer);
        Assert.Contains("comink-clients", token.Audiences);

        // nameidentifier là id người dùng — hub SignalR dùng claim này để đưa vào nhóm user:{id}.
        Assert.Equal(userId.ToString(), token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("khoa.nguyen", token.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.True(result.ExpiresAt > _time.GetUtcNow());
    }

    /// <summary>Đồng hồ giả để test hết hạn mà không phải chờ.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
