// Hợp đồng nghiệp vụ media (docs/05_api_contract.md mục 7.6).
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Cấp thông tin upload, nhận tệp (khi dev) và đăng ký tài sản media.</summary>
public interface IMediaService
{
    /// <summary>Lấy thông tin để client tải tệp lên nơi lưu trữ.</summary>
    Task<OperationResult<MediaSignatureDto>> GetSignatureAsync(
        Guid currentUserId,
        MediaSignatureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhận tệp do client gửi lên backend. Chỉ dùng khi cấu hình lưu cục bộ (dev);
    /// nếu đang dùng Cloudinary thì trả lỗi vì client phải upload trực tiếp.
    /// </summary>
    Task<OperationResult<LocalUploadResultDto>> SaveLocalAsync(
        Guid currentUserId,
        string context,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Đăng ký tài sản media đã tải lên vào DB.</summary>
    Task<OperationResult<MediaAssetDto>> RegisterAsync(
        Guid currentUserId,
        RegisterMediaRequest request,
        CancellationToken cancellationToken = default);
}
