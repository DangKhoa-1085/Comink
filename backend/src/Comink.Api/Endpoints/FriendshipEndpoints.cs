// Endpoint nhóm Bạn bè — /api/friendships (docs/tasks/phase2_social.md mục 3.2).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Gửi / chấp nhận / từ chối lời mời kết bạn và danh sách bạn bè.</summary>
public static class FriendshipEndpoints
{
    public static IEndpointRouteBuilder MapFriendshipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/friendships")
            .WithTags("Friendships")
            .RequireAuthorization("Authenticated");

        group.MapPost("/requests", SendRequestAsync)
            .WithName("SendFriendRequest")
            .Produces<FriendshipDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/requests/incoming", GetIncomingAsync)
            .WithName("GetIncomingFriendRequests")
            .Produces<PagedList<FriendshipDto>>();

        group.MapGet("/requests/outgoing", GetOutgoingAsync)
            .WithName("GetOutgoingFriendRequests")
            .Produces<PagedList<FriendshipDto>>();

        group.MapPost("/requests/{id:guid}/accept", AcceptAsync)
            .WithName("AcceptFriendRequest")
            .Produces<FriendshipDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/requests/{id:guid}", RemoveRequestAsync)
            .WithName("RemoveFriendRequest")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/", GetFriendsAsync)
            .WithName("GetFriends")
            .Produces<PagedList<FriendshipDto>>();

        group.MapDelete("/{userId:guid}", RemoveFriendAsync)
            .WithName("RemoveFriend")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> SendRequestAsync(
        SendFriendRequestRequest request,
        IValidator<SendFriendRequestRequest> validator,
        IFriendshipService friendshipService,
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

        var result = await friendshipService.SendRequestAsync(userId.Value, request.AddresseeId, cancellationToken);
        return ApiResults.FromResult(
            result,
            value => Results.Created($"/api/friendships/{value.Id}", value));
    }

    private static async Task<IResult> AcceptAsync(
        Guid id,
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await friendshipService.AcceptAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, value => Results.Ok(value));
    }

    private static async Task<IResult> RemoveRequestAsync(
        Guid id,
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await friendshipService.RemoveRequestAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> RemoveFriendAsync(
        Guid userId,
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = CurrentUser.GetUserId(httpContext.User);
        if (currentUserId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await friendshipService.RemoveFriendAsync(currentUserId.Value, userId, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static Task<IResult> GetFriendsAsync(
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null) =>
        GetListAsync(friendshipService.GetFriendsAsync, friendshipService, httpContext, cursor, limit, cancellationToken);

    private static Task<IResult> GetIncomingAsync(
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null) =>
        GetListAsync(friendshipService.GetIncomingRequestsAsync, friendshipService, httpContext, cursor, limit, cancellationToken);

    private static Task<IResult> GetOutgoingAsync(
        IFriendshipService friendshipService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null) =>
        GetListAsync(friendshipService.GetOutgoingRequestsAsync, friendshipService, httpContext, cursor, limit, cancellationToken);

    private static async Task<IResult> GetListAsync(
        Func<Guid, string?, int, CancellationToken, Task<OperationResult<PagedList<FriendshipDto>>>> fetch,
        IFriendshipService _,
        HttpContext httpContext,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await fetch(userId.Value, cursor, limit ?? SocialPaging.DefaultLimit, cancellationToken);
        return ApiResults.FromResult(result, page => Results.Ok(page));
    }
}
