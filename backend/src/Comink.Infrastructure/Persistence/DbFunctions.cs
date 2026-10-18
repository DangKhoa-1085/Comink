// Ánh xạ các hàm PostgreSQL do migration tạo để dùng trong truy vấn LINQ.
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Persistence;

/// <summary>
/// Các hàm SQL tuỳ biến cần gọi từ LINQ. Hàm thật được tạo ở migration M0
/// (docs/03_database_design.md mục 5.1).
/// </summary>
public static class CominkDbFunctions
{
    /// <summary>
    /// Bản <c>IMMUTABLE</c> của <c>unaccent</c> — dùng cho tìm kiếm tiếng Việt không dấu.
    /// Chỉ gọi được trong truy vấn LINQ to Entities.
    /// </summary>
    [DbFunction("immutable_unaccent", IsBuiltIn = true)]
    public static string? ImmutableUnaccent(string? value) =>
        throw new NotSupportedException("Chỉ dùng trong truy vấn LINQ to Entities.");
}
