// Validator cho yêu cầu quản trị (Giai đoạn 5).
using Comink.Application.Admin.Dtos;
using Comink.Domain.Admin;
using FluentValidation;

namespace Comink.Application.Admin.Validators;

/// <summary>Kiểm tra yêu cầu khóa tài khoản: lý do tùy chọn, tối đa 500 ký tự (khớp cột `locked_reason`).</summary>
public class LockUserRequestValidator : AbstractValidator<LockUserRequest>
{
    public LockUserRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(AdminUserRules.MaxLockReasonLength)
            .WithMessage($"Lý do khóa tối đa {AdminUserRules.MaxLockReasonLength} ký tự.");
    }
}
