// Validator cho yêu cầu cuộc gọi.
using Comink.Application.Calls.Dtos;
using Comink.Domain.Calls;
using FluentValidation;

namespace Comink.Application.Calls.Validators;

/// <summary>
/// Kiểm tra yêu cầu bắt đầu cuộc gọi: phải là 1–1 (có hội thoại) **hoặc** nhóm (có danh sách người),
/// không được gửi cả hai; loại cuộc gọi chỉ Audio/Video.
/// </summary>
public class StartCallRequestValidator : AbstractValidator<StartCallRequest>
{
    public StartCallRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => type is "Audio" or "Video")
            .WithMessage("Loại cuộc gọi chỉ nhận 'Audio' hoặc 'Video'.");

        RuleFor(x => x.ConversationId)
            .Must((request, conversationId) => conversationId is not null || request.ParticipantIds is not null)
            .WithMessage("Phải có ConversationId (gọi 1–1) hoặc ParticipantIds (gọi nhóm).");

        RuleFor(x => x.ConversationId)
            .Null()
            .When(x => x.ParticipantIds is not null)
            .WithMessage("Không gửi đồng thời ConversationId và ParticipantIds.");

        RuleFor(x => x.ParticipantIds)
            .Must(ids => ids is null || CallRules.AreParticipantsValid(ids))
            .WithMessage($"Cuộc gọi nhóm cần từ 2 đến {CallRules.MaxParticipants} người, không trùng nhau.");
    }
}
