// Test FCM sender bằng HTTP handler giả: OAuth token + gửi message (docs/05 mục 10.3).
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comink.Application.Common.Interfaces;
using Comink.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Comink.Api.IntegrationTests;

public class FcmPushSenderTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string? Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                body));

            if (request.RequestUri!.Host.Contains("oauth", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"test-access-token","expires_in":3600}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private static string ServiceAccountJson()
    {
        using var rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new
        {
            project_id = "comink-test",
            client_email = "firebase@comink-test.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
        });
    }

    private static FcmPushSender NewSender(
        RecordingHandler handler,
        bool configure = true)
    {
        var options = configure
            ? new FirebasePushOptions
            {
                ServiceAccountJson = ServiceAccountJson(),
                OAuthUrl = "https://oauth.test/token",
                FcmBaseUrl = "https://fcm.test",
            }
            : new FirebasePushOptions();

        return new FcmPushSender(
            Options.Create(options),
            NullLogger<FcmPushSender>.Instance,
            new HttpClient(handler));
    }

    [Fact]
    public async Task SendToDevice_RequestsOAuthThenFcmWithNotificationAndData()
    {
        var handler = new RecordingHandler();
        using var sender = NewSender(handler);

        await sender.SendToDeviceAsync(
            "device-token-1",
            new PushNotification(
                "Tin nhắn mới",
                "An: Xin chào",
                new Dictionary<string, string> { ["type"] = "Message", ["conversationId"] = "c1" },
                HighPriority: true));

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://oauth.test/token", handler.Requests[0].Uri.ToString());
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", handler.Requests[0].Body);

        var fcm = handler.Requests[1];
        Assert.Equal("https://fcm.test/v1/projects/comink-test/messages:send", fcm.Uri.ToString());
        Assert.Equal("Bearer test-access-token", fcm.Authorization);

        using var document = JsonDocument.Parse(fcm.Body);
        var message = document.RootElement.GetProperty("message");
        Assert.Equal("device-token-1", message.GetProperty("token").GetString());
        Assert.Equal("Tin nhắn mới", message.GetProperty("notification").GetProperty("title").GetString());
        Assert.Equal("high", message.GetProperty("android").GetProperty("priority").GetString());
        Assert.Equal("c1", message.GetProperty("data").GetProperty("conversationId").GetString());
    }

    [Fact]
    public async Task SendToDevices_SendsOneRequestPerToken()
    {
        var handler = new RecordingHandler();
        using var sender = NewSender(handler);

        await sender.SendToDevicesAsync(
            ["device-a", "device-b"],
            new PushNotification("Tiêu đề", "Nội dung"));

        Assert.Equal(3, handler.Requests.Count); // 1 OAuth + 2 FCM
        Assert.Contains("\"token\":\"device-a\"", handler.Requests[1].Body);
        Assert.Contains("\"token\":\"device-b\"", handler.Requests[2].Body);
    }

    [Fact]
    public async Task WithoutConfiguration_DoesNotMakeHttpCalls()
    {
        var handler = new RecordingHandler();
        using var sender = NewSender(handler, configure: false);

        await sender.SendToDeviceAsync("device-token-1", new PushNotification("A", "B"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MissingServiceAccountFile_DoesNotThrowAndSkipsHttp()
    {
        // Cấu hình Firebase__ServiceAccountPath trỏ tới file không tồn tại là lỗi hay gặp khi
        // cấu hình FCM lần đầu: sender phải bỏ qua an toàn (chỉ log), không làm hỏng request
        // đang gửi push và không gọi mạng.
        var handler = new RecordingHandler();
        var options = new FirebasePushOptions
        {
            ServiceAccountPath = Path.Combine(
                Path.GetTempPath(),
                $"comink-missing-service-account-{Guid.NewGuid():N}.json"),
            OAuthUrl = "https://oauth.test/token",
            FcmBaseUrl = "https://fcm.test",
        };

        using var sender = new FcmPushSender(
            Options.Create(options),
            NullLogger<FcmPushSender>.Instance,
            new HttpClient(handler));

        await sender.SendToDeviceAsync("device-token-1", new PushNotification("A", "B"));

        Assert.Empty(handler.Requests);
    }
}
