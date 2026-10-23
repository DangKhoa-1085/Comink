// Fake IAccountNotifier: ghi lại các lần thông báo khóa tài khoản trong unit test.
using Comink.Application.Admin.Abstractions;

namespace Comink.Application.Tests.Fakes;

/// <summary>Bản giả ghi nhận lời gọi <see cref="AccountLockedAsync"/>.</summary>
public class FakeAccountNotifier : IAccountNotifier
{
    /// <summary>Các id người dùng đã nhận thông báo khóa.</summary>
    public List<Guid> LockedUserIds { get; } = [];

    public Task AccountLockedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        LockedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
