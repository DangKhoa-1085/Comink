// Unit test cho quy tắc miền và FluentValidation của tính năng Comink Locket.
using Comink.Application.Social.Dtos;
using Comink.Application.Social.Validators;
using Comink.Domain.Enums;
using Comink.Domain.Social;

namespace Comink.Application.Tests.Social;

public class LocketTests
{
    [Fact]
    public void CanView_AllowsAuthorBeforeExpiry_AndDeniesAfter24HoursOrDelete()
    {
        var authorId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var expiresAt = now.AddHours(24);

        Assert.True(LocketRules.CanView(
            authorId, authorId, isFriend: false,
            LocketAudience.SelectedFriends, isExplicitRecipient: false,
            expiresAt, deletedAt: null, now));

        // Đúng mốc 24 giờ → hết hạn, không ai xem được nữa.
        Assert.False(LocketRules.CanView(
            authorId, authorId, isFriend: true,
            LocketAudience.AllFriends, isExplicitRecipient: true,
            expiresAt, deletedAt: null, expiresAt));

        // Đã xóa mềm sớm → ẩn ngay lập tức.
        Assert.False(LocketRules.CanView(
            authorId, authorId, isFriend: true,
            LocketAudience.AllFriends, isExplicitRecipient: true,
            expiresAt, deletedAt: now.AddMinutes(5), now.AddMinutes(10)));
    }

    [Fact]
    public void CanView_EnforcesFriendshipAndSelectedRecipients()
    {
        var authorId = Guid.CreateVersion7();
        var friendId = Guid.CreateVersion7();
        var strangerId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var expiresAt = now.AddHours(24);

        // Người lạ không xem được dù AllFriends.
        Assert.False(LocketRules.CanView(
            strangerId, authorId, isFriend: false,
            LocketAudience.AllFriends, isExplicitRecipient: false,
            expiresAt, deletedAt: null, now));

        // Bạn bè xem được AllFriends.
        Assert.True(LocketRules.CanView(
            friendId, authorId, isFriend: true,
            LocketAudience.AllFriends, isExplicitRecipient: false,
            expiresAt, deletedAt: null, now));

        // Với SelectedFriends, bạn không nằm trong danh sách chọn cũng không xem được.
        Assert.False(LocketRules.CanView(
            friendId, authorId, isFriend: true,
            LocketAudience.SelectedFriends, isExplicitRecipient: false,
            expiresAt, deletedAt: null, now));

        // Chỉ bạn vừa là bạn bè vừa được chọn mới xem được.
        Assert.True(LocketRules.CanView(
            friendId, authorId, isFriend: true,
            LocketAudience.SelectedFriends, isExplicitRecipient: true,
            expiresAt, deletedAt: null, now));
    }

    [Theory]
    [InlineData("❤️", true)]
    [InlineData("😂", true)]
    [InlineData("😮", true)]
    [InlineData("🔥", true)]
    [InlineData("🥹", true)]
    [InlineData("👎", false)]
    [InlineData("", false)]
    public void IsValidReaction_AcceptsOnlyAllowedEmojis(string emoji, bool expected)
    {
        Assert.Equal(expected, LocketRules.IsValidReaction(emoji));
    }

    [Fact]
    public async Task CreateValidator_RejectsSelectedFriendsWithoutRecipients_AndCaptionOver100()
    {
        var validator = new CreateLocketRequestValidator();

        var missingRecipients = await validator.ValidateAsync(new CreateLocketRequest(
            Guid.CreateVersion7(),
            "Hihi",
            "SelectedFriends",
            RecipientIds: []));
        Assert.False(missingRecipients.IsValid);

        var tooLongCaption = await validator.ValidateAsync(new CreateLocketRequest(
            Guid.CreateVersion7(),
            new string('a', 101),
            "AllFriends"));
        Assert.False(tooLongCaption.IsValid);

        var valid = await validator.ValidateAsync(new CreateLocketRequest(
            Guid.CreateVersion7(),
            "Đang code Comink Locket ✨",
            "SelectedFriends",
            RecipientIds: [Guid.CreateVersion7()]));
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task ReactAndReplyValidators_ValidatePayload()
    {
        var reactValidator = new ReactLocketRequestValidator();
        Assert.True((await reactValidator.ValidateAsync(new ReactLocketRequest("🔥"))).IsValid);
        Assert.False((await reactValidator.ValidateAsync(new ReactLocketRequest("💩"))).IsValid);

        var replyValidator = new ReplyLocketRequestValidator();
        Assert.True((await replyValidator.ValidateAsync(new ReplyLocketRequest("Đẹp quá!"))).IsValid);
        Assert.False((await replyValidator.ValidateAsync(new ReplyLocketRequest("   "))).IsValid);
    }
}
