// Endpoint bài viết, bảng tin và thích — /api/posts, /api/feed (docs/05_api_contract.md mục 7.4–7.5).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Tạo / xem / sửa / xóa bài viết, bảng tin và thích bài.</summary>
public static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var posts = app.MapGroup("/api/posts")
            .WithTags("Posts")
            .RequireAuthorization("Authenticated");

        posts.MapPost("/", CreateAsync)
            .WithName("CreatePost")
            .Produces<PostDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        posts.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetPost")
            .Produces<PostDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("UpdatePost")
            .Produces<PostDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        posts.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePost")
            .Produces(StatusCodes.Status204NoContent);

        posts.MapPost("/{id:guid}/likes", LikeAsync)
            .WithName("LikePost")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapDelete("/{id:guid}/likes", UnlikeAsync)
            .WithName("UnlikePost")
            .Produces(StatusCodes.Status204NoContent);

        var feed = app.MapGroup("/api/feed")
            .WithTags("Posts")
            .RequireAuthorization("Authenticated");

        feed.MapGet("/", GetFeedAsync)
            .WithName("GetFeed")
            .Produces<PagedList<PostDto>>();

        // Bài trên trang cá nhân nằm dưới /api/users theo hợp đồng.
        var users = app.MapGroup("/api/users")
            .WithTags("Posts")
            .RequireAuthorization("Authenticated");

        users.MapGet("/{id:guid}/posts", GetUserPostsAsync)
            .WithName("GetUserPosts")
            .Produces<PagedList<PostDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreatePostRequest request,
        IValidator<CreatePostRequest> validator,
        IPostService postService,
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

        var result = await postService.CreateAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, post => Results.Created($"/api/posts/{post.Id}", post));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        IPostService postService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await postService.GetByIdAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, post => Results.Ok(post));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdatePostRequest request,
        IValidator<UpdatePostRequest> validator,
        IPostService postService,
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

        var result = await postService.UpdateAsync(userId.Value, id, request, cancellationToken);
        return ApiResults.FromResult(result, post => Results.Ok(post));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IPostService postService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await postService.DeleteAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> LikeAsync(
        Guid id,
        ILikeService likeService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await likeService.LikeAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> UnlikeAsync(
        Guid id,
        ILikeService likeService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await likeService.UnlikeAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> GetFeedAsync(
        IPostService postService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await postService.GetFeedAsync(
            userId.Value, cursor, SocialPaging.Normalize(limit), cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetUserPostsAsync(
        Guid id,
        IPostService postService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await postService.GetUserPostsAsync(
            userId.Value, id, cursor, SocialPaging.Normalize(limit), cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }
}
