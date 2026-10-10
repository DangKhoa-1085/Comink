// Interface nhà cung cấp gọi thoại/video (LiveKit, Agora, Twilio, v.v.)
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng với nhà cung cấp gọi thoại/video.
/// Cài đặt cụ thể ở Infrastructure (LiveKit).
/// </summary>
public interface ICallProvider
{
    /// <summary>
    /// Tạo room mới cho cuộc gọi, trả về tên room ở phía nhà cung cấp.
    /// </summary>
    Task<string> CreateRoomAsync(string roomName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cấp access token ngắn hạn để user tham gia room.
    /// </summary>
    Task<string> GenerateAccessTokenAsync(
        string roomName,
        string userId,
        string displayName,
        bool canPublish = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kết thúc/xóa room ở nhà cung cấp.
    /// </summary>
    Task EndRoomAsync(string roomName, CancellationToken cancellationToken = default);
}
