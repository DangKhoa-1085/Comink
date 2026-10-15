// Hạ tầng dùng chung cho test tích hợp: một PostgreSQL thật trong container,
// mỗi test có database riêng để không nhiễu lẫn nhau khi xUnit chạy song song.
using Comink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Comink.Api.IntegrationTests;

/// <summary>Khởi động một container PostgreSQL 17 dùng chung cho cả lớp test.</summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>
/// Lớp cơ sở cho test tích hợp: tạo một database mới (đã áp migration M0) cho mỗi test.
/// </summary>
[Collection("postgres")]
public abstract class IntegrationTestBase
{
    /// <summary>Fixture container dùng chung (protected để lớp con tự tạo database riêng).</summary>
    protected readonly PostgresFixture Fixture;

    private int _databaseCounter;

    protected IntegrationTestBase(PostgresFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Tạo DbContext trỏ tới một database riêng của test và áp migration M0.
    /// Mỗi lần gọi tạo thêm một database mới nên các test không nhìn thấy dữ liệu của nhau.
    /// </summary>
    protected async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var databaseName = $"comink_test_{Interlocked.Increment(ref _databaseCounter)}_{Guid.NewGuid():N}";
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(Fixture.ConnectionString)
        {
            Database = databaseName
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        var db = new ApplicationDbContext(options);

        // Database chưa tồn tại → EnsureCreated/Migrate sẽ tạo mới.
        await db.Database.MigrateAsync();
        return db;
    }
}
