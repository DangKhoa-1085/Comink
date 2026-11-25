// Cài đặt IMediaStorage cho Cloudinary (client tải trực tiếp bằng chữ ký).
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Comink.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Comink.Infrastructure.Media;

/// <summary>
/// Ký tham số upload Cloudinary (HMAC-SHA1) để client tải trực tiếp lên Cloudinary.
/// Tài liệu: https://cloudinary.com/documentation/upload_images#generating_authentication_signatures
/// </summary>
public class CloudinaryMediaStorage : IMediaStorage
{
    private readonly MediaStorageOptions _options;

    public CloudinaryMediaStorage(IOptions<MediaStorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Provider => "Cloudinary";

    /// <inheritdoc />
    public Task<MediaUploadSignature> GenerateUploadSignatureAsync(
        string context,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.UseCloudinary)
        {
            throw new InvalidOperationException(
                "Chưa cấu hình đủ MediaStorage:CloudName/ApiKey/ApiSecret cho Cloudinary.");
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var targetFolder = string.IsNullOrWhiteSpace(folder) ? $"comink/{context.ToLowerInvariant()}" : folder;

        // Chỉ ký các tham số được gửi kèm (không gồm file, api_key, cloud_name).
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = targetFolder,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(_options.UploadPreset))
        {
            parameters["upload_preset"] = _options.UploadPreset;
        }

        var toSign = string.Join("&", parameters.Select(pair => $"{pair.Key}={pair.Value}"));
        var signature = Convert.ToHexString(
                SHA1.HashData(Encoding.UTF8.GetBytes(toSign + _options.ApiSecret)))
            .ToLowerInvariant();

        var signatureResult = new MediaUploadSignature(
            Provider: "Cloudinary",
            UploadUrl: $"https://api.cloudinary.com/v1_1/{_options.CloudName}/auto/upload",
            Signature: signature,
            ApiKey: _options.ApiKey!,
            CloudName: _options.CloudName!,
            Timestamp: timestamp,
            UploadPreset: _options.UploadPreset ?? string.Empty,
            Folder: targetFolder);

        return Task.FromResult(signatureResult);
    }

    /// <inheritdoc />
    public Task<StoredMedia> SaveAsync(
        string context,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Cloudinary nhận tệp trực tiếp từ client; backend không nhận tệp khi đang dùng Cloudinary.");

    /// <inheritdoc />
    public async Task DeleteAsync(
        string provider,
        string publicId,
        CancellationToken cancellationToken = default)
    {
        // Xóa tệp Cloudinary cần gọi Admin API; chưa dùng ở GĐ 2 (chỉ xóa khi dọn media).
        await Task.CompletedTask;
    }
}
