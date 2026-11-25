// Nghiệp vụ hồ sơ cá nhân: xem và sửa thông tin của chính mình.
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social;

/// <summary>Dịch vụ hồ sơ cá nhân.</summary>
public class ProfileService
{
    /// <summary>Ngôn ngữ giao diện được hỗ trợ (docs/01_COMINK_SPEC.md mục 3.9).</summary>
    public static readonly string[] SupportedLanguages = ["vi", "en"];

    private readonly IIdentityService _identityService;

    public ProfileService(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    /// <summary>Lấy hồ sơ của người dùng đang đăng nhập.</summary>
    public async Task<OperationResult<MyProfileDto>> GetMyProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _identityService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return OperationResult<MyProfileDto>.Failure(ErrorCodes.NotFound);
        }

        var roles = await _identityService.GetRolesAsync(userId, cancellationToken);
        return OperationResult<MyProfileDto>.Success(Map(user, roles));
    }

    /// <summary>Sửa hồ sơ của người dùng đang đăng nhập.</summary>
    public async Task<OperationResult<MyProfileDto>> UpdateMyProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _identityService.UpdateProfileAsync(
            userId,
            request.DisplayName,
            request.Bio,
            request.Location,
            request.DateOfBirth,
            cancellationToken);

        if (!updated.Succeeded)
        {
            return OperationResult<MyProfileDto>.Failure(
                updated.Error == IdentityFailure.NotFound ? ErrorCodes.NotFound : ErrorCodes.InternalError);
        }

        return await GetMyProfileAsync(userId, cancellationToken);
    }

    /// <summary>Sửa cài đặt của người dùng đang đăng nhập (Giai đoạn 1: ngôn ngữ).</summary>
    public async Task<OperationResult<MyProfileDto>> UpdateMySettingsAsync(
        Guid userId,
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Language is not null)
        {
            var updated = await _identityService.UpdateLanguageAsync(userId, request.Language, cancellationToken);
            if (!updated.Succeeded)
            {
                return OperationResult<MyProfileDto>.Failure(
                    updated.Error == IdentityFailure.NotFound ? ErrorCodes.NotFound : ErrorCodes.InternalError);
            }
        }

        return await GetMyProfileAsync(userId, cancellationToken);
    }

    private static MyProfileDto Map(IdentityUserData user, IReadOnlyList<string> roles) =>
        new(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            string.IsNullOrWhiteSpace(user.Bio) ? null : user.Bio,
            user.AvatarUrl,
            user.CoverUrl,
            user.Location,
            user.DateOfBirth,
            user.FollowerCount,
            user.FollowingCount,
            user.FriendCount,
            user.Language,
            roles,
            user.CreatedAt);
}
