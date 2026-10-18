// Phân trang theo con trỏ dùng chung cho tầng Application và Api (docs/05_api_contract.md mục 1.2).
namespace Comink.Application.Common;

/// <summary>
/// Kết quả phân trang theo con trỏ <c>(created_at, id)</c>. Không dùng offset
/// (AGENTS.md mục 8.1).
/// </summary>
/// <typeparam name="T">Kiểu phần tử trong danh sách.</typeparam>
public record PagedList<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

/// <summary>Mã hóa / giải mã con trỏ phân trang dạng <c>base64(createdAt|id)</c>.</summary>
public static class CursorCodec
{
    private const char Separator = '|';

    /// <summary>Mã hóa con trỏ từ (created_at, id).</summary>
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt.ToUniversalTime():O}{Separator}{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>Giải mã con trỏ; trả về null nếu con trỏ không hợp lệ.</summary>
    public static (DateTimeOffset CreatedAt, Guid Id)? TryDecode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(Separator);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(parts[0], out var createdAt))
            {
                return null;
            }

            return Guid.TryParse(parts[1], out var id) ? (createdAt, id) : null;
        }
        catch (FormatException)
        {
            // Con trỏ không phải base64 hợp lệ → coi như không có con trỏ.
            return null;
        }
    }
}
