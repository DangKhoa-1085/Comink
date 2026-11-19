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

        conversations.MapPost("/group", CreateGroupAsync)
            .WithName("CreateGroupConversation")
            .Produces<ConversationDto>(StatusCodes.Status201Created)
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

        // NC Giai đoạn 7: mốc đã đọc (chỉ tiến), phát MessagesRead tới các thành viên.
        conversations.MapPost("/{id:guid}/read", MarkReadAsync)
            .WithName("MarkConversationRead")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // NC Ưu tiên 2: chỉnh sửa / thu hồi tin (chỉ người gửi, trong 24 giờ).
        conversations.MapPatch("/{id:guid}/messages/{messageId:guid}", EditMessageAsync)
            .WithName("EditMessage")
            .Produces<MessageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapDelete("/{id:guid}/messages/{messageId:guid}", RecallMessageAsync)
            .WithName("RecallMessage")
            .Produces<MessageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // NC Ưu tiên 2: cảm xúc trên tin. Khoá cảm xúc nằm trong ReactionCatalog (domain).
        conversations.MapPost("/{id:guid}/messages/{messageId:guid}/reactions", AddReactionAsync)
            .WithName("AddMessageReaction")
            .Produces<MessageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapDelete("/{id:guid}/messages/{messageId:guid}/reactions/{reaction}", RemoveReactionAsync)
            .WithName("RemoveMessageReaction")
            .Produces<MessageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // NC Ưu tiên 2 — Đợt B: chuyển tiếp, tìm kiếm, ngữ cảnh, ghim, tắt thông báo.
        conversations.MapPost("/{id:guid}/messages/forward", ForwardMessageAsync)
            .WithName("ForwardMessage")
            .Produces<MessageDto>(StatusCodes.Status201Created)
            .Produces<MessageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapGet("/{id:guid}/messages/search", SearchConversationMessagesAsync)
            .WithName("SearchConversationMessages")
            .Produces<PagedList<MessageSearchResultDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapGet("/{id:guid}/messages/{messageId:guid}/context", GetMessageContextAsync)
            .WithName("GetMessageContext")
            .Produces<MessageContextDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapGet("/{id:guid}/pinned", GetPinnedMessagesAsync)
            .WithName("GetPinnedMessages")
            .Produces<PinnedMessagesDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapPut("/{id:guid}/pinned/{messageId:guid}", PinMessageAsync)
            .WithName("PinMessage")
            .Produces<PinnedMessagesDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapDelete("/{id:guid}/pinned/{messageId:guid}", UnpinMessageAsync)
            .WithName("UnpinMessage")
            .Produces<PinnedMessagesDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        conversations.MapPut("/{id:guid}/mute", MuteConversationAsync)
            .WithName("MuteConversation")
            .Produces<MuteStateDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Tìm trong MỌI hội thoại người dùng còn là thành viên (yêu cầu roadmap GĐ 7).
        app.MapGet("/api/messages/search", SearchAllMessagesAsync)
            .WithTags("Messaging")
            .RequireAuthorization("Authenticated")
            .WithName("SearchAllMessages")
            .Produces<PagedList<MessageSearchResultDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

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

    private static async Task<IResult> CreateGroupAsync(
        CreateGroupConversationRequest request,
        IValidator<CreateGroupConversationRequest> validator,
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

        var result = await conversationService.CreateGroupAsync(userId.Value, request, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Results.Created($"/api/conversations/{result.Value.Id}", result.Value)
            : ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
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

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        MarkReadRequest request,
        IValidator<MarkReadRequest> validator,
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

        var result = await conversationService.MarkReadAsync(
            userId.Value,
            id,
            request.MessageId,
            cancellationToken);

        // 204 cho cả trường hợp "đã ở mốc này hoặc mới hơn" (idempotent, chỉ tiến).
        return result.Succeeded
            ? Results.NoContent()
            : ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
    }

    private static async Task<IResult> EditMessageAsync(
        Guid id,
        Guid messageId,
        EditMessageRequest request,
        IValidator<EditMessageRequest> validator,
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

        var result = await messageService.EditAsync(
            userId.Value, id, messageId, request, cancellationToken);

        return ApiResults.FromResult(result, message => Results.Ok(message));
    }

    private static async Task<IResult> RecallMessageAsync(
        Guid id,
        Guid messageId,
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.RecallAsync(userId.Value, id, messageId, cancellationToken);
        return ApiResults.FromResult(result, message => Results.Ok(message));
    }

    private static async Task<IResult> AddReactionAsync(
        Guid id,
        Guid messageId,
        ReactionRequest request,
        IValidator<ReactionRequest> validator,
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

        var result = await messageService.AddReactionAsync(
            userId.Value, id, messageId, request.Reaction, cancellationToken);

        return ApiResults.FromResult(result, message => Results.Ok(message));
    }

    private static async Task<IResult> RemoveReactionAsync(
        Guid id,
        Guid messageId,
        string reaction,
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.RemoveReactionAsync(
            userId.Value, id, messageId, reaction, cancellationToken);

        return ApiResults.FromResult(result, message => Results.Ok(message));
    }

    private static async Task<IResult> ForwardMessageAsync(
        Guid id,
        ForwardMessageRequest request,
        IValidator<ForwardMessageRequest> validator,
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

        var result = await messageService.ForwardAsync(userId.Value, id, request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return ApiResults.Problem(result.ErrorCode ?? ErrorCodes.InternalError);
        }

        return result.Value.WasCreated
            ? Results.Created(
                $"/api/conversations/{id}/messages/{result.Value.Message.Id}",
                result.Value.Message)
            : Results.Ok(result.Value.Message);
    }

    private static async Task<IResult> SearchConversationMessagesAsync(
        Guid id,
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? q = null,
        string? cursor = null,
        int limit = 20)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.SearchAsync(
            userId.Value, id, q ?? string.Empty, cursor, limit, cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> SearchAllMessagesAsync(
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? q = null,
        string? cursor = null,
        int limit = 20)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.SearchAsync(
            userId.Value, null, q ?? string.Empty, cursor, limit, cancellationToken);

        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> GetMessageContextAsync(
        Guid id,
        Guid messageId,
        IMessageService messageService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int limit = 31)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await messageService.GetContextAsync(
            userId.Value, id, messageId, limit, cancellationToken);

        return ApiResults.FromResult(result, context => Results.Ok(context));
    }

    private static async Task<IResult> GetPinnedMessagesAsync(
        Guid id,
        IPinnedMessageService pinnedMessageService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await pinnedMessageService.GetAsync(userId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, pinned => Results.Ok(pinned));
    }

    private static async Task<IResult> PinMessageAsync(
        Guid id,
        Guid messageId,
        IPinnedMessageService pinnedMessageService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await pinnedMessageService.PinAsync(userId.Value, id, messageId, cancellationToken);
        return ApiResults.FromResult(result, pinned => Results.Ok(pinned));
    }

    private static async Task<IResult> UnpinMessageAsync(
        Guid id,
        Guid messageId,
        IPinnedMessageService pinnedMessageService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await pinnedMessageService.UnpinAsync(userId.Value, id, messageId, cancellationToken);
        return ApiResults.FromResult(result, pinned => Results.Ok(pinned));
    }

    private static async Task<IResult> MuteConversationAsync(
        Guid id,
        MuteConversationRequest request,
        IValidator<MuteConversationRequest> validator,
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

        var result = await conversationService.MuteAsync(userId.Value, id, request, cancellationToken);
        return ApiResults.FromResult(result, state => Results.Ok(state));
    }
}
