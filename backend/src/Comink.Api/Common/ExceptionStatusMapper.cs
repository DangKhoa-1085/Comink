// Ánh xạ ngoại lệ chưa xử lý thành mã HTTP cho UseExceptionHandler.
using Microsoft.AspNetCore.Http;

namespace Comink.Api.Common;

/// <summary>
/// Quyết định mã HTTP khi có ngoại lệ không được xử lý ở endpoint.
/// <para>
/// Mặc định là <c>500</c>. Riêng lỗi đọc body do framework ném ra dưới dạng
/// <see cref="BadHttpRequestException"/> với <c>400</c> (JSON sai định dạng, sai kiểu dữ liệu,
/// thiếu trường bắt buộc…) là lỗi phía client, nên phải trả <c>400</c> để client hiển thị
/// "dữ liệu không hợp lệ" thay vì "máy chủ gặp lỗi" (docs/05_api_contract.md mục 5).
/// </para>
/// <para>
/// Chỉ map đúng 400. Các mã 4xx khác mà framework có thể ném (ví dụ 413 khi body quá lớn)
/// hiện vẫn trả 500 và chưa có mã lỗi riêng trong <c>ErrorCodes</c> — cần bổ sung ở giai đoạn
/// bảo mật/hardening, không tự đặt mã mới ở đây.
/// </para>
/// </summary>
public static class ExceptionStatusMapper
{
    /// <summary>Mã HTTP trả về cho ngoại lệ <paramref name="exception"/>.</summary>
    public static int StatusCodeFor(Exception exception) =>
        exception is BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest }
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
}
