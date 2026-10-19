// Endpoint cuộc gọi — /api/calls (docs/05_api_contract.md mục 9.2–9.4).
using Comink.Api.Common;
using Comink.Application.Calls.Abstractions;
using Comink.Application.Calls.Dtos;
using Comink.Application.Common;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Bắt đầu/trả lời/từ chối/kết thúc cuộc gọi, cấp token LiveKit và lịch sử.</summary>
public static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var calls = app.MapGroup("/api/calls")
            .WithTags("Calls")
            .RequireAuthorization("Authenticated");

        calls.MapPost("/", StartAsync)
            .WithName("StartCall")
            .Produces<CallDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        calls.MapGet("/", GetHistoryAsync)
            .WithName("GetCallHistory")
            .Produces<PagedList<CallSummaryDto>>();

        calls.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetCall")
            .Produces<CallDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        calls.MapPost("/{id:guid}/token", GetTokenAsync)
            .WithName("GetCallToken")
            .Produces<CallTokenDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        calls.MapPost("/{id:guid}/answer", AnswerAsync)
            .WithName("AnswerCall")
            .Produces<CallDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        calls.MapPost("/{id:guid}/decline", DeclineAsync)
            .WithName("DeclineCall")
            .Produces<CallDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        calls.MapPost("/{id:guid}/end", EndAsync)
            .WithName("EndCall")
            .Produces<CallDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> StartAsync(
        StartCallRequest request,
        IValidator<StartCallRequest> validator,
        ICallService callService,
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

        var result = await callService.StartAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, call => Results.Created($"/api/calls/{call.Id}", call));
    }

    private static async Task<IResult> GetHistoryAsync(
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = 20)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.GetHistoryAsync(userId.Value, cursor, limit, cancellationToken);
        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.GetByIdAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, call => Results.Ok(call));
    }

    private static async Task<IResult> GetTokenAsync(
        Guid id,
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.GetTokenAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, token => Results.Ok(token));
    }

    private static async Task<IResult> AnswerAsync(
        Guid id,
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.AnswerAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, call => Results.Ok(call));
    }

    private static async Task<IResult> DeclineAsync(
        Guid id,
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.DeclineAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, call => Results.Ok(call));
    }

    private static async Task<IResult> EndAsync(
        Guid id,
        ICallService callService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await callService.EndAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, call => Results.Ok(call));
    }
}
