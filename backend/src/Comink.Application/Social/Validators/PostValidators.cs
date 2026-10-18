// Validator cho bài viết và bình luận (Giai đoạn 2).
using Comink.Application.Social.Dtos;
using Comink.Domain.Social;
using FluentValidation;

namespace Comink.Application.Social.Validators;

/// <summary>Kiểm tra yêu cầu tạo bài viết.</summary>
public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .MaximumLength(PostLimits.MaxContentLength)
            .WithMessage($"Nội dung bài viết tối đa {PostLimits.MaxContentLength} ký tự.");

        RuleFor(x => x.Visibility)
            .Must(value => value is null || PostLimits.VisibilityValues.Contains(value))
            .WithMessage("Phạm vi hiển thị chỉ nhận Public, Friends hoặc OnlyMe.");

        RuleFor(x => x.MediaIds)
            .Must(ids => ids is null || ids.Count <= PostLimits.MaxMediaPerPost)
            .WithMessage($"Mỗi bài tối đa {PostLimits.MaxMediaPerPost} ảnh.");
    }
}

/// <summary>Kiểm tra yêu cầu sửa bài viết.</summary>
public class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
    public UpdatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .MaximumLength(PostLimits.MaxContentLength)
            .WithMessage($"Nội dung bài viết tối đa {PostLimits.MaxContentLength} ký tự.");

        RuleFor(x => x.Visibility)
            .Must(value => value is null || PostLimits.VisibilityValues.Contains(value))
            .WithMessage("Phạm vi hiển thị chỉ nhận Public, Friends hoặc OnlyMe.");
    }
}

/// <summary>Kiểm tra yêu cầu tạo bình luận.</summary>
public class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung bình luận không được để trống.")
            .MaximumLength(CommentRules.MaxLength)
            .WithMessage($"Bình luận tối đa {CommentRules.MaxLength} ký tự.");
    }
}

/// <summary>Kiểm tra yêu cầu sửa bình luận.</summary>
public class UpdateCommentRequestValidator : AbstractValidator<UpdateCommentRequest>
{
    public UpdateCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung bình luận không được để trống.")
            .MaximumLength(CommentRules.MaxLength)
            .WithMessage($"Bình luận tối đa {CommentRules.MaxLength} ký tự.");
    }
}
