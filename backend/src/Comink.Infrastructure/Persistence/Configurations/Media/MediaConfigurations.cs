// Cấu hình EF Core cho media_assets (docs/03_database_design.md mục 3.5).
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Media;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets", t =>
        {
            // 'Local' chỉ dùng khi dev chưa có khoá Cloudinary (phương án B của GĐ 2).
            t.HasCheckConstraint("ck_media_assets_provider", "provider IN ('Cloudinary', 'Firebase', 'Local')");
            t.HasCheckConstraint("ck_media_assets_resource_type", "resource_type IN ('Image', 'Video', 'File')");
            t.HasCheckConstraint(
                "ck_media_assets_context",
                "context IN ('Post', 'Message', 'Avatar', 'Cover', 'Album')");
            t.HasCheckConstraint("ck_media_assets_size", "size_bytes > 0");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Provider)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(MediaProvider.Cloudinary);

        builder.Property(m => m.ProviderPublicId).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Url).IsRequired();
        builder.Property(m => m.ThumbnailUrl);

        builder.Property(m => m.ResourceType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(MediaResourceType.Image);

        builder.Property(m => m.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.OriginalFilename).HasMaxLength(255);
        builder.Property(m => m.SizeBytes).IsRequired();
        builder.Property(m => m.DurationSeconds).HasPrecision(10, 2);

        builder.Property(m => m.Context)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(MediaContext.Post);

        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasOne(m => m.Owner)
            .WithMany()
            .HasForeignKey(m => m.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mã tệp chỉ duy nhất trong cùng một provider
        builder.HasIndex(m => new { m.Provider, m.ProviderPublicId }).IsUnique();

        // Tổng dung lượng đã dùng của một người (mục 3.5)
        builder.HasIndex(m => m.OwnerId);
    }
}
