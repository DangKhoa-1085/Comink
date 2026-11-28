// Background service định kỳ đánh dấu xóa mềm các Locket đã hết hạn 24 giờ.
using Comink.Application.Social.Abstractions;

namespace Comink.Api.Realtime;

/// <summary>
/// Quét mỗi 10 phút để dọn các khoảnh khắc Locket quá 24 giờ (<c>ExpiresAt &lt;= now</c>).
/// Các truy vấn đọc cũng tự lọc <c>ExpiresAt &gt; now</c> nên người dùng không bao giờ thấy ảnh hết hạn,
/// job nền này đảm bảo cột <c>deleted_at</c> được đồng bộ sạch trong CSDL.
/// </summary>
public class LocketExpiryCleanupService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocketExpiryCleanupService> _logger;

    public LocketExpiryCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<LocketExpiryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var locketService = scope.ServiceProvider.GetRequiredService<ILocketService>();
                var cleaned = await locketService.CleanupExpiredAsync(stoppingToken);
                if (cleaned > 0)
                {
                    _logger.LogInformation("Đã dọn {Count} khoảnh khắc Locket hết hạn 24h", cleaned);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Lỗi khi dọn các khoảnh khắc Locket hết hạn");
            }
        }
    }
}
