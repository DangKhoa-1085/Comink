// Endpoint nhắn tin — /api/conversations (docs/05_api_contract.md mục 8.2–8.6).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Mở hội thoại 1–1, danh sách hội thoại, lịch sử và gửi tin nhắn.</summary>
public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations")
            .WithTags("Messaging")
            .RequireAuthorization("Authenticated");

        conversations.MapPost("/direct", OpenDirectAsync)
            .WithName("OpenDirectConversation")
            .Produces<ConversationDto>(StatusCodes.Status201Created)
            .Produces<ConversationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapGet("/", GetMyConversationsAsync)
            .WithName("GetMyConversations")
            .Produces<PagedList<ConversationSummaryDto>>();

        conversations.MapGet("/{id:guid}", GetConversationAsync)
            .WithName("GetConversation")
            .Produces<ConversationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapGet("/{id:guid}/messages", GetMessagesAsync)
            .WithName("GetMessages")
            .Produces<PagedList<MessageDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapPost("/{id:guid}/messages", SendMessageAsync)
            .WithName("SendMessage")
            .Produces<MessageDto>(StatusCodes.Status201Created)
            .Produces<MessageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> OpenDirectAsync(
        CreateDirectConversationRequest request,
        IValidator<CreateDirectConversationRequest> validator,
        IConversationService conversationService,
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

        var result = await conversationService.OpenDirectAsync(userId.Value, request.UserId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
        }

        // Tạo mới → 201; đã tồn tại → 200 (idempotent theo direct_key).
        return result.Value.WasCreated
            ? Results.Created($"/api/conversations/{result.Value.Conversation.Id}", result.Value.Conversation)
            : Results.Ok(result.Value.Conversation);
    }

    private static async Task<IResult> GetMyConversationsAsync(
        IConversationService conversationService,
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

        var result = await conversationService.GetMyConversationsAsync(
            userId.Value,
            cursor,
            limit,
            cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetConversationAsync(
        Guid id,
        IConversationService conversationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await conversationService.GetByIdAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, conversation => Results.Ok(conversation));
    }

    private static async Task<IResult> GetMessagesAsync(
        Guid id,
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? before = null,
        string? after = null,
        int limit = 30)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.GetHistoryAsync(
            userId.Value,
            id,
            before,
            after,
            limit,
            cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> SendMessageAsync(
        Guid id,
        SendMessageRequest request,
        IValidator<SendMessageRequest> validator,
        IMessageService messageService,
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

        var result = await messageService.SendAsync(userId.Value, id, request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
        }

        // Trùng client_message_id → 200 kèm tin đã lưu (idempotent); tin mới → 201.
        return result.Value.WasCreated
            ? Results.Created($"/api/conversations/{id}/messages/{result.Value.Message.Id}", result.Value.Message)
            : Results.Ok(result.Value.Message);
    }
}
