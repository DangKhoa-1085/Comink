// DI registration cho Infrastructure layer
using System.Text;
using Comink.Application.Common.Interfaces;
using Comink.Application.Identity;
using Comink.Application.Identity.Abstractions;
using Comink.Application.Social;
using Comink.Application.Social.Abstractions;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Media;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Social;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
        // --- Thời gian (cho phép test thay thế đồng hồ) ---
        services.TryAddTimeProvider();

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

        // --- JWT ---
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Secret) && options.Secret.Length >= 32,
                "Jwt:Secret phải được cấu hình và dài tối thiểu 32 ký tự.")
            .ValidateOnStart();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtIssuer = jwtSection["Issuer"] ?? "comink-api";
        var jwtAudience = jwtSection["Audience"] ?? "comink-clients";
        var jwtSecret = jwtSection["Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "Jwt:Secret chưa được cấu hình. Đặt trong .env (Jwt__Secret) hoặc dotnet user-secrets.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateLifetime = true,
                    // Mặc định .NET cho phép lệch 5 phút; đặt 30 giây để token hết hạn đúng hơn.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Hub SignalR lấy Context.UserIdentifier từ claim này để đưa client vào nhóm user:{id}.
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
                };

                // SignalR: trình duyệt không gửi được header Authorization qua WebSocket,
                // nên chấp nhận access token qua query string `access_token`.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        // AddIdentity ở trên đăng ký cookie scheme và đặt nó làm mặc định.
        // API này xác thực bằng JWT nên phải ghi đè cả ba scheme mặc định;
        // nếu không, request thiếu token sẽ bị chuyển hướng 302 tới /Account/Login
        // thay vì trả 401/403 theo hợp đồng (docs/05_api_contract.md mục 5).
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        // --- Phân quyền theo role ---
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
            .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

        // --- Cổng và dịch vụ nghiệp vụ ---
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();

        // --- Mạng xã hội (Giai đoạn 2) ---
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();
        services.AddScoped<IFriendshipService, FriendshipService>();
        services.AddScoped<IFollowService, FollowService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<ILikeService, LikeService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IMediaService, MediaService>();

        // --- Lưu trữ media: Cloudinary khi có khoá, ngược lại lưu đĩa cục bộ (dev) ---
        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));
        services.AddScoped<IMediaStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MediaStorageOptions>>().Value;

            return options.UseCloudinary
                ? (IMediaStorage)ActivatorUtilities.CreateInstance<CloudinaryMediaStorage>(sp)
                : ActivatorUtilities.CreateInstance<LocalDiskMediaStorage>(sp);
        });

        // --- Redis ---
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        return services;
    }

    /// <summary>
    /// Đăng ký <see cref="TimeProvider"/> nếu chưa có.
    /// Dùng để mọi thành phần lấy thời gian UTC từ một nguồn duy nhất (dễ test).
    /// </summary>
    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
