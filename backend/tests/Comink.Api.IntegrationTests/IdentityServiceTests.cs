// Test tích hợp cho IdentityService thật (ASP.NET Core Identity + PostgreSQL thật).
// UserManager được lấy từ DI container giống cách ứng dụng đăng ký,
// nên test dùng đúng cấu hình thật (chính sách mật khẩu, chuẩn hóa email...).
using Comink.Application.Identity.Abstractions;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Comink.Api.IntegrationTests;

public class IdentityServiceTests : IntegrationTestBase
{
    public IdentityServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// Dựng DI container thật cho một database riêng của test rồi trả về IdentityService
    /// cùng DbContext đang dùng.
    /// </summary>
    private async Task<(IdentityService Service, ApplicationDbContext Db, ServiceProvider Provider)>
        CreateIdentityServiceAsync()
    {
        var databaseName = $"comink_identity_{Guid.NewGuid():N}";
        var adminConnection = new NpgsqlConnectionStringBuilder(Fixture.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;

        // Tạo database mới trên container dùng chung.
        await using (var bootstrap = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await bootstrap.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", bootstrap);
            await create.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(adminConnection).UseSnakeCaseNamingConvention());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-@";
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        // Không gọi AddDefaultTokenProviders: test không dùng token xác minh email/đặt lại mật khẩu,
        // và provider đó cần IDataProtectionProvider (chỉ có trong host đầy đủ của ứng dụng).

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Ứng dụng thật seed role lúc khởi động (MigrationExtensions); test phải làm tương tự
        // vì IdentityService gán role "User" mặc định cho tài khoản mới.
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var roleName in new[] { "User", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        var service = new IdentityService(
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            db,
            TimeProvider.System);

        return (service, db, provider);
    }

    [Fact]
    public async Task CreateUser_trung_email_tra_DuplicateEmail_khong_nem_loi_500()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        var first = await sut.CreateUserAsync("nguoidung1", "trung@example.com", "MatKhau@123", "Người dùng 1");
        Assert.True(first.Succeeded);

        // Cùng email, khác tên đăng nhập. Có hai đường dẫn phải xử lý:
        // Identity tự phát hiện, hoặc PostgreSQL ném lỗi 23505 và ta phải bắt lại.
        var second = await sut.CreateUserAsync("nguoidung2", "trung@example.com", "MatKhau@123", "Người dùng 2");

        Assert.False(second.Succeeded);
        Assert.Equal(IdentityFailure.DuplicateEmail, second.Error);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_trung_ten_dang_nhap_tra_DuplicateUserName()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        var first = await sut.CreateUserAsync("trungten", "a@example.com", "MatKhau@123", "Người dùng A");
        Assert.True(first.Succeeded);

        var second = await sut.CreateUserAsync("trungten", "b@example.com", "MatKhau@123", "Người dùng B");

        Assert.False(second.Succeeded);
        Assert.Equal(IdentityFailure.DuplicateUserName, second.Error);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_tao_kem_ho_so_va_cai_dat_mac_dinh()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        var created = await sut.CreateUserAsync("hoso", "hoso@example.com", "MatKhau@123", "Tên Hiển Thị");
        Assert.True(created.Succeeded);

        var user = await db.Users
            .Include(u => u.Profile)
            .Include(u => u.Settings)
            .SingleAsync(u => u.UserName == "hoso");

        // Hồ sơ và cài đặt phải được tạo cùng tài khoản (EF gán khóa theo quan hệ 1-1).
        Assert.NotNull(user.Profile);
        Assert.Equal("Tên Hiển Thị", user.Profile!.DisplayName);
        Assert.NotNull(user.Settings);
        Assert.Equal("vi", user.Settings!.Language);
        Assert.Equal("Active", user.AccountStatus);
        Assert.False(user.EmailConfirmed); // xác minh email là mục [NC]
    }

    [Fact]
    public async Task GetById_va_cap_nhat_ho_so_hoat_dong()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        await sut.CreateUserAsync("capnhat", "capnhat@example.com", "MatKhau@123", "Tên Gốc");
        var user = await db.Users.SingleAsync(u => u.UserName == "capnhat");

        var updated = await sut.UpdateProfileAsync(
            user.Id,
            displayName: "Tên Mới",
            bio: "Tiểu sử",
            location: "Hà Nội",
            dateOfBirth: new DateOnly(2004, 5, 1));
        Assert.True(updated.Succeeded);

        var data = await sut.GetByIdAsync(user.Id);
        Assert.NotNull(data);
        Assert.Equal("Tên Mới", data!.DisplayName);
        Assert.Equal("Tiểu sử", data.Bio);
        Assert.Equal("Hà Nội", data.Location);
        Assert.Equal(new DateOnly(2004, 5, 1), data.DateOfBirth);
    }

    [Fact]
    public async Task UpdateLanguage_luu_dung_gia_tri()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        await sut.CreateUserAsync("ngonngu", "ngonngu@example.com", "MatKhau@123", "Ngôn ngữ");
        var user = await db.Users.SingleAsync(u => u.UserName == "ngonngu");

        var result = await sut.UpdateLanguageAsync(user.Id, "en");
        Assert.True(result.Succeeded);

        var data = await sut.GetByIdAsync(user.Id);
        Assert.Equal("en", data!.Language);
    }

    [Fact]
    public async Task ListUsers_phan_trang_theo_con_tro_va_tim_kiem()
    {
        var (sut, _, provider) = await CreateIdentityServiceAsync();
        await using var _p = provider;

        for (var i = 1; i <= 5; i++)
        {
            await sut.CreateUserAsync($"phantrang{i}", $"phantrang{i}@example.com", "MatKhau@123", $"Người {i}");
        }

        var firstPage = await sut.ListUsersAsync(null, null, null, 3);
        Assert.Equal(3, firstPage.Count);

        // Trang kế tiếp bắt đầu sau bản ghi cuối của trang trước.
        var last = firstPage[^1];
        var secondPage = await sut.ListUsersAsync(null, last.CreatedAt, last.Id, 3);
        Assert.NotEmpty(secondPage);
        Assert.DoesNotContain(secondPage, u => u.Id == last.Id);

        // Tìm kiếm theo tên đăng nhập.
        var search = await sut.ListUsersAsync("phantrang3", null, null, 10);
        Assert.Single(search);
        Assert.Equal("phantrang3", search[0].UserName);
    }

    [Fact]
    public async Task ValidateCredentials_chan_tai_khoan_bi_khoa_va_sai_mat_khau()
    {
        var (sut, db, provider) = await CreateIdentityServiceAsync();
        await using var _ = provider;

        await sut.CreateUserAsync("kiemtra", "kiemtra@example.com", "MatKhau@123", "Kiểm tra");
        var user = await db.Users.SingleAsync(u => u.UserName == "kiemtra");

        // Đúng mật khẩu, đăng nhập bằng email hoặc tên đăng nhập đều được.
        Assert.Equal(user.Id, await sut.ValidateCredentialsAsync("kiemtra@example.com", "MatKhau@123"));
        Assert.Equal(user.Id, await sut.ValidateCredentialsAsync("kiemtra", "MatKhau@123"));

        // Sai mật khẩu và tài khoản không tồn tại đều trả null (không tiết lộ tài khoản có tồn tại).
        Assert.Null(await sut.ValidateCredentialsAsync("kiemtra@example.com", "SaiMatKhau@999"));
        Assert.Null(await sut.ValidateCredentialsAsync("khongton@example.com", "MatKhau@123"));

        // Admin khóa tài khoản → không đăng nhập được dù mật khẩu đúng.
        user.AccountStatus = "Locked";
        await db.SaveChangesAsync();
        Assert.Null(await sut.ValidateCredentialsAsync("kiemtra@example.com", "MatKhau@123"));
    }
}
