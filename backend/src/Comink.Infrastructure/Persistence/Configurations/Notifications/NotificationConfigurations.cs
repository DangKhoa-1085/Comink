// Cấu hình EF Core cho module Thông báo (docs/03_database_design.md mục 3.1, 3.7).
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Notifications;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("user_devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Platform).HasMaxLength(16).IsRequired();
        builder.Property(d => d.PushToken).IsRequired();
        builder.Property(d => d.DeviceName).HasMaxLength(100);

        builder.HasIndex(d => d.PushToken).IsUnique();
        builder.HasIndex(d => d.UserId);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_user_devices_platform",
            "platform IN ('Web', 'Android', 'iOS')"));
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(n => n.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(n => n.Data).HasColumnType("jsonb");
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        builder.HasIndex(n => new { n.RecipientId, n.CreatedAt });
        builder.HasIndex(n => new { n.RecipientId, n.IsRead });

        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Actor)
            .WithMany()
            .HasForeignKey(n => n.ActorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_notifications_type",
            "type IN ('FriendRequestReceived', 'FriendRequestAccepted', 'NewFollower', 'PostLiked', " +
            "'PostCommented', 'CommentReplied', 'PostShared', 'Mentioned', 'MissedCall', 'AddedToGroup', 'AdminAction')"));
    }
}
