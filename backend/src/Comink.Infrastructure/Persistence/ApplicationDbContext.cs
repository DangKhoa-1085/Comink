// ApplicationDbContext — EF Core DbContext chính
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của Comink.
/// Kế thừa từ IdentityDbContext để tích hợp ASP.NET Core Identity.
/// Dùng EFCore.NamingConventions để tự chuyển PascalCase → snake_case.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<
    ApplicationUser, IdentityRole<Guid>, Guid,
    IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // --- Identity mở rộng ---
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Áp dụng tất cả IEntityTypeConfiguration<T> từ cùng assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Đổi tên bảng Identity sang snake_case (EFCore.NamingConventions đã xử lý cột,
        // nhưng tên bảng Identity cần đổi thủ công vì chúng được đặt tên cứng)
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Chỉ áp dụng nếu chưa được cấu hình từ bên ngoài (ví dụ từ AddDbContext)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSnakeCaseNamingConvention();
        }
    }
}
