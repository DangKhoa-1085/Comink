// Validator cho thiết bị nhận push.
using Comink.Application.Notifications.Dtos;
using FluentValidation;

namespace Comink.Application.Notifications.Validators;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    private static readonly string[] Platforms = ["Web", "Android", "iOS"];

    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.PushToken)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(4096);

        RuleFor(x => x.Platform)
            .Must(platform => Platforms.Contains(platform))
            .WithMessage("Platform phải là Web, Android hoặc iOS.");

        RuleFor(x => x.DeviceName)
            .MaximumLength(100);
    }
}
