// Cấu hình EF Core cho module Mạng xã hội: friendships, follows, posts, post_media,
// post_likes, comments — theo docs/03_database_design.md mục 3.2, 4, 5.2.
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Social;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships", t =>
        {
            t.HasCheckConstraint("ck_friendships_status", "status IN ('Pending', 'Accepted')");
            t.HasCheckConstraint("ck_friendships_not_self", "requester_id <> addressee_id");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(FriendshipStatus.Pending);

        builder.Property(f => f.CreatedAt).IsRequired();

        // Hai khóa ngoại cùng trỏ tới users nên phải khai báo tường minh
        builder.HasOne(f => f.Requester)
            .WithMany()
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Addressee)
            .WithMany()
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lời mời đang chờ gửi tới tôi / tôi đã gửi
        builder.HasIndex(f => new { f.AddresseeId, f.Status });
        builder.HasIndex(f => new { f.RequesterId, f.Status });

        // Chỉ mục duy nhất trên cặp KHÔNG phụ thuộc thứ tự được tạo bằng SQL trong migration
        // (EF Core không hỗ trợ chỉ mục trên biểu thức LEAST/GREATEST):
        //   CREATE UNIQUE INDEX ux_friendships_pair ON friendships (LEAST(requester_id, addressee_id), GREATEST(requester_id, addressee_id));
    }
}

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("follows", t =>
            t.HasCheckConstraint("ck_follows_not_self", "follower_id <> followee_id"));

        builder.HasKey(f => new { f.FollowerId, f.FolloweeId });

        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasOne(f => f.Follower)
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Followee)
            .WithMany()
            .HasForeignKey(f => f.FolloweeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Danh sách người theo dõi tôi
        builder.HasIndex(f => f.FolloweeId);
    }
}

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts", t =>
        {
            t.HasCheckConstraint("ck_posts_content_length", "char_length(content) <= 5000");
            t.HasCheckConstraint("ck_posts_visibility", "visibility IN ('Public', 'Friends', 'OnlyMe')");
            t.HasCheckConstraint("ck_posts_status", "status IN ('Active', 'Hidden')");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Content).IsRequired(false);

        builder.Property(p => p.Visibility)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(Visibility.Friends)
            // BẪY: Visibility.Public = 0 trùng với default(Visibility). Nếu không đặt sentinel khác,
            // EF coi "Public" là "chưa gán" nên bỏ cột khỏi câu INSERT → DB ghi đè bằng 'Friends'
            // (giá trị mặc định). Sentinel -1 (giá trị không hợp lệ) buộc EF luôn gửi giá trị thật.
            .HasSentinel((Visibility)(-1));

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(ContentStatus.Active);

        builder.Property(p => p.LikeCount).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.CommentCount).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.ShareCount).IsRequired().HasDefaultValue(0);

        builder.Property(p => p.CreatedAt).IsRequired();

        // Người dùng không bị xóa cứng nên dùng RESTRICT (mục 1.3 tài liệu thiết kế)
        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bài chia sẻ: bài gốc bị xóa hẳn thì vẫn giữ bài chia sẻ với shared_post_id = null
        builder.HasOne(p => p.SharedPost)
            .WithMany()
            .HasForeignKey(p => p.SharedPostId)
            .OnDelete(DeleteBehavior.SetNull);

        // Concurrency token từ cột hệ thống xmin (mục 1.2) — ánh xạ này KHÔNG tạo cột mới
        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Nguồn của bảng tin và trang cá nhân; chỉ mục có điều kiện để nhỏ và nhanh (mục 5.2)
        builder.HasIndex(p => new { p.AuthorId, p.CreatedAt, p.Id })
            .HasFilter("deleted_at IS NULL AND status = 'Active'")
            .IsDescending(false, true, true);

        builder.HasIndex(p => p.SharedPostId);
    }
}

public class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("post_media");
        builder.HasKey(pm => new { pm.PostId, pm.MediaId });

        builder.Property(pm => pm.SortOrder).IsRequired().HasDefaultValue((short)0);

        builder.HasOne(pm => pm.Post)
            .WithMany(p => p.Media)
            .HasForeignKey(pm => pm.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.Media)
            .WithMany()
            .HasForeignKey(pm => pm.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pm => pm.MediaId);
    }
}

public class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("post_likes");

        // Khóa chính kép cũng chính là chỉ mục tra cứu theo (post_id, user_id):
        // chỉ mục riêng trên post_id là dư thừa nên bỏ.
        builder.HasKey(l => new { l.PostId, l.UserId });

        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasOne(l => l.Post)
            .WithMany()
            .HasForeignKey(l => l.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments", t =>
        {
            t.HasCheckConstraint("ck_comments_content_length", "char_length(content) BETWEEN 1 AND 2000");
            t.HasCheckConstraint("ck_comments_status", "status IN ('Active', 'Hidden')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Content).HasMaxLength(2000).IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(ContentStatus.Active);

        builder.Property(c => c.ReplyCount).IsRequired().HasDefaultValue(0);
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasOne(c => c.Post)
            .WithMany()
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Trả lời một cấp: chỉ mục tự tham chiếu, xóa bình luận cha thì xóa theo
        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bình luận gốc của một bài (mục 5.2)
        builder.HasIndex(c => new { c.PostId, c.CreatedAt, c.Id })
            .HasFilter("deleted_at IS NULL AND parent_comment_id IS NULL");

        // Các trả lời của một bình luận gốc
        builder.HasIndex(c => new { c.ParentCommentId, c.CreatedAt });
    }
}
