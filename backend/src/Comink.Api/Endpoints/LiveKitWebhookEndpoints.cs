// Webhook LiveKit — /api/webhooks/livekit (docs/05_api_contract.md mục 9.5).
using System.Text.Json;
using Comink.Api.Common;
using Comink.Application.Calls.Abstractions;
using Comink.Infrastructure.Calls;
using Microsoft.Extensions.Options;

namespace Comink.Api.Endpoints;

/// <summary>Nhận sự kiện room/participant từ LiveKit để đóng cuộc gọi và chống room treo.</summary>
public static class LiveKitWebhookEndpoints
{
    public static IEndpointRouteBuilder MapLiveKitWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/livekit", HandleAsync)
            .WithTags("Calls")
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest httpRequest,
        ICallService callService,
        IOptions<LiveKitOptions> liveKitOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("LiveKitWebhook");

        using var reader = new StreamReader(httpRequest.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var authorization = httpRequest.Headers.Authorization.ToString().Trim();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            logger.LogWarning("Webhook LiveKit thiếu header Authorization");
            return Results.Json(new { error = "missing_authorization" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // LiveKit gửi JWT trần trong header `Authorization` (không kèm `Bearer `), nhưng vẫn
        // chấp nhận cả hai dạng để không phụ thuộc phiên bản LiveKit.
        const string bearerPrefix = "Bearer ";
        var token = authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..].Trim()
            : authorization;

        if (!LiveKitWebhookVerifier.IsValid(token, body, liveKitOptions.Value.ApiSecret))
        {
            // Chữ ký sai → không xử lý; trả mã chẩn đoán (không lộ token/secret) để dễ điều tra.
            logger.LogWarning("Webhook LiveKit có chữ ký không hợp lệ (body {Length} ký tự)", body.Length);
            return Results.Json(new { error = "invalid_signature" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        CallProviderEvent? providerEvent;
        try
        {
            providerEvent = Parse(body);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Webhook LiveKit có body không phải JSON hợp lệ");
            return Results.BadRequest();
        }

        if (providerEvent is null)
        {
            // Sự kiện không quan tâm → trả 200 để LiveKit không gửi lại.
            return Results.Ok();
        }

        await callService.HandleProviderEventAsync(providerEvent, cancellationToken);
        return Results.Ok();
    }

    /// <summary>Chuyển payload LiveKit thành sự kiện đã chuẩn hoá; null nếu không quan tâm.</summary>
    private static CallProviderEvent? Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("event", out var eventProperty))
        {
            return null;
        }

        var eventName = eventProperty.GetString();
        var type = eventName switch
        {
            "room_started" => CallProviderEventType.RoomStarted,
            "room_finished" => CallProviderEventType.RoomFinished,
            "participant_joined" => CallProviderEventType.ParticipantJoined,
            "participant_left" => CallProviderEventType.ParticipantLeft,
            _ => (CallProviderEventType?)null
        };

        if (type is null)
        {
            return null;
        }

        var roomName = root.TryGetProperty("room", out var room) &&
                       room.TryGetProperty("name", out var name)
            ? name.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(roomName))
        {
            return null;
        }

        var identity = root.TryGetProperty("participant", out var participant) &&
                       participant.TryGetProperty("identity", out var identityProperty)
            ? identityProperty.GetString()
            : null;

        return new CallProviderEvent(type.Value, roomName, identity);
    }
}
