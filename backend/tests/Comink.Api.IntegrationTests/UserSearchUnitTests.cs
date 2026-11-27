// Unit test chuẩn hoá từ khoá `@username`, bỏ dấu và con trỏ hạng tìm kiếm (không cần Docker).
using Comink.Infrastructure.Social;

namespace Comink.Api.IntegrationTests;

public class UserSearchUnitTests
{
    [Theory]
    [InlineData("KhoaTester", "khoatester")]
    [InlineData("@KhoaTester", "khoatester")]
    [InlineData("  @@Khoa_Tester  ", "khoa_tester")]
    [InlineData("   @   ", "")]
    [InlineData("user@domain", "user@domain")]
    public void NormalizeKeyword_BoKyTuAtODauVaChuanHoaChuThuong(string input, string expected)
    {
        var actual = UserSearchHelper.NormalizeKeyword(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Khoa Nguyễn", "khoa nguyen")]
    [InlineData("Đặng Khoa", "dang khoa")]
    [InlineData("Minh Trần", "minh tran")]
    public void RemoveVietnameseDiacritics_BoDauTiengViet(string input, string expected)
    {
        var actual = UserSearchHelper.RemoveVietnameseDiacritics(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EscapeLike_EscapeKyTuDaiDienSql()
    {
        var escaped = UserSearchHelper.EscapeLike(@"a_b%c\d");
        Assert.Equal(@"a\_b\%c\\d", escaped);
    }

    [Fact]
    public void Cursor_EncodeVaDecodeGiuNguyenHangKhoiTao()
    {
        var createdAt = new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
        var id = Guid.CreateVersion7();

        var encoded = UserSearchHelper.EncodeCursor(2, createdAt, id);
        var decoded = UserSearchHelper.TryDecodeCursor(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(2, decoded.Value.MatchRank);
        Assert.Equal(createdAt, decoded.Value.CreatedAt);
        Assert.Equal(id, decoded.Value.Id);
        Assert.Null(UserSearchHelper.TryDecodeCursor("invalid-cursor"));
    }
}
