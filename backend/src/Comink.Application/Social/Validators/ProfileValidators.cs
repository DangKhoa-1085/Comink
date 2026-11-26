// FluentValidation cho nhóm Hồ sơ (docs/05_api_contract.md mục 3).
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Application.Social.Validators;

/// <summary>Kiểm tra dữ liệu đầu vào khi sửa hồ sơ.</summary>
public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(64).WithMessage("Tên hiển thị tối đa 64 ký tự.")
            // Chuỗi rỗng hoặc chỉ khoảng trắng không được chấp nhận nếu có gửi lên.
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
                .WithMessage("Tên hiển thị không được để trống.");

        RuleFor(x => x.Bio)
            .MaximumLength(300).WithMessage("Tiểu sử tối đa 300 ký tự.");

        RuleFor(x => x.Location)
            .MaximumLength(100).WithMessage("Địa điểm tối đa 100 ký tự.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Ngày sinh không thể ở tương lai.")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh đại diện tối đa 500 ký tự.")
            .Must(IsValidMediaUrl).WithMessage("Đường dẫn ảnh đại diện không hợp lệ.");

        RuleFor(x => x.CoverUrl)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh bìa tối đa 500 ký tự.")
            .Must(IsValidMediaUrl).WithMessage("Đường dẫn ảnh bìa không hợp lệ.");
    }

    private static bool IsValidMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>Kiểm tra dữ liệu đầu vào khi sửa cài đặt.</summary>
public class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Language)
            .Must(value => value is null || ProfileService.SupportedLanguages.Contains(value))
                .WithMessage("Ngôn ngữ chỉ nhận giá trị 'vi' hoặc 'en'.");
    }
}
