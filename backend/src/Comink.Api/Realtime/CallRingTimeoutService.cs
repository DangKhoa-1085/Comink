// Background service đóng các cuộc gọi quá giờ đổ chuông (docs/05_api_contract.md mục 9.1:
// "Đổ chuông tối đa 30 giây, quá hạn → NoAnswer").
using Comink.Application.Calls.Abstractions;

namespace Comink.Api.Realtime;

/// <summary>
/// Quét mỗi vài giây: cuộc gọi còn <c>Ringing</c> quá <c>CallRules.RingTimeoutSeconds</c> mà chưa ai
/// nghe sẽ được đóng thành <c>Missed</c>/<c>NoAnswer</c> và phát <c>CallEnded</c>.
/// <para>
/// Vì sao cần phía server: client có đếm ngược 30s trên modal, nhưng nếu người dùng đóng tab,
/// mất mạng hoặc không nhận được sự kiện thì backend phải tự dọn — nếu không cuộc gọi "treo"
/// mãi ở trạng thái đổ chuông và người gọi bị kẹt trong phòng.
/// </para>
/// </summary>
public class CallRingTimeoutService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallRingTimeoutService> _logger;

    public CallRingTimeoutService(IServiceScopeFactory scopeFactory, ILogger<CallRingTimeoutService> logger)
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
                var callService = scope.ServiceProvider.GetRequiredService<ICallService>();
                await callService.TimeoutRingingCallsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Không để lỗi một lần quét làm chết vòng lặp.
                _logger.LogError(exception, "Lỗi khi dọn cuộc gọi hết giờ đổ chuông");
            }
        }
    }
}
