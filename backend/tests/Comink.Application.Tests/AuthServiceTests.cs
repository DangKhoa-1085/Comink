// Test nghiệp vụ xác thực: đăng ký, đăng nhập, phân quyền và xoay vòng refresh token.
// Đây là phần nghiệp vụ lõi được yêu cầu kiểm thử ở AGENTS.md mục 10.
using Comink.Application.Common;
using Comink.Application.Identity;
using Comink.Application.Identity.Dtos;
using Comink.Application.Tests.Fakes;

namespace Comink.Application.Tests;

public class AuthServiceTests
{
    private const string ValidPassword = "MatKhau@123";

    private readonly FakeIdentityService _identity = new();
    private readonly FakeTokenService _tokens = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_identity, _tokens);
    }

    private static RefreshTokenContext Context() => new("Test device", "127.0.0.1", "xunit");

    // ---------------------------------------------------------------
    // Đăng ký
    // ---------------------------------------------------------------

    [Fact]
    public async Task Register_voi_du_lieu_hop_le_tra_ve_token_va_ho_so()
    {
        var result = await _sut.RegisterAsync(
            new RegisterRequest("khoa.nguyen", "khoa@example.com", ValidPassword, "Nguyễn Khoa"),
            Context());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("khoa@example.com", result.Value!.User.Email);
        Assert.Equal("khoa.nguyen", result.Value.User.UserName);
        Assert.Contains("User", result.Value.User.Roles);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
    }

    [Fact]
    public async Task Register_trung_email_tra_ma_loi_AUTH_EMAIL_ALREADY_EXISTS()
    {
        _identity.AddUser("nguoidung1", "trung@example.com", ValidPassword);
        _identity.NextCreateFailure = Application.Identity.Abstractions.IdentityFailure.DuplicateEmail;

        var result = await _sut.RegisterAsync(
            new RegisterRequest("nguoidung2", "trung@example.com", ValidPassword, "Người dùng 2"),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthEmailAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task Register_trung_ten_dang_nhap_tra_ma_loi_AUTH_USERNAME_ALREADY_EXISTS()
    {
        _identity.NextCreateFailure = Application.Identity.Abstractions.IdentityFailure.DuplicateUserName;

        var result = await _sut.RegisterAsync(
            new RegisterRequest("datten", "moi@example.com", ValidPassword, "Người dùng mới"),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthUserNameAlreadyExists, result.ErrorCode);
    }

    // ---------------------------------------------------------------
    // Đăng nhập
    // ---------------------------------------------------------------

    [Fact]
    public async Task Login_bang_email_dung_mat_khau_thanh_cong()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);

        var result = await _sut.LoginAsync(
            new LoginRequest("khoa@example.com", ValidPassword, "Chrome"),
            Context());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("khoa@example.com", result.Value!.User.Email);
    }

    [Fact]
    public async Task Login_bang_ten_dang_nhap_thanh_cong()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);

        var result = await _sut.LoginAsync(
            new LoginRequest("khoa.nguyen", ValidPassword, null),
            Context());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Login_sai_mat_khau_tra_AUTH_INVALID_CREDENTIALS()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);

        var result = await _sut.LoginAsync(
            new LoginRequest("khoa@example.com", "SaiMatKhau@999", null),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_tai_khoan_khong_ton_tai_tra_cung_ma_loi_nhu_sai_mat_khau()
    {
        // Không tiết lộ tài khoản có tồn tại hay không (chống dò tài khoản).
        var result = await _sut.LoginAsync(
            new LoginRequest("khong.ton.tai@example.com", ValidPassword, null),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_tai_khoan_bi_khoa_tra_AUTH_INVALID_CREDENTIALS()
    {
        // IdentityService thật cũng chặn ở bước kiểm tra mật khẩu khi account_status != Active.
        _identity.AddUser("bikhoa", "bikhoa@example.com", ValidPassword, accountStatus: "Locked");

        var result = await _sut.LoginAsync(
            new LoginRequest("bikhoa@example.com", ValidPassword, null),
            Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_phat_refresh_token_thuoc_ho_moi()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);

        var first = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());
        var second = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        // Hai lần đăng nhập là hai họ token khác nhau (mỗi phiên độc lập).
        Assert.NotEqual(first.Value!.RefreshToken, second.Value!.RefreshToken);
    }

    // ---------------------------------------------------------------
    // Làm mới token
    // ---------------------------------------------------------------

    [Fact]
    public async Task Refresh_xoay_vong_thanh_cong_va_tra_token_moi()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);
        var login = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());
        var originalRefreshToken = login.Value!.RefreshToken;

        var refreshed = await _sut.RefreshAsync(new RefreshTokenRequest(originalRefreshToken), Context());

        Assert.True(refreshed.Succeeded);
        Assert.NotNull(refreshed.Value);
        Assert.NotEqual(originalRefreshToken, refreshed.Value!.RefreshToken);
        Assert.Contains(originalRefreshToken, _tokens.RevokedTokens);
    }

    [Fact]
    public async Task Refresh_token_khong_hop_le_tra_AUTH_REFRESH_TOKEN_INVALID()
    {
        var result = await _sut.RefreshAsync(new RefreshTokenRequest("token-khong-ton-tai"), Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_token_het_han_tra_AUTH_REFRESH_TOKEN_INVALID()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);
        var login = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());

        // Đẩy đồng hồ qua thời hạn 30 ngày của refresh token.
        _tokens.Now = _tokens.Now.AddDays(31);

        var result = await _sut.RefreshAsync(new RefreshTokenRequest(login.Value!.RefreshToken), Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Dung_lai_refresh_token_cu_tra_AUTH_REFRESH_TOKEN_REUSED_va_thu_hoi_ca_ho()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);
        var login = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());
        var oldToken = login.Value!.RefreshToken;

        // Lần 1: xoay vòng bình thường → token cũ bị thu hồi, có token mới.
        var first = await _sut.RefreshAsync(new RefreshTokenRequest(oldToken), Context());
        Assert.True(first.Succeeded);
        var newToken = first.Value!.RefreshToken;

        // Lần 2: kẻ tấn công dùng lại token cũ ⇒ phải bị phát hiện.
        var reused = await _sut.RefreshAsync(new RefreshTokenRequest(oldToken), Context());

        Assert.False(reused.Succeeded);
        Assert.Equal(ErrorCodes.AuthRefreshTokenReused, reused.ErrorCode);
        Assert.Equal(1, _tokens.FamilyRevocations);

        // Toàn bộ họ bị thu hồi, kể cả token mới nhất → phiên hợp lệ cũng phải đăng nhập lại.
        Assert.Contains(newToken, _tokens.RevokedTokens);
        var afterRevocation = await _sut.RefreshAsync(new RefreshTokenRequest(newToken), Context());
        Assert.False(afterRevocation.Succeeded);
        Assert.Equal(ErrorCodes.AuthRefreshTokenReused, afterRevocation.ErrorCode);
    }

    [Fact]
    public async Task Refresh_tai_khoan_bi_khoa_sau_khi_dang_nhap_bi_chan()
    {
        var user = _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);
        var login = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());
        Assert.True(login.Succeeded);

        // Admin khóa tài khoản sau khi người dùng đã đăng nhập.
        _identity.SetAccountStatus(user.Id, "Locked");

        var result = await _sut.RefreshAsync(new RefreshTokenRequest(login.Value!.RefreshToken), Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.AuthAccountLocked, result.ErrorCode);
    }

    // ---------------------------------------------------------------
    // Thu hồi khi đăng xuất
    // ---------------------------------------------------------------

    [Fact]
    public async Task Logout_thu_hoi_refresh_token_va_goi_lai_van_thanh_cong()
    {
        _identity.AddUser("khoa.nguyen", "khoa@example.com", ValidPassword);
        var login = await _sut.LoginAsync(new LoginRequest("khoa@example.com", ValidPassword, null), Context());
        var refreshToken = login.Value!.RefreshToken;

        await _sut.LogoutAsync(refreshToken);
        Assert.Contains(refreshToken, _tokens.RevokedTokens);

        // Idempotent: gọi lại không được ném lỗi.
        await _sut.LogoutAsync(refreshToken);

        // Token đã thu hồi thì không dùng để làm mới được nữa.
        var result = await _sut.RefreshAsync(new RefreshTokenRequest(refreshToken), Context());
        Assert.False(result.Succeeded);
    }
}
