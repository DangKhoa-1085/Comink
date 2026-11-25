// Presence (trạng thái online) lưu trong Redis — NC Giai đoạn 7, docs/tasks/phase7_advanced_messaging.md.
// Theo docs/02_tech_stack_decisions.md mục 5.2: "Presence lưu trong Redis với TTL; đếm số kết nối
// của mỗi người dùng để xử lý nhiều tab/thiết bị".
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Comink.Infrastructure.Messaging;

/// <summary>
/// Mỗi người dùng có một **sorted set** connection id → thời điểm ping gần nhất, key có TTL.
/// Nhờ vậy:
/// <list type="bullet">
///   <item>nhiều tab cùng mở vẫn đúng (chỉ tab cuối đóng mới offline);</item>
///   <item>client chết không gửi disconnect thì mốc thời gian cũ tự rơi khỏi cửa sổ TTL,
///   không cần dọn tay từng connection.</item>
/// </list>
/// </summary>
public sealed class RedisPresenceService : IPresenceService
{
    private const string ConnectionSetPrefix = "comink:presence:conns:";
    private const string LastSeenPrefix = "comink:presence:lastseen:";

    /// <summary>
    /// Cửa sổ "còn sống" — client gửi <c>Ping</c> mỗi 30 giây nên 90 giây chịu được 2 lần ping
    /// lỡ; ngủ máy lâu hơn thì coi là offline.
    /// </summary>
    public static readonly TimeSpan OnlineTtl = TimeSpan.FromSeconds(90);

    /// <summary>Giữ "truy cập lần cuối" 7 ngày — đủ cho nhãn hiển thị, không phình Redis.</summary>
    public static readonly TimeSpan LastSeenTtl = TimeSpan.FromDays(7);

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _clock;
    private readonly ILogger<RedisPresenceService> _logger;

    public RedisPresenceService(
        IConnectionMultiplexer redis,
        TimeProvider clock,
        ILogger<RedisPresenceService> logger)
    {
        _redis = redis;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ConnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = ConnectionSetKey(userId);
            var now = _clock.GetUtcNow();

            await db.SortedSetAddAsync(key, connectionId, now.ToUnixTimeMilliseconds());
            await db.KeyExpireAsync(key, OnlineTtl);

            // Đếm trong cửa sổ TTL: = 1 nghĩa là trước đó chưa có kết nối nào còn "sống"
            // → vừa chuyển offline → online.
            return await CountAliveAsync(db, key, now) == 1;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogUnavailable(exception, "ghi nhận online");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task TouchAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = ConnectionSetKey(userId);

            // Cập nhật mốc ping để connection này tiếp tục nằm trong cửa sổ còn sống.
            await db.SortedSetAddAsync(key, connectionId, _clock.GetUtcNow().ToUnixTimeMilliseconds());
            await db.KeyExpireAsync(key, OnlineTtl);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogUnavailable(exception, "làm mới presence");
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> DisconnectAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = ConnectionSetKey(userId);
            var now = _clock.GetUtcNow();

            await db.SortedSetRemoveAsync(key, connectionId);

            if (await CountAliveAsync(db, key, now) > 0)
            {
                return null;
            }

            // Kết nối cuối cùng (trong cửa sổ còn sống) đã đóng → offline; ghi "truy cập lần cuối".
            await db.KeyDeleteAsync(key);
            await db.StringSetAsync(LastSeenKey(userId), now.ToString("O"), LastSeenTtl);
            return now;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogUnavailable(exception, "ghi nhận offline");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PresenceDto>> GetStatusesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, PresenceDto>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var distinctIds = userIds.Distinct().ToList();

        try
        {
            var db = _redis.GetDatabase();
            var now = _clock.GetUtcNow();

            var online = await Task.WhenAll(
                distinctIds.Select(async userId => await CountAliveAsync(db, ConnectionSetKey(userId), now) > 0));

            async Task<DateTimeOffset?> LoadLastSeenAsync(Guid userId, bool isUserOnline)
            {
                if (isUserOnline)
                {
                    return null;
                }

                var value = await db.StringGetAsync(LastSeenKey(userId));
                return ParseLastSeen(value);
            }

            var lastSeen = await Task.WhenAll(
                distinctIds.Select((userId, index) => LoadLastSeenAsync(userId, online[index])));

            for (var index = 0; index < distinctIds.Count; index++)
            {
                result[distinctIds[index]] = new PresenceDto(
                    distinctIds[index],
                    online[index],
                    online[index] ? null : lastSeen[index]);
            }
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            // Redis không sẵn sàng: coi mọi người là offline (không ném lỗi cho nghiệp vụ).
            LogUnavailable(exception, "đọc trạng thái online");
            foreach (var userId in distinctIds)
            {
                result[userId] = new PresenceDto(userId, false, null);
            }
        }

        return result;
    }

    /// <summary>Đếm connection có mốc ping nằm trong cửa sổ còn sống (bỏ qua mốc cũ do client chết).</summary>
    private static Task<long> CountAliveAsync(IDatabase db, RedisKey key, DateTimeOffset now) =>
        db.SortedSetLengthAsync(
            key,
            now.Subtract(OnlineTtl).ToUnixTimeMilliseconds(),
            double.PositiveInfinity);

    private static DateTimeOffset? ParseLastSeen(RedisValue value) =>
        value.IsNullOrEmpty
            ? null
            : DateTimeOffset.TryParse(
                value.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed
                : null;

    private static RedisKey ConnectionSetKey(Guid userId) => $"{ConnectionSetPrefix}{userId}";

    private static RedisKey LastSeenKey(Guid userId) => $"{LastSeenPrefix}{userId}";

    /// <summary>Lỗi kết nối/lệnh của Redis — presence được phép suy giảm, không làm sập chat.</summary>
    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or TimeoutException;

    private void LogUnavailable(Exception exception, string action) =>
        _logger.LogWarning(exception, "Redis không sẵn sàng khi {Action} — presence tạm coi là offline", action);
}
