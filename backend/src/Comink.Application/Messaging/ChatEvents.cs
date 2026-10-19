// Tên sự kiện SignalR của module Nhắn tin — hằng số dùng chung backend/web
// (docs/02_tech_stack_decisions.md mục 5.8: không viết tay tên sự kiện rải rác).
namespace Comink.Application.Messaging;

/// <summary>Tên sự kiện hub và tiền tố group.</summary>
public static class ChatEvents
{
    /// <summary>Server → client: có tin nhắn mới trong hội thoại.</summary>
    public const string MessageCreated = "MessageCreated";

    /// <summary>Group theo hội thoại: <c>conversation:{id}</c>.</summary>
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId}";

    /// <summary>Group riêng của người dùng: <c>user:{id}</c> (đã dùng từ GĐ 1).</summary>
    public static string UserGroup(Guid userId) => $"user:{userId}";
}
