// Quy tắc tắt thông báo theo từng hội thoại (NC Giai đoạn 7 — Ưu tiên 2, quyết định D7).
// Lưu ở `conversation_members.muted_until`; chỉ chặn push, không đổi số tin chưa đọc.
namespace Comink.Domain.Messaging;

/// <summary>Các lựa chọn thời hạn tắt thông báo do client gửi lên.</summary>
public static class MuteRules
{
    public const string Off = "Off";
    public const string OneHour = "OneHour";
    public const string EightHours = "EightHours";
    public const string OneWeek = "OneWeek";
    public const string Forever = "Forever";

    /// <summary>
    /// Mốc “vĩnh viễn”. Không dùng <c>'infinity'</c> của PostgreSQL vì EF/Npgsql không đọc
    /// được giá trị đó vào <see cref="DateTimeOffset"/> (quyết định D7).
    /// </summary>
    public static readonly DateTimeOffset ForeverUntil = new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);

    /// <summary>Các giá trị hợp lệ của trường `duration`.</summary>
    public static readonly IReadOnlyList<string> Durations =
        [Off, OneHour, EightHours, OneWeek, Forever];

    /// <summary>Chuỗi thời hạn có thuộc danh sách cho phép không.</summary>
    public static bool IsValidDuration(string? duration) =>
        duration is not null && Durations.Contains(duration);

    /// <summary>
    /// Quy đổi lựa chọn thời hạn thành mốc <c>muted_until</c>; <c>null</c> = bật thông báo.
    /// Client gửi lựa chọn thay vì mốc tuyệt đối để tránh lệch đồng hồ giữa máy và server.
    /// </summary>
    public static DateTimeOffset? ResolveMutedUntil(string duration, DateTimeOffset now) => duration switch
    {
        Off => null,
        OneHour => now.AddHours(1),
        EightHours => now.AddHours(8),
        OneWeek => now.AddDays(7),
        Forever => ForeverUntil,
        _ => null
    };

    /// <summary>Hội thoại đang bị tắt thông báo tại thời điểm <paramref name="now"/>.</summary>
    public static bool IsMuted(DateTimeOffset? mutedUntil, DateTimeOffset now) =>
        mutedUntil is not null && mutedUntil.Value > now;

    /// <summary>Đang tắt vĩnh viễn (dùng cho nhãn giao diện).</summary>
    public static bool IsForever(DateTimeOffset? mutedUntil) =>
        mutedUntil == ForeverUntil;
}
