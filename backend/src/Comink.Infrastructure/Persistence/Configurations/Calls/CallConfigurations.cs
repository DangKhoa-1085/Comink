// Cấu hình EF Core cho module Cuộc gọi: calls, call_participants
// — theo docs/03_database_design.md mục 3.4, 4, 5.2, 6.3 (mốc M3).
using Comink.Domain.Enums;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comink.Infrastructure.Persistence.Configurations.Calls;

public class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        builder.ToTable("calls", t =>
        {
            t.HasCheckConstraint("ck_calls_type", "type IN ('Audio', 'Video')");
            t.HasCheckConstraint(
                "ck_calls_status",
                "status IN ('Ringing', 'InProgress', 'Completed', 'Declined', 'Cancelled', 'Missed', 'Failed')");
            t.HasCheckConstraint(
                "ck_calls_end_reason",
                "end_reason IS NULL OR end_reason IN ('Completed', 'NoAnswer', 'Declined', 'Cancelled', 'Failed')");
            t.HasCheckConstraint(
                "ck_calls_duration",
                "duration_seconds IS NULL OR duration_seconds >= 0");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(8)
            .IsRequired()
            .HasDefaultValue(CallType.Audio);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(CallStatus.Ringing);

        builder.Property(c => c.EndReason)
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(c => c.RoomName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasOne(c => c.Conversation)
            .WithMany()
            .HasForeignKey(c => c.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Initiator)
            .WithMany()
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Call)
            .HasForeignKey(p => p.CallId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tên room ở nhà cung cấp là duy nhất
        builder.HasIndex(c => c.RoomName).IsUnique();

        // Lịch sử cuộc gọi của một hội thoại
        builder.HasIndex(c => new { c.ConversationId, c.CreatedAt }).IsDescending(false, true);
    }
}

public class CallParticipantConfiguration : IEntityTypeConfiguration<CallParticipant>
{
    public void Configure(EntityTypeBuilder<CallParticipant> builder)
    {
        builder.ToTable("call_participants", t =>
            t.HasCheckConstraint(
                "ck_call_participants_status",
                "status IN ('Invited', 'Joined', 'Declined', 'Missed', 'Left')"));

        builder.HasKey(p => new { p.CallId, p.UserId });

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(CallParticipantStatus.Invited);

        builder.Property(p => p.InvitedAt).IsRequired();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lịch sử cuộc gọi của tôi (sắp theo lúc được mời)
        builder.HasIndex(p => new { p.UserId, p.InvitedAt }).IsDescending(false, true);
    }
}
