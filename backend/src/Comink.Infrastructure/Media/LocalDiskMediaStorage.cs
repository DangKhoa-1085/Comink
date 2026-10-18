// Cài đặt IMediaStorage lưu xuống đĩa cục bộ — chỉ dùng khi dev chưa có khoá Cloudinary.
using Comink.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Comink.Infrastructure.Media;

/// <summary>
/// Lưu tệp vào thư mục trên máy (mặc định <c>infra/uploads</c>) và phục vụ qua static file.
/// **Không dùng cho production** — chỉ là phương án dự phòng khi máy dev thiếu khoá Cloudinary.
/// </summary>
public class LocalDiskMediaStorage : IMediaStorage
{
    private readonly MediaStorageOptions _options;
    private readonly string _rootPath;

    public LocalDiskMediaStorage(IOptions<MediaStorageOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;

        // Đường dẫn tương đối được tính từ thư mục gốc repository khi chạy dev
        // (Program.cs đã dò ngược để tìm .env ở gốc, nên dùng content root làm mốc).
        _rootPath = Path.IsPathRooted(_options.LocalRootPath)
            ? _options.LocalRootPath
            : Path.Combine(environment.ContentRootPath, "..", "..", "..", _options.LocalRootPath);

        _rootPath = Path.GetFullPath(_rootPath);
    }

    /// <summary>Thư mục gốc thực tế — Program.cs dùng để phục vụ static file.</summary>
    public string RootPath => _rootPath;

    /// <inheritdoc />
    public string Provider => "Local";

    /// <inheritdoc />
    public Task<MediaUploadSignature> GenerateUploadSignatureAsync(
        string context,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        // Không cần chữ ký: client gửi tệp lên backend kèm access token.
        var signature = new MediaUploadSignature(
            Provider: "Local",
            UploadUrl: $"/api/media/local-upload?context={Uri.EscapeDataString(context)}",
            Signature: string.Empty,
            ApiKey: string.Empty,
            CloudName: string.Empty,
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UploadPreset: string.Empty,
            Folder: context.ToLowerInvariant());

        return Task.FromResult(signature);
    }

    /// <inheritdoc />
    public async Task<StoredMedia> SaveAsync(
        string context,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
        }

        var publicId = $"{Guid.CreateVersion7():N}";
        var folder = Path.Combine(_rootPath, context.ToLowerInvariant());
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, publicId + extension.ToLowerInvariant());
        await using (var target = File.Create(fullPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/{context.ToLowerInvariant()}/{publicId}{extension.ToLowerInvariant()}";

        return new StoredMedia(
            Provider: "Local",
            ProviderPublicId: publicId,
            Url: url,
            ThumbnailUrl: url,
            MimeType: contentType,
            SizeBytes: sizeBytes);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string provider, string publicId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        foreach (var file in Directory.EnumerateFiles(_rootPath, publicId + ".*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }
}
