// FluentValidation cho các yêu cầu của nhóm Xác thực.
// Ràng buộc lấy từ docs/05_api_contract.md mục 2.
using Comink.Application.Identity.Dtos;
using FluentValidation;

namespace Comink.Application.Identity.Validators;

/// <summary>Kiểm tra dữ liệu đầu vào khi đăng ký.</summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    // Khớp cấu hình Identity ở Comink.Infrastructure/DependencyInjection.cs (mục AllowedUserNameCharacters).
    private const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-@";

    public RegisterRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Tên đăng nhập là bắt buộc.")
            .Length(3, 32).WithMessage("Tên đăng nhập phải từ 3 đến 32 ký tự.")
            .Must(value => value.All(c => AllowedUserNameCharacters.Contains(c)))
                .WithMessage("Tên đăng nhập chỉ được chứa chữ cái, chữ số và các ký tự . _ - @");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Email không hợp lệ.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Tên hiển thị là bắt buộc.")
            .MaximumLength(64).WithMessage("Tên hiển thị tối đa 64 ký tự.");

        // Chính sách mật khẩu trùng với cấu hình Identity (8 ký tự, đủ 4 nhóm ký tự).
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất một chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất một chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất một chữ số.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Mật khẩu phải có ít nhất một ký tự đặc biệt.");
    }
}

/// <summary>Kiểm tra dữ liệu đầu vào khi đăng nhập.</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.EmailOrUserName)
            .NotEmpty().WithMessage("Email hoặc tên đăng nhập là bắt buộc.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc.");

        RuleFor(x => x.DeviceName)
            .MaximumLength(100);
    }
}

/// <summary>Kiểm tra dữ liệu đầu vào khi làm mới / thu hồi token.</summary>
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token là bắt buộc.");
    }
}
