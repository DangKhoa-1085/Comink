// Chuẩn hoá chuỗi kết nối PostgreSQL và Redis (hỗ trợ cả URI từ Neon/Railway lẫn key-value dev).
using Npgsql;
using StackExchange.Redis;

namespace Comink.Infrastructure;

/// <summary>
/// Chuyển đổi chuỗi kết nối dạng URI (<c>postgresql://</c>, <c>redis://</c>) sang định dạng mà
/// Npgsql và StackExchange.Redis hiểu được, đồng thời giữ nguyên định dạng key-value khi dev.
/// </summary>
public static class ConnectionStringNormalizer
{
    /// <summary>
    /// Chuẩn hoá chuỗi kết nối PostgreSQL. Nếu là URI <c>postgres://</c> hoặc <c>postgresql://</c>
    /// (ví dụ lấy từ Neon) thì chuyển sang chuỗi key-value của <see cref="NpgsqlConnectionStringBuilder"/>;
    /// ngược lại trả về nguyên bản.
    /// </summary>
    public static string? NormalizePostgres(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var trimmed = connectionString.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(trimmed);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port
        };

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (!string.IsNullOrEmpty(database))
        {
            builder.Database = database;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userInfoParts = uri.UserInfo.Split(':', 2);
            if (!string.IsNullOrEmpty(userInfoParts[0]))
            {
                builder.Username = Uri.UnescapeDataString(userInfoParts[0]);
            }

            if (userInfoParts.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(userInfoParts[1]);
            }
        }

        ApplyPostgresQueryParameters(builder, uri.Query);
        return builder.ConnectionString;
    }

    /// <summary>
    /// Phân tích chuỗi kết nối Redis (hỗ trợ cả URI <c>redis://</c>/<c>rediss://</c> từ Railway
    /// lẫn chuỗi key-value thông thường). Mặc định tắt <c>AbortOnConnectFail</c> để tránh crash
    /// khi container API khởi động sớm hơn Redis vài giây.
    /// </summary>
    public static ConfigurationOptions ParseRedis(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Chuỗi kết nối Redis không được để trống.", nameof(connectionString));
        }

        var trimmed = connectionString.Trim();
        if (trimmed.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(trimmed);
            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            };

            options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfoParts = uri.UserInfo.Split(':', 2);
                if (userInfoParts.Length == 1)
                {
                    options.Password = Uri.UnescapeDataString(userInfoParts[0]);
                }
                else
                {
                    if (!string.IsNullOrEmpty(userInfoParts[0]))
                    {
                        options.User = Uri.UnescapeDataString(userInfoParts[0]);
                    }

                    if (!string.IsNullOrEmpty(userInfoParts[1]))
                    {
                        options.Password = Uri.UnescapeDataString(userInfoParts[1]);
                    }
                }
            }

            var path = uri.AbsolutePath.TrimStart('/');
            if (int.TryParse(path, out var dbIndex))
            {
                options.DefaultDatabase = dbIndex;
            }

            return options;
        }

        var parsed = ConfigurationOptions.Parse(trimmed);
        if (!trimmed.Contains("abortConnect=", StringComparison.OrdinalIgnoreCase))
        {
            parsed.AbortOnConnectFail = false;
        }

        return parsed;
    }

    private static void ApplyPostgresQueryParameters(NpgsqlConnectionStringBuilder builder, string rawQuery)
    {
        if (string.IsNullOrEmpty(rawQuery))
        {
            return;
        }

        var query = rawQuery.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]).ToLowerInvariant() : string.Empty;

            switch (key)
            {
                case "sslmode":
                    builder.SslMode = value switch
                    {
                        "disable" => SslMode.Disable,
                        "allow" => SslMode.Allow,
                        "prefer" => SslMode.Prefer,
                        "require" => SslMode.Require,
                        "verify-ca" => SslMode.VerifyCA,
                        "verify-full" => SslMode.VerifyFull,
                        _ => builder.SslMode
                    };
                    break;

                case "channel_binding":
                    builder.ChannelBinding = value switch
                    {
                        "disable" => ChannelBinding.Disable,
                        "prefer" => ChannelBinding.Prefer,
                        "require" => ChannelBinding.Require,
                        _ => builder.ChannelBinding
                    };
                    break;
            }
        }
    }
}
