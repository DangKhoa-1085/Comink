// Chặn spam tín hiệu "đang nhập" phía server — lớp bảo vệ thứ hai sau throttle phía client
// (docs/02_tech_stack_decisions.md mục 5.2; quyết định D4 của phase7).
using System.Collections.Concurrent;

namespace Comink.Api.Realtime;

/// <summary>Giới hạn tần suất tín hiệu "bắt đầu gõ" của mỗi người trong mỗi hội thoại.</summary>
public interface ITypingThrottle
{
    /// <summary>Trả <c>true</c> nếu được phát tín hiệu; <c>false</c> nếu còn quá gần lần trước.</summary>
    bool TryAcquire(Guid userId, Guid conversationId);

    /// <summary>Xoá mốc khi người dùng ngừng gõ để lần gõ kế tiếp được phát ngay.</summary>
    void Release(Guid userId, Guid conversationId);
}

/// <inheritdoc />
public sealed class TypingThrottle : ITypingThrottle
{
    /// <summary>Tối thiểu 2 giây giữa hai tín hiệu "bắt đầu gõ" (client tự throttle 3 giây).</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<(Guid UserId, Guid ConversationId), DateTimeOffset> _lastSentAt = new();

    /// <inheritdoc />
    public bool TryAcquire(Guid userId, Guid conversationId)
    {
        var key = (userId, conversationId);
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            if (!_lastSentAt.TryGetValue(key, out var lastSentAt))
            {
                if (_lastSentAt.TryAdd(key, now))
                {
                    return true;
                }

                continue;
            }

            if (now - lastSentAt < MinInterval)
            {
                return false;
            }

            if (_lastSentAt.TryUpdate(key, now, lastSentAt))
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public void Release(Guid userId, Guid conversationId) =>
        _lastSentAt.TryRemove((userId, conversationId), out _);
}
