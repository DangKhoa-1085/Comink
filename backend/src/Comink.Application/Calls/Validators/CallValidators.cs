// Validator cho yêu cầu cuộc gọi.
using Comink.Application.Calls.Dtos;
using Comink.Domain.Calls;
using FluentValidation;

namespace Comink.Application.Calls.Validators;

/// <summary>
/// Kiểm tra yêu cầu bắt đầu cuộc gọi: bắt buộc có hội thoại (1–1 hoặc nhóm), loại chỉ Audio/Video.
/// Quyết định D6: không còn nhánh gọi bằng danh sách người (`participantIds`).
/// </summary>
public class StartCallRequestValidator : AbstractValidator<StartCallRequest>
{
    public StartCallRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => type is "Audio" or "Video")
            .WithMessage("Loại cuộc gọi chỉ nhận 'Audio' hoặc 'Video'.");

        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Phải có ConversationId (cuộc gọi luôn bắt đầu trong một hội thoại).");
    }
}
