// Entity ánh xạ bảng `media_assets` (docs/03_database_design.md mục 3.5).
using Comink.Domain.Enums;
using Comink.Infrastructure.Identity;

namespace Comink.Infrastructure.Persistence.Entities;

/// <summary>
/// Tài sản media đã tải lên dịch vụ cloud. Client tải trực tiếp lên Cloudinary
/// bằng chữ ký do backend cấp, sau đó gọi API để đăng ký thông tin vào bảng này.
/// </summary>
public class MediaAsset
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;

    public MediaProvider Provider { get; set; } = MediaProvider.Cloudinary;

    /// <summary>Mã tệp ở dịch vụ cloud; duy nhất theo từng provider.</summary>
    public string ProviderPublicId { get; set; } = string.Empty;

    /// <summary>URL phân phối.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>URL ảnh xem trước (với video).</summary>
    public string? ThumbnailUrl { get; set; }

    public MediaResourceType ResourceType { get; set; } = MediaResourceType.Image;

    public string MimeType { get; set; } = string.Empty;

    public string? OriginalFilename { get; set; }

    public long SizeBytes { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Thời lượng (video/âm thanh), tính bằng giây.</summary>
    public decimal? DurationSeconds { get; set; }

    /// <summary>Tệp được tải lên để dùng ở đâu.</summary>
    public MediaContext Context { get; set; } = MediaContext.Post;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Xóa mềm; job nền xóa tệp thật trên cloud rồi mới xóa dòng.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
