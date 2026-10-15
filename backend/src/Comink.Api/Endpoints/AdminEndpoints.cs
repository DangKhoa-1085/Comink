// Endpoint quản trị — /api/admin (docs/05_api_contract.md mục 4).
// Giai đoạn 1 chỉ có danh sách tài khoản; khóa/mở khóa thuộc Giai đoạn 5.
using Comink.Api.Common;
using Comink.Application.Identity.Abstractions;

namespace Comink.Api.Endpoints;

/// <summary>Một dòng trong danh sách tài khoản của trang quản trị.</summary>
public record AdminUserSummary(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string AccountStatus,
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
            .Produces<PagedResult<AdminUserSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

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

        var items = page
            .Select(u => new AdminUserSummary(
                u.Id,
                u.UserName,
                u.Email,
                u.DisplayName,
                u.AccountStatus,
                u.CreatedAt))
            .ToList();

        var nextCursor = hasMore && page.Count > 0
            ? CursorCodec.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;

        return Results.Ok(new PagedResult<AdminUserSummary>(items, nextCursor, hasMore));
    }
}
