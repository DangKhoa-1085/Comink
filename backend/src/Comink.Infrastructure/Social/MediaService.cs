// Nghiệp vụ media: cấp thông tin upload, nhận tệp (dev) và đăng ký tài sản.
using Comink.Application.Common;
using Comink.Application.Common.Interfaces;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Cấp thông tin upload, nhận tệp khi dev và đăng ký tài sản media vào DB.</summary>
public class MediaService : IMediaService
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaStorage _storage;

    public MediaService(ApplicationDbContext db, IMediaStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <inheritdoc />
    public async Task<OperationResult<MediaSignatureDto>> GetSignatureAsync(
        Guid currentUserId,
        MediaSignatureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!MediaLimits.AllowedContexts.Contains(request.Context))
        {
            return OperationResult<MediaSignatureDto>.Failure(ErrorCodes.ValidationFailed);
        }

        var signature = await _storage.GenerateUploadSignatureAsync(request.Context, null, cancellationToken);

        return OperationResult<MediaSignatureDto>.Success(new MediaSignatureDto(
            signature.Provider,
            signature.UploadUrl,
            signature.Signature,
            signature.ApiKey,
            signature.CloudName,
            signature.Timestamp,
            signature.UploadPreset,
            signature.Folder));
    }

    /// <inheritdoc />
    public async Task<OperationResult<LocalUploadResultDto>> SaveLocalAsync(
        Guid currentUserId,
        string context,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!MediaLimits.AllowedContexts.Contains(context))
        {
            return OperationResult<LocalUploadResultDto>.Failure(ErrorCodes.ValidationFailed);
        }

        if (!MediaLimits.IsAllowedImage(contentType))
        {
            return OperationResult<LocalUploadResultDto>.Failure(ErrorCodes.MediaUnsupportedType);
        }

        if (sizeBytes <= 0 || sizeBytes > MediaLimits.MaxImageBytes)
        {
            return OperationResult<LocalUploadResultDto>.Failure(ErrorCodes.MediaTooLarge);
        }

        StoredMedia stored;
        try
        {
            stored = await _storage.SaveAsync(context, fileName, contentType, sizeBytes, content, cancellationToken);
        }
        catch (NotSupportedException)
        {
            // Đang dùng Cloudinary: client phải tải trực tiếp, backend không nhận tệp.
            return OperationResult<LocalUploadResultDto>.Failure(ErrorCodes.ValidationFailed);
        }

        return OperationResult<LocalUploadResultDto>.Success(new LocalUploadResultDto(
            stored.Provider,
            stored.ProviderPublicId,
            stored.Url,
            stored.MimeType,
            stored.SizeBytes));
    }

    /// <inheritdoc />
    public async Task<OperationResult<MediaAssetDto>> RegisterAsync(
        Guid currentUserId,
        RegisterMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!MediaLimits.AllowedContexts.Contains(request.Context))
        {
            return OperationResult<MediaAssetDto>.Failure(ErrorCodes.ValidationFailed);
        }

        if (!MediaLimits.IsAllowedImage(request.MimeType))
        {
            return OperationResult<MediaAssetDto>.Failure(ErrorCodes.MediaUnsupportedType);
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > MediaLimits.MaxImageBytes)
        {
            return OperationResult<MediaAssetDto>.Failure(ErrorCodes.MediaTooLarge);
        }

        if (string.IsNullOrWhiteSpace(request.ProviderPublicId) || string.IsNullOrWhiteSpace(request.Url))
        {
            return OperationResult<MediaAssetDto>.Failure(ErrorCodes.ValidationFailed);
        }

        var provider = ParseProvider(_storage.Provider);

        // Đăng ký lại cùng một tệp → trả về bản đã có (idempotent).
        var existing = await _db.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.ProviderPublicId == request.ProviderPublicId && m.OwnerId == currentUserId,
                cancellationToken);

        if (existing is not null)
        {
            return OperationResult<MediaAssetDto>.Success(
                new MediaAssetDto(existing.Id, existing.Url, existing.ThumbnailUrl, existing.Width, existing.Height));
        }

        var asset = new MediaAsset
        {
            OwnerId = currentUserId,
            Provider = provider,
            ProviderPublicId = request.ProviderPublicId,
            Url = request.Url,
            ThumbnailUrl = request.ThumbnailUrl,
            ResourceType = MediaResourceType.Image,
            MimeType = request.MimeType,
            SizeBytes = request.SizeBytes,
            Width = request.Width,
            Height = request.Height,
            Context = ParseContext(request.Context)
        };

        _db.MediaAssets.Add(asset);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OperationResult<MediaAssetDto>.Failure(ErrorCodes.ValidationFailed);
        }

        return OperationResult<MediaAssetDto>.Success(
            new MediaAssetDto(asset.Id, asset.Url, asset.ThumbnailUrl, asset.Width, asset.Height));
    }

    private static MediaProvider ParseProvider(string value) =>
        Enum.TryParse<MediaProvider>(value, ignoreCase: true, out var provider) ? provider : MediaProvider.Cloudinary;

    private static MediaContext ParseContext(string value) =>
        Enum.TryParse<MediaContext>(value, ignoreCase: true, out var context) ? context : MediaContext.Post;
}
