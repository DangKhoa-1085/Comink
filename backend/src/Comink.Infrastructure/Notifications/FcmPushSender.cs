// Gửi push FCM HTTP v1 bằng service account (không phụ thuộc SDK).
// Nếu chưa cấu hình Firebase, sender chỉ ghi log và bỏ qua — app vẫn chạy bình thường.
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comink.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comink.Infrastructure.Notifications;

public class FirebasePushOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Toàn bộ JSON service account (đặt trong .env/user-secrets, không commit).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>Đường dẫn tới file JSON service account (cách khác thay cho chuỗi JSON).</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceAccountJson) ||
        !string.IsNullOrWhiteSpace(ServiceAccountPath);
}

/// <summary>Gửi notification qua FCM HTTP v1; tự lấy access token OAuth2 và cache đến khi hết hạn.</summary>
public class FcmPushSender : IPushSender, IDisposable
{
    private static readonly HttpClient Http = new();
    private readonly FirebasePushOptions _options;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;
    private ServiceAccount? _account;

    public FcmPushSender(IOptions<FirebasePushOptions> options, ILogger<FcmPushSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendToDeviceAsync(
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken = default) =>
        await SendToDevicesAsync([deviceToken], notification, cancellationToken);

    public async Task SendToDevicesAsync(
        IEnumerable<string> deviceTokens,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogDebug(
                "Bỏ qua push '{Title}': chưa cấu hình Firebase service account",
                notification.Title);
            return;
        }

        var account = await GetAccountAsync(cancellationToken);
        if (account is null)
        {
            return;
        }

        var token = await GetAccessTokenAsync(account, cancellationToken);
        if (token is null)
        {
            return;
        }

        foreach (var deviceToken in deviceTokens.Distinct())
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                continue;
            }

            await SendOneAsync(account.ProjectId, token, deviceToken, notification, cancellationToken);
        }
    }

    private async Task SendOneAsync(
        string projectId,
        string accessToken,
        string deviceToken,
        PushNotification notification,
        CancellationToken cancellationToken)
    {
        var message = new Dictionary<string, object>
        {
            ["message"] = new Dictionary<string, object>
            {
                ["token"] = deviceToken,
                ["notification"] = new Dictionary<string, string>
                {
                    ["title"] = notification.Title,
                    ["body"] = notification.Body,
                },
                ["data"] = notification.Data ?? new Dictionary<string, string>(),
                ["android"] = new Dictionary<string, object>
                {
                    ["priority"] = notification.HighPriority ? "high" : "normal",
                    ["notification"] = new Dictionary<string, string>
                    {
                        ["sound"] = "default",
                    },
                },
                ["apns"] = new Dictionary<string, object>
                {
                    ["headers"] = new Dictionary<string, string>
                    {
                        ["apns-priority"] = notification.HighPriority ? "10" : "5",
                    },
                    ["payload"] = new Dictionary<string, object>
                    {
                        ["aps"] = new Dictionary<string, object>
                        {
                            ["sound"] = "default",
                        },
                    },
                },
            },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(message),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "FCM trả {Status} khi gửi push: {Body}",
                    (int)response.StatusCode,
                    body.Length > 500 ? body[..500] : body);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Không gửi được push FCM tới một thiết bị");
        }
    }

    private async Task<ServiceAccount?> GetAccountAsync(CancellationToken cancellationToken)
    {
        if (_account is not null)
        {
            return _account;
        }

        try
        {
            var json = _options.ServiceAccountJson;
            if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(_options.ServiceAccountPath))
            {
                json = await File.ReadAllTextAsync(_options.ServiceAccountPath, cancellationToken);
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var projectId = root.GetProperty("project_id").GetString();
            var clientEmail = root.GetProperty("client_email").GetString();
            var privateKey = root.GetProperty("private_key").GetString();
            if (string.IsNullOrWhiteSpace(projectId) ||
                string.IsNullOrWhiteSpace(clientEmail) ||
                string.IsNullOrWhiteSpace(privateKey))
            {
                _logger.LogWarning("Firebase service account thiếu project_id/client_email/private_key");
                return null;
            }

            _account = new ServiceAccount(projectId, clientEmail, privateKey);
            return _account;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Không đọc được Firebase service account");
            return null;
        }
    }

    private async Task<string?> GetAccessTokenAsync(
        ServiceAccount account,
        CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt)
            {
                return _accessToken;
            }

            var assertion = CreateAssertion(account);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://oauth2.googleapis.com/token");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            });

            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Không lấy được FCM access token: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.TryGetProperty("expires_in", out var value)
                ? value.GetInt32()
                : 3600;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            return _accessToken;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Không lấy được FCM access token");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string CreateAssertion(ServiceAccount account)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT",
        }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = "https://oauth2.googleapis.com/token",
            iat = now,
            exp = now + 3600,
        }));
        var unsigned = $"{header}.{claims}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _tokenLock.Dispose();

    private sealed record ServiceAccount(string ProjectId, string ClientEmail, string PrivateKey);
}
