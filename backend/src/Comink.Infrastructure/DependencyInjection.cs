// DI registration cho Infrastructure layer
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Comink.Infrastructure;

/// <summary>
/// Extension method để đăng ký tất cả dịch vụ Infrastructure vào DI container.
/// Được gọi từ Comink.Api/Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- PostgreSQL + EF Core ---
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            // Tự động chuyển PascalCase C# → snake_case PostgreSQL
            options.UseSnakeCaseNamingConvention();
        });

        // --- ASP.NET Core Identity ---
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Yêu cầu mật khẩu
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            // Khóa tài khoản tạm khi đăng nhập sai nhiều lần (Identity)
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // Tạm thời không yêu cầu xác minh email ở CB
            options.SignIn.RequireConfirmedEmail = false;

            // Cho phép tên người dùng có ký tự đặc biệt (@ . -)
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-@";
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // --- Redis ---
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        return services;
    }
}
