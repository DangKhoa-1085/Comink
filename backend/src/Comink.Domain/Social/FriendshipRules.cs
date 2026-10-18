// Quy tắc nghiệp vụ cho bạn bè và theo dõi.
using Comink.Domain.Enums;

namespace Comink.Domain.Social;

/// <summary>
/// Quy tắc của quan hệ bạn bè hai chiều và theo dõi một chiều
/// (docs/03_database_design.md mục 3.2; hợp đồng API mục 3.2/3.3).
/// </summary>
public static class FriendshipRules
{
    /// <summary>Không ai được kết bạn hoặc theo dõi chính mình.</summary>
    public static bool IsSelfPair(Guid first, Guid second) => first == second;

    /// <summary>Chỉ lời mời đang chờ mới có thể chấp nhận.</summary>
    public static bool CanAccept(FriendshipStatus status) => status == FriendshipStatus.Pending;

    /// <summary>Đã là bạn bè rồi thì không gửi lời mời mới (trả lỗi "đã là bạn").</summary>
    public static bool CanSendRequest(FriendshipStatus? existingStatus) =>
        existingStatus != FriendshipStatus.Accepted;

    /// <summary>Cột đếm không bao giờ được âm.</summary>
    public static int Decrement(int current) => current > 0 ? current - 1 : 0;

    /// <summary>Tăng cột đếm an toàn (tránh tràn số khi dữ liệu bất thường).</summary>
    public static int Increment(int current) => current < int.MaxValue ? current + 1 : current;
}
