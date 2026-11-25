// Cấu hình EF Core cho module Nhắn tin: conversations, conversation_members, messages,
// message_attachments — theo docs/03_database_design.md mục 3.3, 4, 5.2, 6.3 (mốc M2).
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Messaging;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", t =>
        {
            t.HasCheckConstraint("ck_conversations_type", "type IN ('Direct', 'Group')");
            // Chat riêng bắt buộc có direct_key; nhóm thì không (docs/03_database_design.md mục 3.3)
            t.HasCheckConstraint("ck_conversations_direct_key", "(type = 'Direct') = (direct_key IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(ConversationType.Direct);

        builder.Property(c => c.DirectKey).HasMaxLength(73);
        builder.Property(c => c.Title).HasMaxLength(100);
        builder.Property(c => c.AvatarUrl);

        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // FK vòng: conversations.last_message_id → messages.id phải là SET NULL, không CASCADE
        // (nếu CASCADE sẽ xoá hội thoại khi xoá tin).
        builder.HasOne(c => c.LastMessage)
            .WithMany()
            .HasForeignKey(c => c.LastMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Members)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Danh sách hội thoại của tôi sắp theo tin mới nhất
        builder.HasIndex(c => c.LastMessageAt).IsDescending();

        // Chỉ mục duy nhất CÓ ĐIỀU KIỆN cho direct_key được tạo bằng SQL trong migration
        // (EF Core không hỗ trợ index có WHERE):
        //   CREATE UNIQUE INDEX ux_conversations_direct_key ON conversations (direct_key) WHERE type = 'Direct';
    }
}

public class ConversationMemberConfiguration : IEntityTypeConfiguration<ConversationMember>
{
    public void Configure(EntityTypeBuilder<ConversationMember> builder)
    {
        builder.ToTable("conversation_members", t =>
            t.HasCheckConstraint("ck_conversation_members_role", "role IN ('Owner', 'Admin', 'Member')"));

        builder.HasKey(m => new { m.ConversationId, m.UserId });

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(MemberRole.Member)
            // ⚠️ Bẫy giống `Visibility` ở GĐ 2: CLR default của enum là `Owner` (=0) nhưng cột có
            // default `Member` → EF coi là "chưa gán" và bỏ cột khỏi INSERT. Sentinel buộc EF luôn
            // ghi giá trị thật khi Role = Owner.
            .HasSentinel((MemberRole)(-1));

        builder.Property(m => m.JoinedAt).IsRequired();

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chỉ mục "hội thoại của người dùng" có điều kiện tạo bằng SQL trong migration:
        //   CREATE INDEX ix_conversation_members_user_active ON conversation_members (user_id) WHERE left_at IS NULL;
        builder.HasIndex(m => m.UserId);

        // Mốc đã đọc trỏ tới tin nhắn; xoá tin thì chỉ mất mốc, không xoá thành viên.
        // Phải khai báo TƯỜNG MINH: bảng messages có nhiều quan hệ tới conversation_members
        // (chỉ mốc đọc) nên EF không tự suy ra được.
        builder.HasOne(m => m.LastReadMessage)
            .WithMany()
            .HasForeignKey(m => m.LastReadMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tắt thông báo từng cuộc (NC Ưu tiên 2, M7)
        builder.Property(m => m.MutedUntil);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", t =>
        {
            t.HasCheckConstraint("ck_messages_type", "type IN ('Text', 'Image', 'Video', 'File', 'System', 'Call')");
            t.HasCheckConstraint("ck_messages_content_length", "content IS NULL OR char_length(content) <= 4000");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(MessageType.Text);

        builder.Property(m => m.Content);
        builder.Property(m => m.ClientMessageId).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        // Tin loại Call trỏ tới cuộc gọi; xoá cuộc gọi thì tin vẫn giữ (SET NULL), không CASCADE.
        builder.HasOne(m => m.Call)
            .WithMany()
            .HasForeignKey(m => m.CallId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Conversation)
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hai FK tự thân (NC Ưu tiên 2, M7) — khai báo TƯỜNG MINH vì cùng trỏ về messages.
        // Xoá tin đích thì tin trả lời/chuyển tiếp vẫn giữ, chỉ mất liên kết (SET NULL).
        builder.HasOne(m => m.ReplyToMessage)
            .WithMany()
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.ForwardedFromMessage)
            .WithMany()
            .HasForeignKey(m => m.ForwardedFromMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Chỉnh sửa/thu hồi (NC Ưu tiên 2)
        builder.Property(m => m.EditedAt);
        builder.Property(m => m.DeletedAt);

        builder.HasMany(m => m.Attachments)
            .WithOne(a => a.Message)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chống gửi trùng: một người chỉ có một tin cho mỗi client_message_id
        builder.HasIndex(m => new { m.SenderId, m.ClientMessageId }).IsUnique();

        // Lịch sử tin của hội thoại: conversation_id, created_at DESC, id DESC
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt, m.Id })
            .IsDescending(false, true, true);
    }
}

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("message_attachments");

        builder.HasKey(a => new { a.MessageId, a.MediaId });

        builder.Property(a => a.SortOrder).IsRequired().HasDefaultValue((short)0);

        builder.HasOne(a => a.Media)
            .WithMany()
            .HasForeignKey(a => a.MediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("message_reactions");

        // Một người được thả NHIỀU loại cảm xúc trên cùng một tin (docs/03 mục 7 #1):
        // khoá chính gồm cả emoji. Mỗi loại chỉ một lần/người/tin.
        builder.HasKey(r => new { r.MessageId, r.UserId, r.Emoji });

        builder.Property(r => r.Emoji).HasMaxLength(16).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Message)
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PinnedMessageConfiguration : IEntityTypeConfiguration<PinnedMessage>
{
    public void Configure(EntityTypeBuilder<PinnedMessage> builder)
    {
        builder.ToTable("pinned_messages");

        builder.HasKey(p => new { p.ConversationId, p.MessageId });

        builder.Property(p => p.PinnedAt).IsRequired();

        builder.HasOne(p => p.Conversation)
            .WithMany()
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Thu hồi/xoá tin thì bỏ ghim theo (xử lý tường minh ở service, cascade là lưới an toàn).
        builder.HasOne(p => p.Message)
            .WithMany()
            .HasForeignKey(p => p.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.PinnedByUser)
            .WithMany()
            .HasForeignKey(p => p.PinnedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Panel ghim sắp theo thời điểm ghim giảm dần.
        builder.HasIndex(p => new { p.ConversationId, p.PinnedAt }).IsDescending(false, true);
    }
}
