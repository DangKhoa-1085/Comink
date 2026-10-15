// Cấu hình EF Core cho ApplicationUser và các bảng Identity mở rộng
using Comink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Identity;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Khóa chính UUID, sinh bởi backend (UUIDv7)
        builder.Property(u => u.Id)
            .ValueGeneratedNever(); // EF không tự sinh, backend sinh

        // account_status: varchar(16) với giá trị hợp lệ
        builder.Property(u => u.AccountStatus)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("Active");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_users_account_status",
            "account_status IN ('Active', 'Locked', 'Deleted')"));

        builder.Property(u => u.LockedReason).HasMaxLength(500);
        builder.Property(u => u.CreatedAt).IsRequired();

        // users.updated_at: cột chuẩn khi tài khoản được cập nhật (mục 1.2 tài liệu thiết kế)
        builder.Property(u => u.UpdatedAt);

        // user_name: tài liệu 03_database_design.md mục 3.1 quy định varchar(32)
        builder.Property(u => u.UserName).HasMaxLength(32);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(32);

        // Quan hệ 1-1 với UserProfile
        builder.HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Quan hệ 1-1 với UserSettings
        builder.HasOne(u => u.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<UserSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Quan hệ 1-nhiều với RefreshToken
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.DisplayName).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Bio).HasMaxLength(300);
        builder.Property(p => p.Location).HasMaxLength(100);
        builder.Property(p => p.AvatarUrl);
        builder.Property(p => p.CoverUrl);

        // Bộ đếm denormalized
        builder.Property(p => p.FollowerCount).HasDefaultValue(0).IsRequired();
        builder.Property(p => p.FollowingCount).HasDefaultValue(0).IsRequired();
        builder.Property(p => p.FriendCount).HasDefaultValue(0).IsRequired();
    }
}

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(s => s.UserId);

        builder.Property(s => s.Language).HasMaxLength(5).IsRequired().HasDefaultValue("vi");

        // Các cài đặt quyền riêng tư [NC] — có sẵn trong schema
        builder.Property(s => s.ProfileVisibility).HasMaxLength(16).IsRequired().HasDefaultValue("Everyone");
        builder.Property(s => s.DefaultPostVisibility).HasMaxLength(16).IsRequired().HasDefaultValue("Friends");
        builder.Property(s => s.WhoCanMessage).HasMaxLength(16).IsRequired().HasDefaultValue("Everyone");
        builder.Property(s => s.WhoCanCall).HasMaxLength(16).IsRequired().HasDefaultValue("Friends");
        builder.Property(s => s.ShowOnlineStatus).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.ShowReadReceipts).IsRequired().HasDefaultValue(true);

        // Bật/tắt từng loại thông báo — jsonb, mặc định bật hết
        builder.Property(s => s.NotificationSettings)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue(UserSettings.DefaultNotificationSettings);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.DeviceName).HasMaxLength(100);
        builder.Property(t => t.IpAddress).HasMaxLength(45);
        builder.Property(t => t.UserAgent).HasMaxLength(512);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();

        // Index để dọn token hết hạn bằng job nền
        builder.HasIndex(t => t.ExpiresAt);
        builder.HasIndex(t => t.UserId);
    }
}
