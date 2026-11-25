// Endpoint media — /api/media (docs/05_api_contract.md mục 7.6).
using Comink.Api.Common;
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;

namespace Comink.Api.Endpoints;

/// <summary>Cấp thông tin upload, nhận tệp (dev) và đăng ký tài sản media.</summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media")
            .WithTags("Media")
            .RequireAuthorization("Authenticated");

        group.MapPost("/signature", GetSignatureAsync)
            .WithName("GetMediaSignature")
            .Produces<MediaSignatureDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // Chỉ có tác dụng khi cấu hình lưu cục bộ (dev).
        group.MapPost("/local-upload", LocalUploadAsync)
            .WithName("UploadMediaLocal")
            .DisableAntiforgery()
            .Produces<LocalUploadResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", RegisterAsync)
            .WithName("RegisterMedia")
            .Produces<MediaAssetDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSignatureAsync(
        MediaSignatureRequest request,
        IMediaService mediaService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await mediaService.GetSignatureAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, value => Results.Ok(value));
    }

    private static async Task<IResult> LocalUploadAsync(
        IFormFile? file,
        IMediaService mediaService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string context = "Post")
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        if (file is null || file.Length == 0)
        {
            return ApiResults.Problem(ErrorCodes.ValidationFailed);
        }

        await using var stream = file.OpenReadStream();
        var result = await mediaService.SaveLocalAsync(
            userId.Value,
            context,
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            cancellationToken);

        return ApiResults.FromResult(result, value => Results.Ok(value));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterMediaRequest request,
        IMediaService mediaService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.GetUserId(httpContext.User);
        if (userId is null)
        {
            return ApiResults.Problem(ErrorCodes.Unauthorized);
        }

        var result = await mediaService.RegisterAsync(userId.Value, request, cancellationToken);
        return ApiResults.FromResult(result, value => Results.Created($"/api/media/{value.Id}", value));
    }
}
