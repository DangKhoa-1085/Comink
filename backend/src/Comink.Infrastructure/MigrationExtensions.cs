// Extension áp dụng migration (chỉ Development) và seed dữ liệu thiết yếu khi khởi động.
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Comink.Infrastructure;

/// <summary>
/// Extension method để áp dụng migration EF Core (chỉ Development) và seed role/admin thiết yếu khi khởi động.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Áp dụng migration chưa chạy, seed role/admin và dữ liệu mẫu demo (chỉ dùng trong môi trường Development).
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            // Áp dụng migration chưa chạy
            await db.Database.MigrateAsync();
            logger.LogInformation("Migration áp dụng thành công");

            await serviceProvider.SeedEssentialDataAsync();

            // Seed dữ liệu mẫu cho demo (mặc định tắt; CHỈ Development) — xem docs/tasks/milestone_a_web_cb.md
            var demoEnabled = bool.TryParse(configuration["DemoSeed:Enabled"], out var enabled) && enabled;
            if (environment.IsDevelopment() && demoEnabled)
            {
                var demoSeeded = await DemoDataSeeder.SeedAsync(
                    db,
                    userManager,
                    enabled: true,
                    password: configuration["DemoSeed:Password"],
                    logger);

                if (demoSeeded)
                {
                    logger.LogInformation("Dữ liệu demo đã sẵn sàng — xem README mục dữ liệu mẫu");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi áp dụng migration hoặc seed data");
            throw;
        }
    }

    /// <summary>
    /// Đảm bảo hai role cơ bản (<c>User</c>, <c>Admin</c>) và tài khoản admin ban đầu (nếu có cấu hình
    /// <c>AdminSeed</c>) đã tồn tại. Dùng được ở cả Production lẫn Development; không chạy migration.
    /// </summary>
    public static async Task SeedEssentialDataAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            await SeedRolesAsync(roleManager, logger);
            await SeedAdminAsync(userManager, configuration, logger);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(
                ex,
                "Thiếu bảng cơ sở dữ liệu khi seed dữ liệu thiết yếu. Hãy chạy `dotnet ef database update` trước khi khởi động môi trường Production.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi seed role hoặc tài khoản admin");
            throw;
        }
    }

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger logger)
    {
        string[] roles = ["User", "Admin"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Đã tạo role: {Role}", role);
            }
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var adminEmail = configuration["AdminSeed:Email"];
        var adminPassword = configuration["AdminSeed:Password"];
        var adminDisplayName = configuration["AdminSeed:DisplayName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Biến môi trường AdminSeed:Email hoặc AdminSeed:Password chưa cấu hình — bỏ qua seed admin");
            return;
        }

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null) return;

        var admin = new ApplicationUser
        {
            // Phải gán Id tường minh: ApplicationUser được cấu hình ValueGeneratedNever
            // (xem IdentityConfigurations), nên bỏ trống thì EF ghi Guid.Empty — trùng với
            // giá trị "chưa đặt" mà code thường dùng để kiểm tra.
            Id = Guid.CreateVersion7(),
            UserName = "admin",
            Email = adminEmail,
            EmailConfirmed = true,
            Profile = new UserProfile { DisplayName = adminDisplayName }
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Đã tạo tài khoản admin: {Email}", adminEmail);
        }
        else
        {
            logger.LogError("Không thể tạo admin: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
