// Endpoint bình luận — /api/posts/{id}/comments, /api/comments/{id}
// (docs/05_api_contract.md mục 7.5).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Tạo / sửa / xóa bình luận và danh sách trả lời.</summary>
public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var postComments = app.MapGroup("/api/posts/{postId:guid}/comments")
            .WithTags("Comments")
            .RequireAuthorization("Authenticated");

        postComments.MapPost("/", CreateAsync)
            .WithName("CreateComment")
            .Produces<CommentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        postComments.MapGet("/", GetCommentsAsync)
            .WithName("GetPostComments")
            .Produces<PagedList<CommentDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var comments = app.MapGroup("/api/comments")
            .WithTags("Comments")
            .RequireAuthorization("Authenticated");

        comments.MapGet("/{id:guid}/replies", GetRepliesAsync)
            .WithName("GetCommentReplies")
            .Produces<PagedList<CommentDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        comments.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("UpdateComment")
            .Produces<CommentDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        comments.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteComment")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        Guid postId,
        CreateCommentRequest request,
        IValidator<CreateCommentRequest> validator,
        ICommentService commentService,
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

        var result = await commentService.CreateAsync(userId.Value, postId, request, cancellationToken);
        return ApiResults.FromResult(result, comment => Results.Created($"/api/comments/{comment.Id}", comment));
    }

    private static async Task<IResult> GetCommentsAsync(
        Guid postId,
        ICommentService commentService,
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

        var result = await commentService.GetPostCommentsAsync(
            userId.Value, postId, cursor, SocialPaging.Normalize(limit), cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetRepliesAsync(
        Guid id,
        ICommentService commentService,
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

        var result = await commentService.GetRepliesAsync(
            userId.Value, id, cursor, SocialPaging.Normalize(limit), cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCommentRequest request,
        IValidator<UpdateCommentRequest> validator,
        ICommentService commentService,
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

        var result = await commentService.UpdateAsync(userId.Value, id, request, cancellationToken);
        return ApiResults.FromResult(result, comment => Results.Ok(comment));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ICommentService commentService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await commentService.DeleteAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }
}
