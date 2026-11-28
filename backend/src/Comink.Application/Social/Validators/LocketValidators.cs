// FluentValidation cho các request của Comink Locket.
using Comink.Application.Social.Dtos;
using Comink.Domain.Social;
using FluentValidation;

namespace Comink.Application.Social.Validators;

/// <summary>Kiểm tra dữ liệu tạo Locket mới.</summary>
public class CreateLocketRequestValidator : AbstractValidator<CreateLocketRequest>
{
    public CreateLocketRequestValidator()
    {
        RuleFor(x => x.MediaAssetId)
            .NotEmpty();

        RuleFor(x => x.Caption)
            .MaximumLength(LocketRules.MaxCaptionLength);

        RuleFor(x => x.AudienceType)
            .Must(v => v is "AllFriends" or "SelectedFriends")
            .WithMessage("AudienceType must be AllFriends or SelectedFriends.");

        RuleFor(x => x.RecipientIds)
            .Must(ids => ids is not null && ids.Any(id => id != Guid.Empty))
            .When(x => x.AudienceType == "SelectedFriends")
            .WithMessage("RecipientIds is required when AudienceType is SelectedFriends.");
    }
}

/// <summary>Kiểm tra emoji khi thả cảm xúc trên Locket.</summary>
public class ReactLocketRequestValidator : AbstractValidator<ReactLocketRequest>
{
    public ReactLocketRequestValidator()
    {
        RuleFor(x => x.Emoji)
            .NotEmpty()
            .Must(LocketRules.IsValidReaction)
            .WithMessage("Invalid Locket reaction emoji.");
    }
}

/// <summary>Kiểm tra nội dung trả lời nhanh Locket vào chat.</summary>
public class ReplyLocketRequestValidator : AbstractValidator<ReplyLocketRequest>
{
    public ReplyLocketRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(LocketRules.MaxReplyLength);
    }
}
