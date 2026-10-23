// Endpoint quản trị — /api/admin (docs/05_api_contract.md mục 4).
// Giai đoạn 1: danh sách tài khoản. Giai đoạn 5: khóa/mở khóa tài khoản.
using System.Security.Claims;
using System.Text.Json;
using Comink.Api.Common;
using Comink.Application.Admin.Abstractions;
using Comink.Application.Admin.Dtos;
using Comink.Application.Common;
using Comink.Application.Identity.Abstractions;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Một dòng trong danh sách tài khoản của trang quản trị.</summary>
public record AdminUserSummary(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string AccountStatus,
    string? LockedReason,
    DateTimeOffset? LockedAt,
    DateTimeOffset CreatedAt);

/// <summary>Đăng ký nhóm endpoint quản trị (chỉ role Admin).</summary>
public static class AdminEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/users", ListUsersAsync)
            .WithName("ListUsers")
            .Produces<PagedList<AdminUserSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/users/{id:guid}/lock", LockUserAsync)
            .WithName("LockUser")
            .Produces<AdminUserSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/users/{id:guid}/unlock", UnlockUserAsync)
            .WithName("UnlockUser")
            .Produces<AdminUserSummary>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListUsersAsync(
        IIdentityService identityService,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null,
        string? search = null)
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        // Lấy dư một bản ghi để biết còn dữ liệu phía sau hay không.
        var decoded = CursorCodec.TryDecode(cursor);
        var users = await identityService.ListUsersAsync(
            search,
            decoded?.CreatedAt,
            decoded?.Id,
            pageSize + 1,
            cancellationToken);

        var hasMore = users.Count > pageSize;
        var page = hasMore ? users.Take(pageSize).ToList() : users.ToList();

        var items = page.Select(ToSummary).ToList();

        var nextCursor = hasMore && page.Count > 0
            ? CursorCodec.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;

        return Results.Ok(new PagedList<AdminUserSummary>(items, nextCursor, hasMore));
    }

    /// <summary>Khóa tài khoản. Body tùy chọn: <c>{ "reason": "..." }</c> (lý do ≤ 500 ký tự).</summary>
    private static async Task<IResult> LockUserAsync(
        Guid id,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IAdminService adminService,
        IValidator<LockUserRequest> validator,
        CancellationToken cancellationToken)
    {
        // Đọc body thủ công để body **tùy chọn** thật sự: minimal API trả 415 khi client không gửi
        // Content-Type, kể cả với EmptyBodyBehavior.Allow. Không có body ⇒ khóa không kèm lý do.
        LockUserRequest? request = null;
        if (httpRequest.ContentLength is > 0)
        {
            try
            {
                request = await httpRequest.ReadFromJsonAsync<LockUserRequest>(cancellationToken);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or BadHttpRequestException)
            {
                // Body không đọc được → 400 VALIDATION_FAILED (docs/05_api_contract.md mục 5).
                return ApiResults.Problem(ErrorCodes.ValidationFailed);
            }
        }

        var payload = request ?? new LockUserRequest(null);

        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, payload, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var adminId = CurrentUser.GetUserId(user);
        if (adminId is null)
        {
            // Không xảy ra khi policy AdminOnly đã chạy, nhưng vẫn phòng thủ (AGENTS.md mục 9.2).
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await adminService.LockUserAsync(adminId.Value, id, payload.Reason, cancellationToken);
        return ApiResults.FromResult(result, summary => Results.Ok(ToSummary(summary)));
    }

    /// <summary>Mở khóa tài khoản (idempotent khi tài khoản đang hoạt động).</summary>
    private static async Task<IResult> UnlockUserAsync(
        Guid id,
        ClaimsPrincipal user,
        IAdminService adminService,
        CancellationToken cancellationToken)
    {
        var adminId = CurrentUser.GetUserId(user);
        if (adminId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await adminService.UnlockUserAsync(adminId.Value, id, cancellationToken);
        return ApiResults.FromResult(result, summary => Results.Ok(ToSummary(summary)));
    }

    private static AdminUserSummary ToSummary(AdminUserData user) =>
        new(user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.AccountStatus,
            user.LockedReason,
            user.LockedAt,
            user.CreatedAt);
}
