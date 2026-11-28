// Endpoint Comink Locket — /api/lockets
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Đăng, xem, thả cảm xúc, trả lời nhanh và xóa khoảnh khắc Locket (hiệu lực 24 giờ).</summary>
public static class LocketEndpoints
{
    public static IEndpointRouteBuilder MapLocketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lockets")
            .WithTags("Lockets")
            .RequireAuthorization("Authenticated");

        group.MapPost("/", CreateAsync)
            .WithName("CreateLocket")
            .Produces<LocketDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", GetFeedAsync)
            .WithName("GetLockets")
            .Produces<PagedList<LocketDto>>();

        group.MapPost("/{id:guid}/reactions", ReactAsync)
            .WithName("ReactLocket")
            .Produces<LocketDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        group.MapPost("/{id:guid}/reply", ReplyAsync)
            .WithName("ReplyLocket")
            .Produces<LocketReplyResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteLocket")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateLocketRequest request,
        IValidator<CreateLocketRequest> validator,
        ILocketService locketService,
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

        var result = await locketService.CreateAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, moment => Results.Created($"/api/lockets/{moment.Id}", moment));
    }

    private static async Task<IResult> GetFeedAsync(
        Guid? authorId,
        string? cursor,
        int? limit,
        ILocketService locketService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await locketService.GetFeedAsync(
            userId.Value, authorId, cursor, limit ?? 20, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> ReactAsync(
        Guid id,
        ReactLocketRequest request,
        IValidator<ReactLocketRequest> validator,
        ILocketService locketService,
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

        var result = await locketService.ReactAsync(userId.Value, id, request, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> ReplyAsync(
        Guid id,
        ReplyLocketRequest request,
        IValidator<ReplyLocketRequest> validator,
        ILocketService locketService,
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

        var result = await locketService.ReplyAsync(userId.Value, id, request, cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ILocketService locketService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await locketService.DeleteAsync(userId.Value, id, cancellationToken);
        return result.Succeeded
            ? Results.NoContent()
            : ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
    }
}
