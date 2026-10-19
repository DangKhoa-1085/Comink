// Trạng thái cuộc gọi (docs/03_database_design.md mục 4, docs/tasks/phase4_calls.md).
namespace Comink.Domain.Enums;

/// <summary>Vòng đời cuộc gọi.</summary>
public enum CallStatus
{
    /// <summary>Đang đổ chuông, chưa ai tham gia.</summary>
    Ringing = 0,

    /// <summary>Đã có ít nhất một người vào room.</summary>
    InProgress = 1,

    /// <summary>Kết thúc bình thường (đã có người tham gia).</summary>
    Completed = 2,

    /// <summary>Tất cả người được mời từ chối.</summary>
    Declined = 3,

    /// <summary>Người gọi huỷ trước khi ai tham gia.</summary>
    Cancelled = 4,

    /// <summary>Không ai trả lời trong thời gian đổ chuông.</summary>
    Missed = 5,

    /// <summary>Lỗi (nhà cung cấp gọi không khả dụng…).</summary>
    Failed = 6
}
