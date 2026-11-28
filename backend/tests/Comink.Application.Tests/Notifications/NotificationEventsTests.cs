// Kiểm tra hằng số sự kiện SignalR của module Thông báo và trạng thái kết bạn.
using Comink.Application.Notifications;

namespace Comink.Application.Tests.Notifications;

public class NotificationEventsTests
{
    [Fact]
    public void EventNames_MatchFrontendAndMobileHubContract()
    {
        Assert.Equal("NotificationCreated", NotificationEvents.NotificationCreated);
        Assert.Equal("FriendshipChanged", NotificationEvents.FriendshipChanged);
    }
}
