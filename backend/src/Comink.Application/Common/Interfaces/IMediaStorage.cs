// Interface kho lưu trữ tệp đa phương tiện (Cloudinary, Firebase, v.v.)
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng lưu trữ media. Cài đặt cụ thể ở Infrastructure.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// Tạo chữ ký để client upload trực tiếp lên cloud provider.
    /// </summary>
    Task<MediaUploadSignature> GenerateUploadSignatureAsync(
        string context,
        string? folder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa tệp khỏi cloud provider theo public ID.
    /// </summary>
    Task DeleteAsync(string provider, string publicId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thông tin chữ ký để client upload trực tiếp.
/// </summary>
public record MediaUploadSignature(
    string Signature,
    string ApiKey,
    string CloudName,
    long Timestamp,
    string UploadPreset,
    string Folder);
