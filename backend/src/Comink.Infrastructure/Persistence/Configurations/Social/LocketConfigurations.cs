// Cấu hình EF Core cho module Comink Locket: locket_moments, locket_recipients, locket_reactions.
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Social;

public class LocketMomentConfiguration : IEntityTypeConfiguration<LocketMoment>
{
    public void Configure(EntityTypeBuilder<LocketMoment> builder)
    {
        builder.ToTable("locket_moments", t =>
        {
            t.HasCheckConstraint(
                "ck_locket_moments_audience",
                "audience_type IN ('AllFriends', 'SelectedFriends')");
            t.HasCheckConstraint(
                "ck_locket_moments_caption_length",
                "caption IS NULL OR char_length(caption) <= 100");
            t.HasCheckConstraint(
                "ck_locket_moments_expiry",
                "expires_at > created_at");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Caption).HasMaxLength(100);

        builder.Property(m => m.AudienceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(LocketAudience.AllFriends)
            .HasSentinel((LocketAudience)(-1));

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ExpiresAt).IsRequired();

        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.MediaAsset)
            .WithMany()
            .HasForeignKey(m => m.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Quét các Locket còn hạn hoặc sắp hết hạn
        builder.HasIndex(m => new { m.ExpiresAt, m.DeletedAt });

        // Phân trang Locket theo tác giả + thời gian
        builder.HasIndex(m => new { m.AuthorId, m.CreatedAt, m.Id });
    }
}

public class LocketRecipientConfiguration : IEntityTypeConfiguration<LocketRecipient>
{
    public void Configure(EntityTypeBuilder<LocketRecipient> builder)
    {
        builder.ToTable("locket_recipients");

        builder.HasKey(r => new { r.MomentId, r.RecipientUserId });

        builder.HasOne(r => r.Moment)
            .WithMany(m => m.Recipients)
            .HasForeignKey(r => r.MomentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.RecipientUser)
            .WithMany()
            .HasForeignKey(r => r.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.RecipientUserId);
    }
}

public class LocketReactionConfiguration : IEntityTypeConfiguration<LocketReaction>
{
    public void Configure(EntityTypeBuilder<LocketReaction> builder)
    {
        builder.ToTable("locket_reactions");

        builder.HasKey(r => new { r.MomentId, r.UserId });

        builder.Property(r => r.Emoji).HasMaxLength(16).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Moment)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MomentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
