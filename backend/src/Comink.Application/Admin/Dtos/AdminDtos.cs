// Yêu cầu của nhóm quản trị — docs/05_api_contract.md mục 4.
namespace Comink.Application.Admin.Dtos;

/// <summary>
/// Yêu cầu khóa tài khoản. `reason` là tùy chọn (tối đa 500 ký tự) và chỉ admin thấy.
/// </summary>
public record LockUserRequest(string? Reason);
