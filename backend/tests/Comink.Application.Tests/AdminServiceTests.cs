// Test nghiệp vụ quản trị tài khoản (Giai đoạn 5): khóa/mở khóa, tự khóa, thu hồi phiên.
using Comink.Application.Admin;
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using Comink.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Comink.Application.Tests;

public class AdminServiceTests
{
    private const string ValidPassword = "MatKhau@123";

    private readonly FakeIdentityService _identity = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeAccountNotifier _notifier = new();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(
            _identity,
            _tokens,
            _notifier,
            TimeProvider.System,
            NullLogger<AdminService>.Instance);
    }

    // ---------------------------------------------------------------
    // Khóa tài khoản
    // ---------------------------------------------------------------

    [Fact]
    public async Task LockUser_thanh_cong_ghi_trang_thai_thu_hoi_token_va_phat_su_kien()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        // Giả lập người dùng đang có 2 phiên đăng nhập (2 họ refresh token).
        var tokenA = await _tokens.IssueRefreshTokenAsync(target.Id, null, "Chrome", null, null);
        var tokenB = await _tokens.IssueRefreshTokenAsync(target.Id, null, "Firefox", null, null);

        var result = await _sut.LockUserAsync(admin.Id, target.Id, "  Vi phạm quy định  ");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("Locked", result.Value!.AccountStatus);
        Assert.Equal("Vi phạm quy định", result.Value.LockedReason);
        Assert.NotNull(result.Value.LockedAt);

        var stored = await _identity.GetByIdAsync(target.Id);
        Assert.Equal("Locked", stored!.AccountStatus);

        // Phiên hiện tại và mọi phiên cũ đều bị thu hồi (roadmap GĐ 5 #4).
        Assert.Contains(tokenA.Token, _tokens.RevokedTokens);
        Assert.Contains(tokenB.Token, _tokens.RevokedTokens);

        // Có thông báo để client đăng xuất + ngắt SignalR.
        Assert.Single(_notifier.LockedUserIds);
        Assert.Equal(target.Id, _notifier.LockedUserIds[0]);
    }

    [Fact]
    public async Task LockUser_admin_tu_khoa_chinh_minh_bi_tu_choi()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var token = await _tokens.IssueRefreshTokenAsync(admin.Id, null, "Chrome", null, null);

        var result = await _sut.LockUserAsync(admin.Id, admin.Id, "tự khóa");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AdminCannotLockSelf, result.ErrorCode);

        // Không được thay đổi dữ liệu, không thu hồi phiên, không phát sự kiện.
        var stored = await _identity.GetByIdAsync(admin.Id);
        Assert.Equal("Active", stored!.AccountStatus);
        Assert.DoesNotContain(token.Token, _tokens.RevokedTokens);
        Assert.Empty(_notifier.LockedUserIds);
    }

    [Fact]
    public async Task LockUser_khong_ton_tai_tra_USER_NOT_FOUND()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);

        var result = await _sut.LockUserAsync(admin.Id, Guid.CreateVersion7(), null);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task LockUser_da_khoa_truoc_do_la_idempotent_va_khong_thu_hoi_lai()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        var first = await _sut.LockUserAsync(admin.Id, target.Id, "lý do đầu");
        Assert.True(first.Succeeded);

        var second = await _sut.LockUserAsync(admin.Id, target.Id, "lý do thứ hai");

        Assert.True(second.Succeeded);
        // Không ghi đè lý do khóa ban đầu và không phát sự kiện/không thu hồi thêm.
        Assert.Equal("lý do đầu", second.Value!.LockedReason);
        Assert.Single(_notifier.LockedUserIds);
    }

    [Fact]
    public async Task LockUser_ly_do_qua_dai_tra_loi_validate()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        var result = await _sut.LockUserAsync(admin.Id, target.Id, new string('x', 501));

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.ContainsKey("reason"));
    }

    [Fact]
    public async Task LockUser_ly_do_toan_khoang_trang_luu_thanh_null()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        var result = await _sut.LockUserAsync(admin.Id, target.Id, "   ");

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.LockedReason);
    }

    // ---------------------------------------------------------------
    // Mở khóa tài khoản
    // ---------------------------------------------------------------

    [Fact]
    public async Task UnlockUser_mo_khoa_thanh_cong_va_xoa_dau_vet_khoa()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        await _sut.LockUserAsync(admin.Id, target.Id, "lý do");
        var result = await _sut.UnlockUserAsync(admin.Id, target.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Active", result.Value!.AccountStatus);
        Assert.Null(result.Value.LockedReason);
        Assert.Null(result.Value.LockedAt);

        var stored = await _identity.GetByIdAsync(target.Id);
        Assert.Equal("Active", stored!.AccountStatus);
        Assert.Null(stored.LockedReason);
    }

    [Fact]
    public async Task UnlockUser_tai_khoan_dang_hoat_dong_la_idempotent()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);
        var target = _identity.AddUser("target", "target@example.com", ValidPassword);

        var result = await _sut.UnlockUserAsync(admin.Id, target.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Active", result.Value!.AccountStatus);
        Assert.Empty(_notifier.LockedUserIds);
    }

    [Fact]
    public async Task UnlockUser_khong_ton_tai_tra_USER_NOT_FOUND()
    {
        var admin = _identity.AddUser("admin", "admin@example.com", ValidPassword, roles: ["Admin"]);

        var result = await _sut.UnlockUserAsync(admin.Id, Guid.CreateVersion7());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.UserNotFound, result.ErrorCode);
    }
}
