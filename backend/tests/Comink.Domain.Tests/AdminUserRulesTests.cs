// Test quy tắc quản trị tài khoản (Giai đoạn 5): tự khóa, idempotent, chuẩn hóa lý do.
// Đây là phần nghiệp vụ lõi được yêu cầu kiểm thử ở AGENTS.md mục 10.
using Comink.Domain.Admin;

namespace Comink.Domain.Tests;

public class AdminUserRulesTests
{
    private static readonly Guid AdminId = Guid.Parse("0199aaaa-0000-7000-8000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("0199aaaa-0000-7000-8000-000000000002");

    // ---------------------------------------------------------------
    // Khóa tài khoản
    // ---------------------------------------------------------------

    [Fact]
    public void EvaluateLock_admin_tu_khoa_chinh_minh_bi_tu_choi()
    {
        var action = AdminUserRules.EvaluateLock(AdminId, AdminId, AdminUserRules.ActiveStatus);

        Assert.Equal(AdminAccountAction.CannotLockSelf, action);
    }

    [Fact]
    public void EvaluateLock_tu_khoa_chinh_minh_bi_tu_choi_ke_ca_khi_da_khoa()
    {
        // Thứ tự kiểm tra: tự khóa luôn bị từ chối trước, không trả "đã ở trạng thái đích".
        var action = AdminUserRules.EvaluateLock(AdminId, AdminId, AdminUserRules.LockedStatus);

        Assert.Equal(AdminAccountAction.CannotLockSelf, action);
    }

    [Fact]
    public void EvaluateLock_tai_khoan_dang_hoat_dong_duoc_phep()
    {
        Assert.Equal(
            AdminAccountAction.Apply,
            AdminUserRules.EvaluateLock(AdminId, OtherUserId, AdminUserRules.ActiveStatus));
    }

    [Fact]
    public void EvaluateLock_tai_khoan_da_khoa_la_idempotent()
    {
        Assert.Equal(
            AdminAccountAction.AlreadyInState,
            AdminUserRules.EvaluateLock(AdminId, OtherUserId, AdminUserRules.LockedStatus));
    }

    // ---------------------------------------------------------------
    // Mở khóa tài khoản
    // ---------------------------------------------------------------

    [Fact]
    public void EvaluateUnlock_tai_khoan_bi_khoa_duoc_phep()
    {
        Assert.Equal(
            AdminAccountAction.Apply,
            AdminUserRules.EvaluateUnlock(AdminUserRules.LockedStatus));
    }

    [Fact]
    public void EvaluateUnlock_tai_khoan_dang_hoat_dong_la_idempotent()
    {
        Assert.Equal(
            AdminAccountAction.AlreadyInState,
            AdminUserRules.EvaluateUnlock(AdminUserRules.ActiveStatus));
    }

    // ---------------------------------------------------------------
    // Trạng thái và lý do khóa
    // ---------------------------------------------------------------

    [Fact]
    public void IsLocked_va_IsActive_so_sanh_dung_chuoi_trang_thai()
    {
        Assert.True(AdminUserRules.IsLocked("Locked"));
        Assert.False(AdminUserRules.IsLocked("locked"));
        Assert.False(AdminUserRules.IsLocked(null));

        Assert.True(AdminUserRules.IsActive("Active"));
        Assert.False(AdminUserRules.IsActive("Locked"));
        Assert.False(AdminUserRules.IsActive(null));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Vi phạm quy định  ", "Vi phạm quy định")]
    public void NormalizeLockReason_cat_khoang_trang_va_doi_chuoi_rong_thanh_null(
        string? input,
        string? expected)
    {
        Assert.Equal(expected, AdminUserRules.NormalizeLockReason(input));
    }

    [Fact]
    public void MaxLockReasonLength_khop_cot_locked_reason()
    {
        // users.locked_reason là varchar(500) (docs/03_database_design.md mục 3.1).
        Assert.Equal(500, AdminUserRules.MaxLockReasonLength);
    }
}
