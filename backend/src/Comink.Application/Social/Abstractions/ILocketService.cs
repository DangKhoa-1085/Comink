// Hợp đồng nghiệp vụ của tính năng Comink Locket.
using Comink.Application.Common;
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Tạo, xem, thả cảm xúc, trả lời vào chat và xóa khoảnh khắc Locket (hiệu lực 24 giờ).</summary>
public interface ILocketService
{
    /// <summary>Đăng một khoảnh khắc Locket mới từ ảnh đã tải lên với ngữ cảnh Locket.</summary>
    Task<OperationResult<LocketDto>> CreateAsync(
        Guid currentUserId,
        CreateLocketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách Locket còn hiệu lực 24h theo con trỏ, có thể lọc theo tác giả.</summary>
    Task<OperationResult<PagedList<LocketDto>>> GetFeedAsync(
        Guid currentUserId,
        Guid? authorId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Thả, đổi hoặc gỡ cảm xúc trên một Locket còn hiệu lực.</summary>
    Task<OperationResult<LocketDto>> ReactAsync(
        Guid currentUserId,
        Guid momentId,
        ReactLocketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Trả lời nhanh một Locket bằng tin nhắn vào đoạn chat 1–1 với tác giả.</summary>
    Task<OperationResult<LocketReplyResultDto>> ReplyAsync(
        Guid currentUserId,
        Guid momentId,
        ReplyLocketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chủ sở hữu xóa mềm Locket sớm trước hạn 24 giờ.</summary>
    Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid momentId,
        CancellationToken cancellationToken = default);

    /// <summary>Dọn các Locket đã hết hạn 24 giờ (chạy nền định kỳ).</summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
