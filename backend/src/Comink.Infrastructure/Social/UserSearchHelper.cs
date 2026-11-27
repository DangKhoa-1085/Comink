// Chuẩn hoá từ khoá tìm kiếm người dùng (@username / tên hiển thị) và mã hoá con trỏ theo hạng khớp.
using System.Globalization;
using System.Text;
using Comink.Application.Common;

namespace Comink.Infrastructure.Social;

/// <summary>Tiện ích chuẩn hoá từ khoá và con trỏ cho tìm kiếm người dùng theo hạng khớp.</summary>
public static class UserSearchHelper
{
    /// <summary>
    /// Chuẩn hoá từ khoá: bỏ khoảng trắng hai đầu, bỏ các ký tự <c>@</c> ở đầu để gõ
    /// <c>@Khoa</c> hay <c>Khoa</c> đều khớp cùng người dùng, và đưa về chữ thường.
    /// </summary>
    public static string NormalizeKeyword(string raw) =>
        raw.Trim().TrimStart('@').Trim().ToLowerInvariant();

    /// <summary>Bỏ dấu tiếng Việt và chuẩn hoá chữ thường (khớp cả khi gõ có dấu hoặc không dấu).</summary>
    public static string RemoveVietnameseDiacritics(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => ch
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Escape các ký tự đại diện của SQL LIKE (<c>\</c>, <c>%</c>, <c>_</c>).</summary>
    public static string EscapeLike(string input) =>
        input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>Mã hoá con trỏ phân trang kèm hạng ưu tiên <paramref name="matchRank"/>.</summary>
    public static string EncodeCursor(int matchRank, DateTimeOffset createdAt, Guid id) =>
        $"r{matchRank}:{CursorCodec.Encode(createdAt, id)}";

    /// <summary>Giải mã con trỏ tìm kiếm; trả null nếu con trỏ rỗng hoặc không hợp lệ.</summary>
    public static (int MatchRank, DateTimeOffset CreatedAt, Guid Id)? TryDecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || !cursor.StartsWith('r'))
        {
            return null;
        }

        var separator = cursor.IndexOf(':', 1);
        if (separator <= 1)
        {
            return null;
        }

        if (!int.TryParse(cursor.AsSpan(1, separator - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var rank))
        {
            return null;
        }

        var decoded = CursorCodec.TryDecode(cursor[(separator + 1)..]);
        return decoded is null ? null : (rank, decoded.Value.CreatedAt, decoded.Value.Id);
    }
}
