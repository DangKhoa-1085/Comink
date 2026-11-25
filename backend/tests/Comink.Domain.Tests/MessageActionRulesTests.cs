// Test đơn vị cho quy tắc sửa/thu hồi, cảm xúc và tắt thông báo
// (NC Giai đoạn 7 — Ưu tiên 2, docs/tasks/phase7b_advanced_messaging.md mục 2).
using Comink.Domain.Enums;
using Comink.Domain.Messaging;

namespace Comink.Domain.Tests;

public class MessageActionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static MessageActionOutcome Edit(
        Guid? senderId = null,
        Guid? currentUserId = null,
        bool isDeleted = false,
        MessageType type = MessageType.Text,
        DateTimeOffset? createdAt = null) =>
        MessageActionRules.EvaluateEdit(
            senderId ?? Current,
            currentUserId ?? Current,
            isDeleted,
            type,
            createdAt ?? Now.AddHours(-1),
            Now);

    private static readonly Guid Current = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void EvaluateEdit_AllowsOwnerWithinWindow()
    {
        Assert.Equal(MessageActionOutcome.Allowed, Edit(createdAt: Now.AddHours(-23)));
        Assert.Equal(MessageActionOutcome.Allowed, Edit(createdAt: Now));
        // Lệch đồng hồ (tin "ở tương lai") vẫn coi là còn trong hạn.
        Assert.Equal(MessageActionOutcome.Allowed, Edit(createdAt: Now.AddMinutes(5)));
    }

    [Fact]
    public void EvaluateEdit_ExactlyAtWindowIsStillAllowed()
    {
        Assert.Equal(
            MessageActionOutcome.Allowed,
            Edit(createdAt: Now - MessageActionRules.ActionWindow));
    }

    [Fact]
    public void EvaluateEdit_OneTickOverWindowIsExpired()
    {
        var justOver = Now - MessageActionRules.ActionWindow - TimeSpan.FromTicks(1);

        Assert.Equal(MessageActionOutcome.WindowExpired, Edit(createdAt: justOver));
    }

    [Fact]
    public void EvaluateEdit_RejectsNonOwner()
    {
        Assert.Equal(MessageActionOutcome.NotOwner, Edit(senderId: Other, currentUserId: Current));
    }

    [Fact]
    public void EvaluateEdit_RejectsDeletedMessage()
    {
        Assert.Equal(MessageActionOutcome.AlreadyDeleted, Edit(isDeleted: true));
    }

    [Theory]
    [InlineData(MessageType.System)]
    [InlineData(MessageType.Call)]
    public void EvaluateEdit_RejectsNonEditableTypes(MessageType type)
    {
        Assert.Equal(MessageActionOutcome.NotEditable, Edit(type: type));
    }

    [Theory]
    [InlineData(MessageType.Text, true)]
    [InlineData(MessageType.Image, true)]
    [InlineData(MessageType.Video, true)]
    [InlineData(MessageType.File, true)]
    [InlineData(MessageType.System, false)]
    [InlineData(MessageType.Call, false)]
    public void IsEditableType_OnlyUserContent(MessageType type, bool expected) =>
        Assert.Equal(expected, MessageActionRules.IsEditableType(type));

    [Fact]
    public void EvaluateRecall_FollowsSameRulesAsEdit()
    {
        Assert.Equal(
            MessageActionOutcome.Allowed,
            MessageActionRules.EvaluateRecall(Current, Current, false, MessageType.Text, Now.AddHours(-2), Now));
        Assert.Equal(
            MessageActionOutcome.NotOwner,
            MessageActionRules.EvaluateRecall(Other, Current, false, MessageType.Text, Now, Now));
        Assert.Equal(
            MessageActionOutcome.AlreadyDeleted,
            MessageActionRules.EvaluateRecall(Current, Current, true, MessageType.Text, Now, Now));
        Assert.Equal(
            MessageActionOutcome.WindowExpired,
            MessageActionRules.EvaluateRecall(Current, Current, false, MessageType.Text, Now.AddDays(-2), Now));
    }

    [Fact]
    public void MaxPinnedMessages_IsTen() => Assert.Equal(10, MessageActionRules.MaxPinnedMessages);
}

public class ReactionCatalogTests
{
    [Fact]
    public void All_HasSixUniqueKeysAndEmojis()
    {
        Assert.Equal(6, ReactionCatalog.All.Count);
        Assert.Equal(ReactionCatalog.All.Count, ReactionCatalog.All.Select(item => item.Key).Distinct().Count());
        Assert.Equal(ReactionCatalog.All.Count, ReactionCatalog.All.Select(item => item.Emoji).Distinct().Count());
        Assert.All(ReactionCatalog.All, item => Assert.False(string.IsNullOrWhiteSpace(item.Emoji)));
    }

    [Theory]
    [InlineData("like", "👍")]
    [InlineData("love", "❤️")]
    [InlineData("haha", "😂")]
    [InlineData("wow", "😮")]
    [InlineData("sad", "😢")]
    [InlineData("angry", "😡")]
    public void TryGetEmoji_MapsKeys(string key, string expectedEmoji)
    {
        Assert.True(ReactionCatalog.TryGetEmoji(key, out var emoji));
        Assert.Equal(expectedEmoji, emoji);
    }

    [Theory]
    [InlineData("LIKE")]
    [InlineData(" Love ")]
    public void TryGetEmoji_IgnoresCaseAndWhitespace(string key) =>
        Assert.True(ReactionCatalog.TryGetEmoji(key, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("❤️")]
    [InlineData("thumbsup")]
    public void TryGetEmoji_RejectsUnknownKeys(string? key)
    {
        Assert.False(ReactionCatalog.IsValid(key));
        Assert.Null(ReactionCatalog.ToEmoji(key));
    }

    [Fact]
    public void Keys_MatchCatalogue() =>
        Assert.Equal(ReactionCatalog.All.Select(item => item.Key), ReactionCatalog.Keys);
}

public class MuteRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ResolveMutedUntil_UsesGivenNow()
    {
        Assert.Null(MuteRules.ResolveMutedUntil(MuteRules.Off, Now));
        Assert.Equal(Now.AddHours(1), MuteRules.ResolveMutedUntil(MuteRules.OneHour, Now));
        Assert.Equal(Now.AddHours(8), MuteRules.ResolveMutedUntil(MuteRules.EightHours, Now));
        Assert.Equal(Now.AddDays(7), MuteRules.ResolveMutedUntil(MuteRules.OneWeek, Now));
        Assert.Equal(MuteRules.ForeverUntil, MuteRules.ResolveMutedUntil(MuteRules.Forever, Now));
    }

    [Fact]
    public void ForeverUntil_IsFarFutureSentinel()
    {
        Assert.Equal(9999, MuteRules.ForeverUntil.Year);
        Assert.True(MuteRules.ForeverUntil > Now.AddYears(1000));
    }

    [Theory]
    [InlineData("Off", true)]
    [InlineData("OneHour", true)]
    [InlineData("EightHours", true)]
    [InlineData("OneWeek", true)]
    [InlineData("Forever", true)]
    [InlineData("off", false)]
    [InlineData("TwoHours", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidDuration_OnlyAllowsPresets(string? duration, bool expected) =>
        Assert.Equal(expected, MuteRules.IsValidDuration(duration));

    [Fact]
    public void IsMuted_OnlyWhileMutedUntilInFuture()
    {
        Assert.True(MuteRules.IsMuted(Now.AddMinutes(1), Now));
        Assert.True(MuteRules.IsMuted(MuteRules.ForeverUntil, Now));
        Assert.False(MuteRules.IsMuted(Now, Now));
        Assert.False(MuteRules.IsMuted(Now.AddMinutes(-1), Now));
        Assert.False(MuteRules.IsMuted(null, Now));
    }

    [Fact]
    public void IsForever_DetectsSentinel()
    {
        Assert.True(MuteRules.IsForever(MuteRules.ForeverUntil));
        Assert.False(MuteRules.IsForever(Now.AddDays(7)));
        Assert.False(MuteRules.IsForever(null));
    }
}
