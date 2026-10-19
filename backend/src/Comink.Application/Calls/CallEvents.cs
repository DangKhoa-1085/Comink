// Tên sự kiện SignalR của module Cuộc gọi — hằng số dùng chung backend/web.
namespace Comink.Application.Calls;

/// <summary>Tên sự kiện hub liên quan tới cuộc gọi.</summary>
public static class CallEvents
{
    /// <summary>Có cuộc gọi đến (gửi tới `user:{id}` của người được mời).</summary>
    public const string CallIncoming = "CallIncoming";

    /// <summary>Trạng thái cuộc gọi thay đổi (đã trả lời, có người vào/rời).</summary>
    public const string CallUpdated = "CallUpdated";

    /// <summary>Cuộc gọi kết thúc.</summary>
    public const string CallEnded = "CallEnded";
}
