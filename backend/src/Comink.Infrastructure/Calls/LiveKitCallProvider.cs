// Cài đặt ICallProvider cho LiveKit: sinh JWT ngắn hạn và gọi Twirp API để tạo/xoá room.
// Tài liệu: https://docs.livekit.io/home/server/generating-tokens/ và /server/room-service/
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comink.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comink.Infrastructure.Calls;

/// <summary>
/// Gọi LiveKit: tạo/xoá room qua Twirp API, cấp token vào room bằng JWT HS256.
/// <c>ApiSecret</c> chỉ nằm ở backend, không bao giờ trả cho client.
/// </summary>
public class LiveKitCallProvider : ICallProvider, IDisposable
{
    private const int RoomEmptyTimeoutSeconds = 60;
    private const int RoomDepartureTimeoutSeconds = 20;
    private const int AdminTokenTtlSeconds = 60;

    private readonly HttpClient _http;
    private readonly LiveKitOptions _options;
    private readonly ILogger<LiveKitCallProvider> _logger;

    public LiveKitCallProvider(
        IOptions<LiveKitOptions> options,
        ILogger<LiveKitCallProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <inheritdoc />
    public async Task<string> CreateRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        // Room tự động bị xoá khi không còn ai: empty_timeout + departure_timeout
        // (lớp bảo vệ thứ nhất chống "room treo" — docs/tasks/phase4_calls.md mục 6).
        var body = new
        {
            name = roomName,
            empty_timeout = RoomEmptyTimeoutSeconds,
            departure_timeout = RoomDepartureTimeoutSeconds
        };

        await TwirpAsync("RoomService/CreateRoom", body, AdminToken(), cancellationToken);
        _logger.LogInformation("Đã tạo room LiveKit {Room}", roomName);
        return roomName;
    }

    /// <inheritdoc />
    public async Task EndRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            await TwirpAsync("RoomService/DeleteRoom", new { room = roomName }, AdminToken(), cancellationToken);
            _logger.LogInformation("Đã xoá room LiveKit {Room}", roomName);
        }
        catch (Exception exception)
        {
            // Room có thể đã tự xoá (hết empty_timeout) — không coi là lỗi chặn nghiệp vụ.
            _logger.LogWarning(exception, "Không xoá được room LiveKit {Room} (có thể đã tự xoá)", roomName);
        }
    }

    /// <inheritdoc />
    public Task<string> GenerateAccessTokenAsync(
        string roomName,
        string userId,
        string displayName,
        bool canPublish = true,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = _options.ApiKey,
            ["sub"] = userId,
            ["name"] = displayName,
            ["nbf"] = now,
            // Token NGẮN HẠN: chỉ đủ để vào room, không dùng cho thao tác quản trị.
            ["exp"] = now + Domain.Calls.CallRules.JoinTokenTtlSeconds,
            ["video"] = new Dictionary<string, object?>
            {
                ["room"] = roomName,
                ["roomJoin"] = true,
                ["canPublish"] = canPublish,
                ["canSubscribe"] = true
            }
        };

        return Task.FromResult(SignJwt(payload));
    }

    public void Dispose() => _http.Dispose();

    /// <summary>JWT cho thao tác quản trị (tạo/xoá room); hạn 60 giây.</summary>
    private string AdminToken()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return SignJwt(new Dictionary<string, object?>
        {
            ["iss"] = _options.ApiKey,
            ["sub"] = "comink-backend",
            ["nbf"] = now,
            ["exp"] = now + AdminTokenTtlSeconds,
            ["video"] = new Dictionary<string, object?>
            {
                ["roomCreate"] = true,
                ["roomList"] = true,
                ["roomAdmin"] = true
            }
        });
    }

    /// <summary>Ký JWT HS256: base64url(header).base64url(payload).base64url(HMAC-SHA256).</summary>
    private string SignJwt(IDictionary<string, object?> payload)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsigned = $"{header}.{body}";
        var signature = Base64Url(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.ApiSecret), Encoding.UTF8.GetBytes(unsigned)));

        return $"{unsigned}.{signature}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Gọi một method Twirp của LiveKit (JSON, Bearer token).</summary>
    private async Task TwirpAsync(
        string method,
        object body,
        string token,
        CancellationToken cancellationToken)
    {
        var url = $"{_options.Host.TrimEnd('/')}/twirp/livekit.{method}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture,
                    $"LiveKit {method} lỗi {(int)response.StatusCode}: {error}"));
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Chưa cấu hình LiveKit:ApiKey/LiveKit:ApiSecret (xem .env hoặc user-secrets).");
        }
    }
}
