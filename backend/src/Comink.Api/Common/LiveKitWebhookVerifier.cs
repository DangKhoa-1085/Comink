// Xác thực chữ ký JWT của webhook LiveKit (HS256 bằng ApiSecret + đối chiếu claim sha256).
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Comink.Api.Common;

/// <summary>
/// Kiểm tra webhook LiveKit có thật sự do LiveKit gửi hay không.
/// Bắt buộc theo `docs/tasks/phase4_calls.md` (D5) — endpoint webhook là `AllowAnonymous`
/// nên không được tin payload khi chưa xác thực.
/// </summary>
public static class LiveKitWebhookVerifier
{
    /// <summary>
    /// Xác thực token HS256 và đối chiếu `sha256` trong token với hash của body.
    /// </summary>
    public static bool IsValid(string token, string body, string apiSecret)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var unsigned = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Base64UrlEncode(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(apiSecret), Encoding.UTF8.GetBytes(unsigned)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[2])))
        {
            return false;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            if (!payload.RootElement.TryGetProperty("sha256", out var hashProperty))
            {
                return false;
            }

            var expectedBodyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
            return string.Equals(hashProperty.GetString(), expectedBodyHash, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized += new string('=', 4 - padding);
        }

        return Convert.FromBase64String(normalized);
    }
}
