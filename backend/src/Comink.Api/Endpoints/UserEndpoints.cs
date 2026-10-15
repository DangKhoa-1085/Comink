// Endpoint nhóm Hồ sơ — /api/users (docs/05_api_contract.md mục 3).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social;
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Đăng ký nhóm endpoint hồ sơ cá nhân.</summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization("Authenticated");

        group.MapGet("/me", GetMyProfileAsync)
            .WithName("GetMyProfile")
            .Produces<MyProfileDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me/profile", UpdateMyProfileAsync)
            .WithName("UpdateMyProfile")
            .Produces<MyProfileDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/me/settings", UpdateMySettingsAsync)
            .WithName("UpdateMySettings")
            .Produces<MyProfileDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetMyProfileAsync(
        ProfileService profileService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await profileService.GetMyProfileAsync(userId.Value, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        IValidator<UpdateProfileRequest> validator,
        ProfileService profileService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await profileService.UpdateMyProfileAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> UpdateMySettingsAsync(
        UpdateSettingsRequest request,
        IValidator<UpdateSettingsRequest> validator,
        ProfileService profileService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await profileService.UpdateMySettingsAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }
}
