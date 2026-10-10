// Interface nhà cung cấp AI (Gemini, OpenAI, Claude, v.v.)
namespace Comink.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng AI provider. Thiết kế để dễ switch giữa Gemini, OpenAI, Claude.
/// Khóa API chỉ nằm ở backend (Infrastructure), không bao giờ gửi xuống client.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Gửi chat message và nhận phản hồi (non-streaming).
    /// </summary>
    Task<string> ChatAsync(
        IEnumerable<AiChatMessage> messages,
        AiOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi chat message và nhận phản hồi dạng streaming.
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<AiChatMessage> messages,
        AiOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Một tin nhắn trong context AI.
/// </summary>
public record AiChatMessage(AiRole Role, string Content);

/// <summary>
/// Vai trò trong hội thoại AI.
/// </summary>
public enum AiRole { User, Assistant, System }

/// <summary>
/// Tùy chọn cho AI request.
/// </summary>
public record AiOptions(
    int? MaxTokens = null,
    float? Temperature = null);
