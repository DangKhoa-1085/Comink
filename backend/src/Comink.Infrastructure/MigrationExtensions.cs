// Extension giúp áp dụng migration và seed dữ liệu khi khởi động (chỉ môi trường Dev)
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Comink.Infrastructure;

/// <summary>
/// Extension method để áp dụng migration EF Core và seed data khi khởi động.
/// Chỉ dùng trong môi trường Development.
/// </summary>
public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            // Áp dụng migration chưa chạy
            await db.Database.MigrateAsync();
            logger.LogInformation("Migration áp dụng thành công");

            // Seed roles
            await SeedRolesAsync(roleManager, logger);

            // Seed admin account từ biến môi trường
            await SeedAdminAsync(userManager, configuration, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi áp dụng migration hoặc seed data");
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
