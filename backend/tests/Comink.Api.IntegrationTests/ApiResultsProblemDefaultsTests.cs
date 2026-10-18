// Test đơn vị cho việc gắn `code` + `title` nhất quán vào ProblemDetails.
// Đặt trong project này vì đây là project test duy nhất tham chiếu tới Comink.Api.
using Comink.Api.Common;
using Comink.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Comink.Api.IntegrationTests;

public class ApiResultsProblemDefaultsTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed")]
    [InlineData(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Unauthorized")]
    [InlineData(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Forbidden")]
    [InlineData(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found")]
    [InlineData(StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "Unexpected error")]
    public void FrameworkProblem_GetsCodeAndTitleFromCode(int status, string expectedCode, string expectedTitle)
    {
        // ProblemDetails do framework sinh chỉ có status; tiêu đề mặc định của ASP.NET khác với
        // tiêu đề lỗi nghiệp vụ (ví dụ 400 → "Bad Request" vs "Validation failed").
        var problem = new ProblemDetails { Status = status, Title = "tiêu đề mặc định của framework" };

        ApiResults.ApplyProblemDefaults(problem);

        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.Equal(expectedTitle, problem.Title);
    }

    [Fact]
    public void ProblemWithExistingCode_IsLeftUntouched()
    {
        // Lỗi nghiệp vụ đã tự đặt code + title riêng → không được ghi đè.
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid credentials",
            Extensions = { ["code"] = ErrorCodes.AuthInvalidCredentials }
        };

        ApiResults.ApplyProblemDefaults(problem);

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, problem.Extensions["code"]);
        Assert.Equal("Invalid credentials", problem.Title);
    }

    [Fact]
    public void UnmappedStatus_GetsNoCodeAndKeepsTitle()
    {
        // 405 chưa có mã lỗi riêng trong ErrorCodes → không tự bịa mã, giữ nguyên tiêu đề.
        var problem = new ProblemDetails { Status = StatusCodes.Status405MethodNotAllowed, Title = "Method Not Allowed" };

        ApiResults.ApplyProblemDefaults(problem);

        Assert.False(problem.Extensions.ContainsKey("code"));
        Assert.Equal("Method Not Allowed", problem.Title);
    }
}
