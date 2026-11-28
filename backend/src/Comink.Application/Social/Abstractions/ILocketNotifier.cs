// Cổng phát sự kiện SignalR cho tính năng Comink Locket (cài đặt tại Comink.Api).
using Comink.Application.Social.Dtos;

namespace Comink.Application.Social.Abstractions;

/// <summary>Phát thông báo thời gian thực khi có Locket mới, thay đổi cảm xúc hoặc bị xóa.</summary>
public interface ILocketNotifier
{
    /// <summary>Phát sự kiện có khoảnh khắc Locket mới tới tác giả và bạn bè được phép xem.</summary>
    Task LocketCreatedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        LocketDto moment,
        CancellationToken cancellationToken = default);

    /// <summary>Phát sự kiện Locket vừa cập nhật cảm xúc.</summary>
    Task LocketUpdatedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        Guid momentId,
        CancellationToken cancellationToken = default);

    /// <summary>Phát sự kiện Locket đã bị xóa sớm.</summary>
    Task LocketDeletedAsync(
        IReadOnlyList<Guid> viewerUserIds,
        Guid momentId,
        CancellationToken cancellationToken = default);
}
