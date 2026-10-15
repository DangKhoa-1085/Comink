// DTO của nhóm Hồ sơ — hợp đồng với web/app (docs/05_api_contract.md mục 3).
namespace Comink.Application.Social.Dtos;

/// <summary>Hồ sơ đầy đủ của chính người dùng đang đăng nhập.</summary>
public record MyProfileDto(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    string? Location,
    DateOnly? DateOfBirth,
    int FollowerCount,
    int FollowingCount,
    int FriendCount,
    string Language,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

/// <summary>Yêu cầu sửa hồ sơ; trường nào bỏ trống thì giữ nguyên.</summary>
public record UpdateProfileRequest(
    string? DisplayName,
    string? Bio,
    string? Location,
    DateOnly? DateOfBirth);

/// <summary>Yêu cầu sửa cài đặt; Giai đoạn 1 chỉ có ngôn ngữ giao diện.</summary>
public record UpdateSettingsRequest(string? Language);
