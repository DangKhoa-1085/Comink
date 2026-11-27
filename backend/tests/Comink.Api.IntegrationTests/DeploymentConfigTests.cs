// Kiểm thử cấu hình triển khai (chuỗi kết nối URI Neon/Railway, CORS nhiều origin, seed Production).
using Comink.Api.Common;
using Comink.Infrastructure;
using Comink.Infrastructure.Calls;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Comink.Api.IntegrationTests;

/// <summary>Unit test cho các bộ phân tích cấu hình deploy (không phụ thuộc container).</summary>
public class DeploymentConfigUnitTests
{
    [Fact]
    public void NormalizePostgres_ChuyenDoiUriNeonSangChuoiNpgsqlDayDu()
    {
        const string neonUri =
            "postgresql://neondb_owner:p%40ss%21123@ep-cool-rain-123.ap-southeast-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require";

        var normalized = ConnectionStringNormalizer.NormalizePostgres(neonUri);

        Assert.NotNull(normalized);
        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("ep-cool-rain-123.ap-southeast-1.aws.neon.tech", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal("neondb_owner", builder.Username);
        Assert.Equal("p@ss!123", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
    }

    [Fact]
    public void NormalizePostgres_HoTroSchemePostgresVaCongTuyChinh()
    {
        const string uri = "postgres://comink:secret@db.internal:5433/comink_prod?sslmode=verify-full";

        var normalized = ConnectionStringNormalizer.NormalizePostgres(uri);

        var builder = new NpgsqlConnectionStringBuilder(normalized);
        Assert.Equal("db.internal", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("comink_prod", builder.Database);
        Assert.Equal("comink", builder.Username);
        Assert.Equal("secret", builder.Password);
        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Host=localhost;Port=5432;Database=comink;Username=comink;Password=devpassword")]
    public void NormalizePostgres_GiuNguyenChuoiDevHoacRong(string? input)
    {
        var result = ConnectionStringNormalizer.NormalizePostgres(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ParseRedis_ChuyenDoiUriRailwayMacDinhTatAbortOnConnectFail()
    {
        const string railwayUri = "redis://default:my%40Secret%21@redis.railway.internal:6379";

        var options = ConnectionStringNormalizer.ParseRedis(railwayUri);

        Assert.Single(options.EndPoints);
        var endpoint = Assert.IsType<System.Net.DnsEndPoint>(options.EndPoints[0]);
        Assert.Equal("redis.railway.internal", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Equal("default", options.User);
        Assert.Equal("my@Secret!", options.Password);
        Assert.False(options.Ssl);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseRedis_HoTroRedissTlsKhongUserVaChonDatabase()
    {
        const string tlsUri = "rediss://:onlypass@cache.example.com:6380/2";

        var options = ConnectionStringNormalizer.ParseRedis(tlsUri);

        var endpoint = Assert.IsType<System.Net.DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("cache.example.com", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.Null(options.User);
        Assert.Equal("onlypass", options.Password);
        Assert.True(options.Ssl);
        Assert.Equal(2, options.DefaultDatabase);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseRedis_ChuoiKeyValueDevMacDinhTatAbortOnConnectFail()
    {
        var options = ConnectionStringNormalizer.ParseRedis("localhost:6379,password=devredispass");

        Assert.Equal("devredispass", options.Password);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseRedis_TonTrongAbortConnectTrueNeuKhaiBaoTuongMinh()
    {
        var options = ConnectionStringNormalizer.ParseRedis("localhost:6379,password=devredispass,abortConnect=true");

        Assert.True(options.AbortOnConnectFail);
    }

    [Theory]
    [InlineData(null, new[] { "http://localhost:5173" })]
    [InlineData("   ", new[] { "http://localhost:5173" })]
    [InlineData("https://comink.vercel.app/", new[] { "https://comink.vercel.app" })]
    [InlineData(
        " https://comink.vercel.app/ , https://preview.comink.vercel.app ; HTTPS://COMINK.VERCEL.APP ",
        new[] { "https://comink.vercel.app", "https://preview.comink.vercel.app" })]
    public void CorsOriginsParser_ChuanHoaDanhSachOrigin(string? raw, string[] expected)
    {
        var actual = CorsOriginsParser.Parse(raw);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LiveKitOptions_GiuNguyenLocalDevVaLanMobile()
    {
        var defaultDev = new LiveKitOptions();
        Assert.Equal("http://localhost:7880", defaultDev.ServerUrl);
        Assert.Equal("ws://localhost:7880", defaultDev.ClientUrl);

        var lanMobileDev = new LiveKitOptions
        {
            Host = "http://localhost:7880",
            PublicUrl = "ws://192.168.1.3:7880"
        };
        Assert.Equal("http://localhost:7880", lanMobileDev.ServerUrl);
        Assert.Equal("ws://192.168.1.3:7880", lanMobileDev.ClientUrl);
    }

    [Fact]
    public void LiveKitOptions_ChuanHoaWssVaHttpsChoLiveKitCloud()
    {
        var cloudFromWssHost = new LiveKitOptions
        {
            Host = " wss://demo.livekit.cloud/ ",
            PublicUrl = string.Empty
        };
        Assert.Equal("https://demo.livekit.cloud", cloudFromWssHost.ServerUrl);
        Assert.Equal("wss://demo.livekit.cloud", cloudFromWssHost.ClientUrl);

        var cloudFromPublicUrlOnly = new LiveKitOptions
        {
            PublicUrl = " wss://demo.livekit.cloud/ "
        };
        Assert.Equal("https://demo.livekit.cloud", cloudFromPublicUrlOnly.ServerUrl);
        Assert.Equal("wss://demo.livekit.cloud", cloudFromPublicUrlOnly.ClientUrl);
    }

    [Fact]
    public void LiveKitOptions_CatKhoangTrangThuaChoApiKeyVaSecret()
    {
        var options = new LiveKitOptions
        {
            ApiKey = "  APIkey123  ",
            ApiSecret = "\nsecret456\t"
        };

        Assert.True(options.IsConfigured);
        Assert.Equal("APIkey123", options.TrimmedApiKey);
        Assert.Equal("secret456", options.TrimmedApiSecret);
    }
}

/// <summary>Integration test cho seed role/admin ở Production trên PostgreSQL thật.</summary>
public class SeedEssentialDataIntegrationTests : IntegrationTestBase
{
    public SeedEssentialDataIntegrationTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private static ServiceProvider BuildSeedServiceProvider(
        string connectionString,
        IDictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
        });
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedEssentialData_TaoRolesVaAdminIdempotentTrenDbMoiMigrate()
    {
        await using var db = await CreateDbContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        using var provider = BuildSeedServiceProvider(connectionString, new Dictionary<string, string?>
        {
            ["AdminSeed:Email"] = "admin@comink.test",
            ["AdminSeed:Password"] = "Admin@12345",
            ["AdminSeed:DisplayName"] = "Comink Admin"
        });

        await provider.SeedEssentialDataAsync();
        await provider.SeedEssentialDataAsync();

        using var verifyScope = provider.CreateScope();
        var roleManager = verifyScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));

        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
        var admin = Assert.Single(adminUsers);
        Assert.Equal("admin@comink.test", admin.Email);
        Assert.True(admin.EmailConfirmed);
    }

    [Fact]
    public async Task SeedEssentialData_ThieuMatKhauAdminVanTaoDuRoles()
    {
        await using var db = await CreateDbContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        using var provider = BuildSeedServiceProvider(connectionString, new Dictionary<string, string?>
        {
            ["AdminSeed:Email"] = "admin@comink.test"
        });

        await provider.SeedEssentialDataAsync();

        using var verifyScope = provider.CreateScope();
        var roleManager = verifyScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.Empty(await userManager.GetUsersInRoleAsync("Admin"));
    }
}
