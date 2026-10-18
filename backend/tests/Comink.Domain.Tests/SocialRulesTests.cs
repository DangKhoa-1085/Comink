// Test quy tắc nghiệp vụ thuần (không cần DB) — trọng tâm là quyền xem bài viết.
using Comink.Domain.Enums;
using Comink.Domain.Social;
using Xunit;

namespace Comink.Domain.Tests;

public class PostVisibilityRulesTests
{
    [Theory]
    // Bài công khai: ai cũng xem được
    [InlineData(Visibility.Public, false, false, true)]
    [InlineData(Visibility.Public, false, true, true)]
    // Bài bạn bè: người lạ không xem được, bạn bè xem được
    [InlineData(Visibility.Friends, false, false, false)]
    [InlineData(Visibility.Friends, false, true, true)]
    // Bài chỉ mình tôi: kể cả bạn bè cũng không xem được
    [InlineData(Visibility.OnlyMe, false, false, false)]
    [InlineData(Visibility.OnlyMe, false, true, false)]
    // Tác giả luôn xem được bài của mình
    [InlineData(Visibility.OnlyMe, true, false, true)]
    [InlineData(Visibility.Friends, true, false, true)]
    public void CanView_FollowsContract(Visibility visibility, bool isAuthor, bool areFriends, bool expected) =>
        Assert.Equal(expected, PostVisibilityRules.CanView(visibility, isAuthor, areFriends));

    [Theory]
    [InlineData(ContentStatus.Active, false, true)]
    [InlineData(ContentStatus.Hidden, false, false)]
    [InlineData(ContentStatus.Active, true, false)]
    public void IsVisible_RequiresActiveAndNotDeleted(ContentStatus status, bool deleted, bool expected) =>
        Assert.Equal(expected, PostVisibilityRules.IsVisible(status, deleted ? DateTimeOffset.UtcNow : null));
}

public class FriendshipRulesTests
{
    [Fact]
    public void SelfPair_IsRejected() => Assert.True(FriendshipRules.IsSelfPair(Guid.NewGuid(), Guid.NewGuid()) is false);

    [Fact]
    public void SameId_IsSelfPair()
    {
        var id = Guid.CreateVersion7();
        Assert.True(FriendshipRules.IsSelfPair(id, id));
    }

    [Fact]
    public void CanSendRequest_WhenAlreadyFriends_IsFalse() =>
        Assert.False(FriendshipRules.CanSendRequest(FriendshipStatus.Accepted));

    [Fact]
    public void CanSendRequest_WhenPending_IsTrue() =>
        Assert.True(FriendshipRules.CanSendRequest(FriendshipStatus.Pending));

    [Fact]
    public void Decrement_NeverGoesBelowZero()
    {
        Assert.Equal(0, FriendshipRules.Decrement(0));
        Assert.Equal(0, FriendshipRules.Decrement(1));
        Assert.Equal(1, FriendshipRules.Decrement(2));
    }
}

public class CommentRulesTests
{
    [Fact]
    public void CanReplyTo_RootComment_IsTrue() => Assert.True(CommentRules.CanReplyTo(null));

    [Fact]
    public void CanReplyTo_Reply_IsFalse() => Assert.False(CommentRules.CanReplyTo(Guid.CreateVersion7()));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Nội dung hợp lệ", true)]
    public void IsContentValid_RejectsBlank(string? content, bool expected) =>
        Assert.Equal(expected, CommentRules.IsContentValid(content));

    [Fact]
    public void IsContentValid_RejectsTooLong()
    {
        var tooLong = new string('a', CommentRules.MaxLength + 1);
        Assert.False(CommentRules.IsContentValid(tooLong));
    }
}
