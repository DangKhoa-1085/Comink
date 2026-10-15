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
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>PropertyName của FluentValidation (PascalCase) → camelCase theo hợp đồng JSON.</summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
