// Validator cho các request của nhóm mạng xã hội (Giai đoạn 2).
using Comink.Application.Social.Dtos;
using FluentValidation;

namespace Comink.Application.Social.Validators;

/// <summary>Kiểm tra request gửi lời mời kết bạn.</summary>
public class SendFriendRequestValidator : AbstractValidator<SendFriendRequestRequest>
{
    public SendFriendRequestValidator()
    {
        RuleFor(x => x.AddresseeId)
            .NotEmpty().WithMessage("Thiếu id người nhận lời mời.");
    }
}

/// <summary>Kiểm tra tham số tìm kiếm người dùng.</summary>
public class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    /// <summary>Độ dài tối thiểu của từ khóa (tránh quét toàn bảng).</summary>
    public const int MinSearchLength = 1;

    /// <summary>Độ dài tối đa của từ khóa.</summary>
    public const int MaxSearchLength = 100;

    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.Search)
            .NotEmpty().WithMessage("Vui lòng nhập từ khóa tìm kiếm.")
            .MinimumLength(MinSearchLength)
            .MaximumLength(MaxSearchLength).WithMessage("Từ khóa tối đa 100 ký tự.");
    }
}

/// <summary>Tham số truy vấn tìm kiếm người dùng (gắn từ query string).</summary>
public record SearchUsersQuery(string Search);
