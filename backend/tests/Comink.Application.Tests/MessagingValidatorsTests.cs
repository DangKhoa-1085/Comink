// Unit test cho validator cập nhật nhóm & thêm thành viên (không cần Docker).
using Comink.Application.Messaging.Dtos;
using Comink.Application.Messaging.Validators;
using Comink.Domain.Messaging;

namespace Comink.Application.Tests;

public class MessagingValidatorsTests
{
    [Theory]
    [InlineData("Nhóm mới", "https://res.cloudinary.com/demo/image/upload/v1/group.png", true)]
    [InlineData(null, "/uploads/avatar/group.png", true)]
    [InlineData(null, "", true)]
    [InlineData("   ", null, false)]
    [InlineData(null, "ftp://invalid.local/img.png", false)]
    public void UpdateGroupValidator_KiemTraTenVaAvatar(string? title, string? avatarUrl, bool expectedValid)
    {
        var validator = new UpdateGroupConversationRequestValidator();
        var result = validator.Validate(new UpdateGroupConversationRequest(title, avatarUrl));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void AddGroupMembersValidator_TuChoiDanhSachRongHoacQuaGioiHan()
    {
        var validator = new AddGroupMembersRequestValidator();

        Assert.False(validator.Validate(new AddGroupMembersRequest([])).IsValid);

        var tooMany = Enumerable.Range(0, ConversationRules.MaxGroupMembers + 1)
            .Select(_ => Guid.CreateVersion7())
            .ToList();
        Assert.False(validator.Validate(new AddGroupMembersRequest(tooMany)).IsValid);

        Assert.True(validator.Validate(new AddGroupMembersRequest([Guid.CreateVersion7()])).IsValid);
    }
}
