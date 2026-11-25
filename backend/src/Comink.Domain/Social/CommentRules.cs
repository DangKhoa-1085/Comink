// Quy tắc nghiệp vụ cho bình luận (chỉ cho phép một cấp trả lời).
namespace Comink.Domain.Social;

/// <summary>
/// Quy tắc bình luận theo docs/03_database_design.md mục 3.2 (gợi ý 💡: chỉ một cấp trả lời)
/// và hợp đồng API mục 3.6.
/// </summary>
public static class CommentRules
{
    /// <summary>Độ sâu tối đa của trả lời (1 = trả lời trực tiếp bình luận gốc).</summary>
    public const int MaxReplyDepth = 1;

    /// <summary>Độ dài tối đa của nội dung bình luận.</summary>
    public const int MaxLength = 2000;

    /// <summary>
    /// Có được trả lời bình luận này không. Chỉ bình luận gốc
    /// (<paramref name="parentOfParentId"/> là null) mới nhận trả lời.
    /// </summary>
    public static bool CanReplyTo(Guid? parentOfParentId) => parentOfParentId is null;

    /// <summary>Nội dung bình luận hợp lệ (không rỗng, không vượt giới hạn).</summary>
    public static bool IsContentValid(string? content) =>
        !string.IsNullOrWhiteSpace(content) && content.Length <= MaxLength;
}
