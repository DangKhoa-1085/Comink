// Endpoint nhóm Xác thực — /api/auth (docs/05_api_contract.md mục 2).
using Comink.Api.Common;
using Comink.Application.Identity;
using Comink.Application.Identity.Dtos;
using FluentValidation;

namespace Comink.Api.Endpoints;

/// <summary>Đăng ký nhóm endpoint xác thực.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("Register")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshToken")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization("Authenticated")
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IValidator<RegisterRequest> validator,
        AuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await authService.RegisterAsync(request, BuildContext(null, httpContext), cancellationToken);
        return ApiResults.FromResult(result, value => Results.Created("/api/users/me", value));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        AuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await authService.LoginAsync(request, BuildContext(request.DeviceName, httpContext), cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IValidator<RefreshTokenRequest> validator,
        AuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        var result = await authService.RefreshAsync(request, BuildContext(null, httpContext), cancellationToken);
        return ApiResults.FromResult(result, Results.Ok);
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequest request,
        IValidator<RefreshTokenRequest> validator,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var (isValid, errorResult, _) = await ApiResults.ValidateAsync(validator, request, cancellationToken);
        if (!isValid)
        {
            return errorResult!;
        }

        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static RefreshTokenContext BuildContext(string? deviceName, HttpContext httpContext) =>
        new(deviceName, CurrentUser.GetIpAddress(httpContext), CurrentUser.GetUserAgent(httpContext));
}
