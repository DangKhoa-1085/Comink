// Test đơn vị cho ánh xạ ngoại lệ → mã HTTP (không cần DB, không cần container).
// Đặt trong project này vì đây là project test duy nhất tham chiếu tới Comink.Api.
using Comink.Api.Common;
using Microsoft.AspNetCore.Http;

namespace Comink.Api.IntegrationTests;

public class ExceptionStatusMapperTests
{
    [Fact]
    public void BadHttpRequest_400_MapsTo400()
    {
        // Framework ném lỗi này khi body JSON không parse được (ví dụ gửi object vào chỗ chuỗi).
        var exception = new BadHttpRequestException("Failed to read parameter from the request body as JSON.", StatusCodes.Status400BadRequest);

        Assert.Equal(StatusCodes.Status400BadRequest, ExceptionStatusMapper.StatusCodeFor(exception));
    }

    [Fact]
    public void OtherException_MapsTo500()
    {
        Assert.Equal(StatusCodes.Status500InternalServerError, ExceptionStatusMapper.StatusCodeFor(new InvalidOperationException("boom")));
    }

    [Theory]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType)]
    public void BadHttpRequest_Other4xx_StillMapsTo500(int statusCode)
    {
        // Chủ ý: các mã 4xx khác chưa có mã lỗi riêng trong ErrorCodes nên giữ nguyên hành vi cũ (500).
        // Khi bổ sung mã lỗi tương ứng thì mở rộng mapper và đổi test này.
        var exception = new BadHttpRequestException("other", statusCode);

        Assert.Equal(StatusCodes.Status500InternalServerError, ExceptionStatusMapper.StatusCodeFor(exception));
    }
}
