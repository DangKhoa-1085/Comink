// Trạng thái từng người trong cuộc gọi (docs/03_database_design.md mục 3.4).
namespace Comink.Domain.Enums;

/// <summary>Trạng thái của một người được mời trong cuộc gọi.</summary>
public enum CallParticipantStatus
{
    /// <summary>Đã mời, đang đổ chuông.</summary>
    Invited = 0,

    /// <summary>Đã vào room.</summary>
    Joined = 1,

    /// <summary>Đã từ chối.</summary>
    Declined = 2,

    /// <summary>Không trả lời (hết thời gian đổ chuông).</summary>
    Missed = 3,

    /// <summary>Đã vào rồi rời đi.</summary>
    Left = 4
}
