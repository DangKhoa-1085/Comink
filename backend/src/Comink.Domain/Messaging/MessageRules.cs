// Quy tắc nghiệp vụ cho tin nhắn (docs/05_api_contract.md mục 8.6).
namespace Comink.Domain.Messaging;

/// <summary>Giới hạn và quy tắc nội dung một tin nhắn.</summary>
public static class MessageRules
{
    /// <summary>Độ dài tối đa của nội dung — khớp CHECK constraint `char_length(content) <= 4000`.</summary>
    public const int MaxContentLength = 4000;

    /// <summary>Số tệp đính kèm tối đa trong một tin.</summary>
    public const int MaxAttachments = 10;

    /// <summary>Tin phải có nội dung, hoặc có ít nhất một tệp đính kèm.</summary>
    public static bool HasPayload(string? content, int attachmentCount) =>
        !string.IsNullOrWhiteSpace(content) || attachmentCount > 0;

    /// <summary>Nội dung (nếu có) không được vượt quá <see cref="MaxContentLength"/>.</summary>
    public static bool IsContentLengthValid(string? content) =>
        string.IsNullOrEmpty(content) || content.Length <= MaxContentLength;

    /// <summary>Loại tin suy ra từ tệp đính kèm (CB chỉ có Text và Image; video/tệp là [NC]).</summary>
    public static Enums.MessageType ResolveType(int attachmentCount) =>
        attachmentCount > 0 ? Enums.MessageType.Image : Enums.MessageType.Text;
}
