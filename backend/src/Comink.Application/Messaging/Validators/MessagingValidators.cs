// Validator cho request của module Nhắn tin. Kiểm tra hình thức; quy tắc nghiệp vụ nằm ở service.
using Comink.Application.Messaging.Dtos;
using Comink.Domain.Messaging;
using FluentValidation;

namespace Comink.Application.Messaging.Validators;

/// <summary>Kiểm tra yêu cầu mở hội thoại 1–1.</summary>
public class CreateDirectConversationRequestValidator : AbstractValidator<CreateDirectConversationRequest>
{
    public CreateDirectConversationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId là bắt buộc.");
    }
}

/// <summary>Kiểm tra yêu cầu tạo nhóm chat (tên + số người mời). Quy tắc nghiệp vụ ở service.</summary>
public class CreateGroupConversationRequestValidator : AbstractValidator<CreateGroupConversationRequest>
{
    public CreateGroupConversationRequestValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Tên nhóm là bắt buộc.")
            .MaximumLength(ConversationRules.MaxGroupTitleLength)
            .WithMessage($"Tên nhóm tối đa {ConversationRules.MaxGroupTitleLength} ký tự.");

        RuleFor(x => x.ParticipantIds)
            .NotEmpty()
            .WithMessage($"Nhóm cần ít nhất {ConversationRules.MinGroupMembers - 1} người được mời.");
    }
}

/// <summary>Kiểm tra yêu cầu gửi tin nhắn (giới hạn độ dài và số tệp).</summary>
public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.ClientMessageId)
            .NotEmpty()
            .WithMessage("ClientMessageId là bắt buộc (dùng để chống gửi trùng).");

        RuleFor(x => x.Content)
            .MaximumLength(MessageRules.MaxContentLength)
            .WithMessage($"Nội dung tối đa {MessageRules.MaxContentLength} ký tự.");

        RuleFor(x => x.MediaIds)
            .Must(ids => ids is null || ids.Count <= MessageRules.MaxAttachments)
            .WithMessage($"Mỗi tin tối đa {MessageRules.MaxAttachments} tệp.");
    }
}
