// Endpoint thiết bị nhận push và hộp thư thông báo — /api/devices, /api/notifications.
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Notifications.Abstractions;
using Comink.Application.Notifications.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("/api/devices")
            .WithTags("Notifications")
            .RequireAuthorization("Authenticated");

        devices.MapPost("/", RegisterDeviceAsync)
            .WithName("RegisterDevice")
            .Produces<DeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        devices.MapDelete("/", UnregisterDeviceAsync)
            .WithName("UnregisterDevice")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        var notifications = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization("Authenticated");

        notifications.MapGet("/", GetNotificationsAsync)
            .WithName("GetNotifications")
            .Produces<PagedList<NotificationDto>>();

        notifications.MapPost("/{id:guid}/read", MarkReadAsync)
            .WithName("MarkNotificationRead")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterDeviceAsync(
        RegisterDeviceRequest request,
        IValidator<RegisterDeviceRequest> validator,
        INotificationService notificationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var (isValid, errorResult, _) =
            await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await notificationService.RegisterDeviceAsync(
            userId.Value,
            request,
            cancellationToken);
        return ApiResults.FromResult(result, value => Results.Ok(value));
    }

    private static async Task<IResult> UnregisterDeviceAsync(
        INotificationService notificationService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? token = null)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResults.Problem(ErrorCodes.ValidationFailed);
        }

        var result = await notificationService.UnregisterDeviceAsync(
            userId.Value,
            token,
            cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }

    private static async Task<IResult> GetNotificationsAsync(
        INotificationService notificationService,
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

        var result = await notificationService.GetListAsync(
            userId.Value,
            cursor,
            limit,
            cancellationToken);
        return ApiResults.FromResult(result, page => Results.Ok(page));
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        INotificationService notificationService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await notificationService.MarkReadAsync(
            userId.Value,
            id,
            cancellationToken);
        return ApiResults.FromResult(result, _ => Results.NoContent());
    }
}
