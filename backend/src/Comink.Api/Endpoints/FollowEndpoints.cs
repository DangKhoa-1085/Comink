// Endpoint nhóm Theo dõi — /api/follows và danh sách người theo dõi
// (docs/tasks/phase2_social.md mục 3.3).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;

namespace Comink.Api.Endpoints;

/// <summary>Theo dõi / bỏ theo dõi và xem danh sách người theo dõi.</summary>
public static class FollowEndpoints
{
    public static IEndpointRouteBuilder MapFollowEndpoints(this IEndpointRouteBuilder app)
    {
        var follows = app.MapGroup("/api/follows")
            .WithTags("Follows")
            .RequireAuthorization("Authenticated");

        follows.MapPost("/{userId:guid}", FollowAsync)
            .WithName("FollowUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        follows.MapDelete("/{userId:guid}", UnfollowAsync)
            .WithName("UnfollowUser")
            .Produces(StatusCodes.Status204NoContent);

        // Danh sách nằm dưới /api/users/{id} theo hợp đồng.
        var users = app.MapGroup("/api/users")
            .WithTags("Follows")
            .RequireAuthorization("Authenticated");

        users.MapGet("/{id:guid}/followers", GetFollowersAsync)
            .WithName("GetFollowers")
            .Produces<PagedList<UserSummaryDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapGet("/{id:guid}/following", GetFollowingAsync)
            .WithName("GetFollowing")
            .Produces<PagedList<UserSummaryDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> FollowAsync(
        Guid userId,
        IFollowService followService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUser.GetUserId(httpContext.User);
        if (currentUserId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await followService.FollowAsync(currentUserId.Value, userId, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> UnfollowAsync(
        Guid userId,
        IFollowService followService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUser.GetUserId(httpContext.User);
        if (currentUserId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await followService.UnfollowAsync(currentUserId.Value, userId, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> GetFollowersAsync(
        Guid id,
        IFollowService followService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null)
    {
        var currentUserId = CurrentUser.GetUserId(httpContext.User);
        if (currentUserId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await followService.GetFollowersAsync(
            currentUserId.Value, id, cursor, limit ?? SocialPaging.DefaultLimit, cancellationToken);
        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetFollowingAsync(
        Guid id,
        IFollowService followService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null)
    {
        var currentUserId = CurrentUser.GetUserId(httpContext.User);
        if (currentUserId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await followService.GetFollowingAsync(
            currentUserId.Value, id, cursor, limit ?? SocialPaging.DefaultLimit, cancellationToken);
        return ApiResults.FromResult(result, page => Results.Ok(page));
    }
}
