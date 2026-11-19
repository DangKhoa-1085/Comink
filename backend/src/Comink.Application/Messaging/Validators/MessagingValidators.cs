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

/// <summary>Kiểm tra yêu cầu đánh dấu đã đọc (NC Giai đoạn 7).</summary>
public class MarkReadRequestValidator : AbstractValidator<MarkReadRequest>
{
    public MarkReadRequestValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty().WithMessage("MessageId là bắt buộc.");
    }
}

/// <summary>Kiểm tra yêu cầu chỉnh sửa tin (NC Giai đoạn 7 — Ưu tiên 2).</summary>
public class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator()
    {
        // Cho phép rỗng khi tin chỉ có tệp: service kiểm tra "phải còn nội dung hoặc tệp".
        RuleFor(x => x.Content)
            .MaximumLength(MessageRules.MaxContentLength)
            .WithMessage($"Nội dung tối đa {MessageRules.MaxContentLength} ký tự.");
    }
}

/// <summary>Kiểm tra hình thức yêu cầu thả cảm xúc; danh mục hợp lệ do service kiểm tra.</summary>
public class ReactionRequestValidator : AbstractValidator<ReactionRequest>
{
    public ReactionRequestValidator()
    {
        RuleFor(x => x.Reaction)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Reaction là bắt buộc.")
            .MaximumLength(16)
            .WithMessage("Reaction không hợp lệ.");
    }
}

/// <summary>Kiểm tra yêu cầu chuyển tiếp tin (NC Ưu tiên 2).</summary>
public class ForwardMessageRequestValidator : AbstractValidator<ForwardMessageRequest>
{
    public ForwardMessageRequestValidator()
    {
        RuleFor(x => x.SourceMessageId).NotEmpty().WithMessage("SourceMessageId là bắt buộc.");
        RuleFor(x => x.ClientMessageId)
            .NotEmpty()
            .WithMessage("ClientMessageId là bắt buộc (dùng để chống gửi trùng).");
    }
}

/// <summary>Kiểm tra yêu cầu tắt thông báo; danh sách thời hạn lấy từ domain.</summary>
public class MuteConversationRequestValidator : AbstractValidator<MuteConversationRequest>
{
    public MuteConversationRequestValidator()
    {
        RuleFor(x => x.Duration)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Duration là bắt buộc.")
            .Must(MuteRules.IsValidDuration)
            .WithMessage("Duration phải là Off, OneHour, EightHours, OneWeek hoặc Forever.");
    }
}
