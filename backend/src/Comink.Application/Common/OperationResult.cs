// Kết quả thao tác dùng chung cho tầng Application.
// Thay cho việc ném exception cho các lỗi nghiệp vụ đã lường trước
// (sai mật khẩu, trùng email...); lỗi bất ngờ vẫn để exception bay lên middleware.
namespace Comink.Application.Common;

/// <summary>
/// Kết quả của một thao tác: thành công kèm dữ liệu, hoặc thất bại kèm mã lỗi.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu trả về khi thành công.</typeparam>
public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, string? errorCode, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors;
    }

    /// <summary>Thao tác có thành công không.</summary>
    public bool Succeeded { get; }

    /// <summary>Dữ liệu trả về; chỉ có giá trị khi <see cref="Succeeded"/> là true.</summary>
    public T? Value { get; }

    /// <summary>Mã lỗi (xem <see cref="ErrorCodes"/>); null khi thành công.</summary>
    public string? ErrorCode { get; }

    /// <summary>Chi tiết lỗi validate theo từng trường; chỉ có khi mã lỗi là VALIDATION_FAILED.</summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public static OperationResult<T> Success(T value) => new(true, value, null, null);

    public static OperationResult<T> Failure(string errorCode) => new(false, default, errorCode, null);

    public static OperationResult<T> ValidationFailure(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, default, ErrorCodes.ValidationFailed, errors);
}
