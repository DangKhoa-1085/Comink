// Chuyển kết quả nghiệp vụ (OperationResult) và lỗi validate thành HTTP + ProblemDetails
// theo đúng hợp đồng ở docs/05_api_contract.md mục 1.1 và mục 5.
using Comink.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Comink.Api.Common;

/// <summary>Tiện ích trả lỗi thống nhất cho mọi endpoint Minimal API.</summary>
public static class ApiResults
{
    /// <summary>
    /// Chạy validator của FluentValidation; trả về lỗi 400 nếu dữ liệu không hợp lệ.
    /// </summary>
    public static async Task<(bool IsValid, IResult? ErrorResult, IReadOnlyDictionary<string, string[]> Errors)>
        ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return (true, null, new Dictionary<string, string[]>());
        }

        var errors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return (false, ValidationProblem(errors), errors);
    }

    /// <summary>Trả ProblemDetails 400 kèm chi tiết theo từng trường.</summary>
    public static IResult ValidationProblem(IReadOnlyDictionary<string, string[]> errors) =>
        Results.Problem(
            title: "Validation failed",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = ErrorCodes.ValidationFailed,
                ["errors"] = errors
            });

    /// <summary>Trả ProblemDetails theo mã lỗi nghiệp vụ.</summary>
    public static IResult Problem(string errorCode) =>
        Results.Problem(
            title: TitleFor(errorCode),
            statusCode: StatusFor(errorCode),
            extensions: new Dictionary<string, object?> { ["code"] = errorCode });

    /// <summary>
    /// Chuyển <see cref="OperationResult{T}"/> thành HTTP: thành công thì 200/201,
    /// thất bại thì ProblemDetails theo mã lỗi.
    /// </summary>
    public static IResult FromResult<T>(
        OperationResult<T> result,
        Func<T, IResult> onSuccess)
    {
        return result.Succeeded && result.Value is not null
            ? onSuccess(result.Value)
            : result.ErrorCode == ErrorCodes.ValidationFailed && result.ValidationErrors is not null
                ? ValidationProblem(result.ValidationErrors)
                : Problem(result.ErrorCode ?? ErrorCodes.InternalError);
    }

    /// <summary>
    /// Gắn `code` và `title` cho ProblemDetails do framework sinh ra (401/403/404/405…, hoặc 400
    /// khi body JSON không đọc được) để response **nhất quán** với lỗi nghiệp vụ
    /// (docs/05_api_contract.md mục 5). ProblemDetails đã có `code` thì giữ nguyên.
    /// <para>
    /// `code` là nguồn duy nhất: `title` được suy ra từ `code` bằng <see cref="TitleFor"/>, nên
    /// mọi lỗi cùng mã đều có cùng tiêu đề — trước đây lỗi 400 của framework có title
    /// "Bad Request" còn lỗi validate có "Validation failed".
    /// </para>
    /// </summary>
    public static void ApplyProblemDefaults(ProblemDetails problemDetails)
    {
        if (problemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        var code = problemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
            StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.NotFound,
            StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
            _ => null
        };

        if (code is null)
        {
            return;
        }

        problemDetails.Extensions["code"] = code;
        problemDetails.Title = TitleFor(code);
    }

    /// <summary>Tiêu đề tiếng Anh cho ProblemDetails (client không hiển thị trực tiếp).</summary>
    private static string TitleFor(string errorCode) => errorCode switch
    {
        ErrorCodes.ValidationFailed => "Validation failed",
        ErrorCodes.Unauthorized => "Unauthorized",
        ErrorCodes.Forbidden => "Forbidden",
        ErrorCodes.NotFound => "Not found",
        ErrorCodes.AuthInvalidCredentials => "Invalid credentials",
        ErrorCodes.AuthAccountLocked => "Account locked",
        ErrorCodes.AuthRefreshTokenInvalid => "Invalid refresh token",
        ErrorCodes.AuthRefreshTokenReused => "Refresh token reuse detected",
        ErrorCodes.AuthEmailAlreadyExists => "Email already exists",
        ErrorCodes.AuthUserNameAlreadyExists => "Username already exists",
        ErrorCodes.SocialCannotFriendSelf => "Cannot befriend yourself",
        ErrorCodes.SocialCannotFollowSelf => "Cannot follow yourself",
        ErrorCodes.SocialAlreadyFriends => "Already friends",
        ErrorCodes.SocialFriendRequestNotFound => "Friend request not found",
        ErrorCodes.SocialNotRequestAddressee => "Not the addressee of this request",
        ErrorCodes.PostNotFound => "Post not found",
        ErrorCodes.PostContentRequired => "Post content required",
        ErrorCodes.PostTooManyMedia => "Too many media items",
        ErrorCodes.PostMediaNotOwned => "Media does not belong to the author",
        ErrorCodes.CommentNotFound => "Comment not found",
        ErrorCodes.CommentReplyDepthExceeded => "Replies are limited to one level",
        ErrorCodes.MediaUnsupportedType => "Unsupported media type",
        ErrorCodes.MediaTooLarge => "Media is too large",
        ErrorCodes.MediaNotFound => "Media not found",
        ErrorCodes.MediaNotOwned => "Media does not belong to the sender",
        ErrorCodes.ConversationNotFound => "Conversation not found",
        ErrorCodes.MessagingCannotMessageSelf => "Cannot message yourself",
        ErrorCodes.MessagingNotFriends => "You are not friends yet",
        ErrorCodes.MessageContentRequired => "Message content required",
        ErrorCodes.MessageTooManyAttachments => "Too many attachments",
        ErrorCodes.MessageClientIdRequired => "Client message id required",
        ErrorCodes.MessageInvalidCursor => "Invalid cursor",
        ErrorCodes.CallNotFound => "Call not found",
        ErrorCodes.CallAlreadyActive => "Call already active",
        ErrorCodes.CallAlreadyEnded => "Call already ended",
        ErrorCodes.CallNotRinging => "Call is not ringing",
        ErrorCodes.CallProviderUnavailable => "Call provider unavailable",
        _ => "Unexpected error"
    };

    /// <summary>Mã HTTP tương ứng với mã lỗi (docs/05_api_contract.md mục 5).</summary>
    private static int StatusFor(string errorCode) => errorCode switch
    {
        ErrorCodes.ValidationFailed => StatusCodes.Status400BadRequest,
        ErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthInvalidCredentials => StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthRefreshTokenInvalid => StatusCodes.Status401Unauthorized,
        ErrorCodes.AuthRefreshTokenReused => StatusCodes.Status401Unauthorized,
        ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.AuthAccountLocked => StatusCodes.Status403Forbidden,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.AuthEmailAlreadyExists => StatusCodes.Status409Conflict,
        ErrorCodes.AuthUserNameAlreadyExists => StatusCodes.Status409Conflict,
        ErrorCodes.SocialCannotFriendSelf => StatusCodes.Status400BadRequest,
        ErrorCodes.SocialCannotFollowSelf => StatusCodes.Status400BadRequest,
        ErrorCodes.SocialAlreadyFriends => StatusCodes.Status409Conflict,
        ErrorCodes.SocialFriendRequestNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.SocialNotRequestAddressee => StatusCodes.Status403Forbidden,
        ErrorCodes.PostNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.PostContentRequired => StatusCodes.Status400BadRequest,
        ErrorCodes.PostTooManyMedia => StatusCodes.Status400BadRequest,
        ErrorCodes.PostMediaNotOwned => StatusCodes.Status400BadRequest,
        ErrorCodes.CommentNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.CommentReplyDepthExceeded => StatusCodes.Status400BadRequest,
        ErrorCodes.MediaUnsupportedType => StatusCodes.Status400BadRequest,
        ErrorCodes.MediaTooLarge => StatusCodes.Status400BadRequest,
        ErrorCodes.MediaNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MediaNotOwned => StatusCodes.Status400BadRequest,
        ErrorCodes.ConversationNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MessagingCannotMessageSelf => StatusCodes.Status400BadRequest,
        ErrorCodes.MessagingNotFriends => StatusCodes.Status403Forbidden,
        ErrorCodes.MessageContentRequired => StatusCodes.Status400BadRequest,
        ErrorCodes.MessageTooManyAttachments => StatusCodes.Status400BadRequest,
        ErrorCodes.MessageClientIdRequired => StatusCodes.Status400BadRequest,
        ErrorCodes.MessageInvalidCursor => StatusCodes.Status400BadRequest,
        ErrorCodes.CallNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.CallAlreadyActive => StatusCodes.Status409Conflict,
        ErrorCodes.CallAlreadyEnded => StatusCodes.Status409Conflict,
        ErrorCodes.CallNotRinging => StatusCodes.Status409Conflict,
        ErrorCodes.CallProviderUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>PropertyName của FluentValidation (PascalCase) → camelCase theo hợp đồng JSON.</summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
