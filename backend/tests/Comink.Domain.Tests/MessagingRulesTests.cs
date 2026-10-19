// Test đơn vị cho quy tắc nhắn tin (docs/05_api_contract.md mục 8, docs/03_database_design.md mục 3.3).
using Comink.Domain.Enums;
using Comink.Domain.Messaging;

namespace Comink.Domain.Tests;

public class MessagingRulesTests
{
    [Fact]
    public void BuildDirectKey_IsOrderIndependent()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        // Hai phía gọi theo thứ tự nào cũng phải ra cùng một khoá,
        // nếu không sẽ tạo ra hai hội thoại cho cùng một cặp người.
        Assert.Equal(ConversationRules.BuildDirectKey(a, b), ConversationRules.BuildDirectKey(b, a));
    }

    [Fact]
    public void BuildDirectKey_FitsColumnLengthAndUsesGuidFormat()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        var key = ConversationRules.BuildDirectKey(a, b);

        Assert.Equal(ConversationRules.DirectKeyMaxLength, key.Length);
        Assert.Contains('_', key);
        Assert.StartsWith(key[..36], key);
    }

    [Fact]
    public void BuildDirectKey_OrdersByIdStringNotArgumentOrder()
    {
        var small = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var large = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        Assert.Equal($"{small:D}_{large:D}", ConversationRules.BuildDirectKey(large, small));
    }

    [Fact]
    public void IsSelfConversation_OnlyTrueForSameUser()
    {
        var user = Guid.CreateVersion7();

        Assert.True(ConversationRules.IsSelfConversation(user, user));
        Assert.False(ConversationRules.IsSelfConversation(user, Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData("   ", 0, false)]
    [InlineData("xin chào", 0, true)]
    [InlineData(null, 1, true)]
    [InlineData("kèm ảnh", 3, true)]
    public void HasPayload_RequiresContentOrAttachment(string? content, int attachments, bool expected) =>
        Assert.Equal(expected, MessageRules.HasPayload(content, attachments));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("1234567890", true)]
    public void IsContentLengthValid_AcceptsWithinLimit(string? content, bool expected) =>
        Assert.Equal(expected, MessageRules.IsContentLengthValid(content));

    [Fact]
    public void IsContentLengthValid_RejectsOverLimit()
    {
        var tooLong = new string('a', MessageRules.MaxContentLength + 1);

        Assert.False(MessageRules.IsContentLengthValid(tooLong));
        Assert.True(MessageRules.IsContentLengthValid(new string('a', MessageRules.MaxContentLength)));
    }

    [Theory]
    [InlineData(0, MessageType.Text)]
    [InlineData(1, MessageType.Image)]
    [InlineData(5, MessageType.Image)]
    public void ResolveType_DependsOnAttachments(int attachments, MessageType expected) =>
        Assert.Equal(expected, MessageRules.ResolveType(attachments));

    [Fact]
    public void MaxAttachments_IsTen() => Assert.Equal(10, MessageRules.MaxAttachments);
}
