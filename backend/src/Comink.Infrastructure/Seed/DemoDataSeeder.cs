// Seed dữ liệu mẫu cho kịch bản demo Mốc A (chỉ Development).
//
// Mục đích: dựng lại nhanh một mạng xã hội có sẵn bạn bè, bài viết, bình luận, chat 1–1 và
// chat nhóm để demo, tránh phải tạo tay từ đầu mỗi lần.
//
// An toàn:
// - Chỉ chạy khi `DemoSeed:Enabled=true`; mật khẩu lấy từ cấu hình, KHÔNG hard-code.
// - Chạy trong một transaction; nếu lỗi thì rollback, không để dữ liệu nửa vời.
// - Idempotent: nếu đã có bất kỳ tài khoản demo nào thì bỏ qua toàn bộ.
using Comink.Domain.Enums;
using Comink.Domain.Messaging;
using Comink.Infrastructure.Identity;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Comink.Infrastructure.Seed;

/// <summary>Tạo dữ liệu mẫu cho kịch bản demo mức cơ bản.</summary>
public static class DemoDataSeeder
{
    /// <summary>Tài khoản demo; nếu bất kỳ tên nào đã tồn tại thì coi như đã seed.</summary>
    public static readonly IReadOnlyList<string> DemoUserNames = ["an.demo", "binh.demo", "chi.demo"];

    private const string DefaultRole = "User";

    /// <summary>
    /// Seed dữ liệu demo. Trả <c>true</c> nếu vừa tạo mới, <c>false</c> nếu tắt/đã có/lỗi.
    /// <paramref name="password"/> là mật khẩu chung cho 3 tài khoản demo (lấy từ cấu hình).
    /// </summary>
    public static async Task<bool> SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        bool enabled,
        string? password,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "DemoSeed:Enabled=true nhưng thiếu DemoSeed:Password — bỏ qua seed dữ liệu demo");
            return false;
        }

        var existingCount = await db.Users
            .CountAsync(u => u.UserName != null && DemoUserNames.Contains(u.UserName), cancellationToken);

        if (existingCount > 0)
        {
            logger.LogInformation(
                "Đã có {Count}/{Total} tài khoản demo — bỏ qua seed dữ liệu demo",
                existingCount,
                DemoUserNames.Count);
            return false;
        }

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // --- Tài khoản ---
            var an = NewDemoUser("an.demo", "An Nguyễn", now.AddMinutes(-90));
            var binh = NewDemoUser("binh.demo", "Bình Trần", now.AddMinutes(-80));
            var chi = NewDemoUser("chi.demo", "Chi Lê", now.AddMinutes(-70));

            foreach (var user in new[] { an, binh, chi })
            {
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogError(
                        "Không tạo được tài khoản demo {UserName}: {Errors}",
                        user.UserName,
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }

                var role = await userManager.AddToRoleAsync(user, DefaultRole);
                if (!role.Succeeded)
                {
                    logger.LogError(
                        "Không gán được role {Role} cho tài khoản demo {UserName}: {Errors}",
                        DefaultRole,
                        user.UserName,
                        string.Join("; ", role.Errors.Select(e => e.Description)));
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            // --- Quan hệ bạn bè: An–Bình, An–Chi (đã chấp nhận), Bình→Chi (đang chờ) ---
            db.Friendships.AddRange(
                new Friendship
                {
                    RequesterId = an.Id,
                    AddresseeId = binh.Id,
                    Status = FriendshipStatus.Accepted,
                    CreatedAt = now.AddDays(-5),
                    RespondedAt = now.AddDays(-5).AddMinutes(5)
                },
                new Friendship
                {
                    RequesterId = chi.Id,
                    AddresseeId = an.Id,
                    Status = FriendshipStatus.Accepted,
                    CreatedAt = now.AddDays(-4),
                    RespondedAt = now.AddDays(-4).AddMinutes(10)
                },
                new Friendship
                {
                    RequesterId = binh.Id,
                    AddresseeId = chi.Id,
                    Status = FriendshipStatus.Pending,
                    CreatedAt = now.AddDays(-1)
                });

            // --- Theo dõi một chiều: Bình→An, Chi→An, An→Chi ---
            db.Follows.AddRange(
                new Follow { FollowerId = binh.Id, FolloweeId = an.Id, CreatedAt = now.AddDays(-3) },
                new Follow { FollowerId = chi.Id, FolloweeId = an.Id, CreatedAt = now.AddDays(-3) },
                new Follow { FollowerId = an.Id, FolloweeId = chi.Id, CreatedAt = now.AddDays(-2) });

            // --- Bài viết ---
            var anPost = new Post
            {
                AuthorId = an.Id,
                Content = "Chào mừng cả nhà đến với Comink! 🎉",
                Visibility = Visibility.Public,
                CreatedAt = now.AddMinutes(-60)
            };
            var binhPost = new Post
            {
                AuthorId = binh.Id,
                Content = "Cuối tuần này mình đi cà phê học nhóm, ai tham gia không?",
                Visibility = Visibility.Friends,
                CreatedAt = now.AddMinutes(-45)
            };
            var chiPost = new Post
            {
                AuthorId = chi.Id,
                Content = "Hôm nay mình thử gọi video trên Comink, nét thật!",
                Visibility = Visibility.Friends,
                CreatedAt = now.AddMinutes(-30)
            };
            var anPrivatePost = new Post
            {
                AuthorId = an.Id,
                Content = "Ghi chú riêng: chuẩn bị slide bảo vệ đồ án.",
                Visibility = Visibility.OnlyMe,
                CreatedAt = now.AddMinutes(-20)
            };
            var chiSharedPost = new Post
            {
                AuthorId = chi.Id,
                Content = "Bài này hay nè cả nhà!",
                Visibility = Visibility.Friends,
                SharedPostId = anPost.Id,
                CreatedAt = now.AddMinutes(-25)
            };

            anPost.LikeCount = 2;
            anPost.CommentCount = 2;
            anPost.ShareCount = 1;
            binhPost.LikeCount = 1;
            binhPost.CommentCount = 1;
            chiPost.LikeCount = 1;

            db.Posts.AddRange(anPost, binhPost, chiPost, anPrivatePost, chiSharedPost);

            // --- Lượt thích ---
            db.PostLikes.AddRange(
                new PostLike { PostId = anPost.Id, UserId = binh.Id, CreatedAt = now.AddMinutes(-55) },
                new PostLike { PostId = anPost.Id, UserId = chi.Id, CreatedAt = now.AddMinutes(-54) },
                new PostLike { PostId = binhPost.Id, UserId = an.Id, CreatedAt = now.AddMinutes(-40) },
                new PostLike { PostId = chiPost.Id, UserId = an.Id, CreatedAt = now.AddMinutes(-28) });

            // --- Bình luận (1 trả lời trực tiếp) ---
            var binhComment = new Comment
            {
                PostId = anPost.Id,
                AuthorId = binh.Id,
                Content = "Chào An 👋",
                ReplyCount = 1,
                CreatedAt = now.AddMinutes(-50)
            };
            var chiReply = new Comment
            {
                PostId = anPost.Id,
                AuthorId = chi.Id,
                ParentCommentId = binhComment.Id,
                Content = "Mình cũng tham gia!",
                CreatedAt = now.AddMinutes(-48)
            };
            var anComment = new Comment
            {
                PostId = binhPost.Id,
                AuthorId = an.Id,
                Content = "Cho mình một suất nhé!",
                CreatedAt = now.AddMinutes(-35)
            };
            db.Comments.AddRange(binhComment, chiReply, anComment);

            // --- Chat 1–1: An ↔ Bình ---
            var directAnBinh = new Conversation
            {
                Type = ConversationType.Direct,
                DirectKey = ConversationRules.BuildDirectKey(an.Id, binh.Id),
                CreatedBy = an.Id,
                CreatedAt = now.AddMinutes(-40)
            };
            directAnBinh.Members.Add(new ConversationMember
            {
                ConversationId = directAnBinh.Id,
                UserId = an.Id,
                Role = MemberRole.Member,
                JoinedAt = directAnBinh.CreatedAt
            });
            directAnBinh.Members.Add(new ConversationMember
            {
                ConversationId = directAnBinh.Id,
                UserId = binh.Id,
                Role = MemberRole.Member,
                JoinedAt = directAnBinh.CreatedAt
            });

            // --- Chat 1–1: Bình ↔ Chi ---
            var directBinhChi = new Conversation
            {
                Type = ConversationType.Direct,
                DirectKey = ConversationRules.BuildDirectKey(binh.Id, chi.Id),
                CreatedBy = chi.Id,
                CreatedAt = now.AddMinutes(-30)
            };
            directBinhChi.Members.Add(new ConversationMember
            {
                ConversationId = directBinhChi.Id,
                UserId = binh.Id,
                Role = MemberRole.Member,
                JoinedAt = directBinhChi.CreatedAt
            });
            directBinhChi.Members.Add(new ConversationMember
            {
                ConversationId = directBinhChi.Id,
                UserId = chi.Id,
                Role = MemberRole.Member,
                JoinedAt = directBinhChi.CreatedAt
            });

            // --- Chat nhóm 3 người ---
            var group = new Conversation
            {
                Type = ConversationType.Group,
                Title = "Nhóm học tập",
                CreatedBy = an.Id,
                CreatedAt = now.AddMinutes(-25)
            };
            group.Members.Add(new ConversationMember
            {
                ConversationId = group.Id,
                UserId = an.Id,
                Role = MemberRole.Owner,
                JoinedAt = group.CreatedAt
            });
            group.Members.Add(new ConversationMember
            {
                ConversationId = group.Id,
                UserId = binh.Id,
                Role = MemberRole.Member,
                JoinedAt = group.CreatedAt
            });
            group.Members.Add(new ConversationMember
            {
                ConversationId = group.Id,
                UserId = chi.Id,
                Role = MemberRole.Member,
                JoinedAt = group.CreatedAt
            });

            db.Conversations.AddRange(directAnBinh, directBinhChi, group);
            await db.SaveChangesAsync(cancellationToken);

            // --- Tin nhắn (sau khi hội thoại đã có id) ---
            var messages = new List<Message>
            {
                NewTextMessage(directAnBinh.Id, an.Id, "Chào Bình, chiều nay học nhóm không?", now.AddMinutes(-39)),
                NewTextMessage(directAnBinh.Id, binh.Id, "Có nhé, 3 giờ nhé!", now.AddMinutes(-38)),
                NewTextMessage(directAnBinh.Id, an.Id, "Ok 👍", now.AddMinutes(-37)),
                NewTextMessage(directBinhChi.Id, chi.Id, "Bình ơi, mai nộp bài rồi", now.AddMinutes(-29)),
                NewTextMessage(directBinhChi.Id, binh.Id, "Mình biết rồi, tối nay xong nhé", now.AddMinutes(-28)),
                NewTextMessage(group.Id, chi.Id, "Mọi người ơi, chuẩn bị họp nhóm nhé", now.AddMinutes(-14)),
                NewTextMessage(group.Id, binh.Id, "Mình xong phần API rồi", now.AddMinutes(-13)),
                NewTextMessage(group.Id, an.Id, "Tuyệt, mình đang làm giao diện", now.AddMinutes(-12))
            };
            db.Messages.AddRange(messages);
            await db.SaveChangesAsync(cancellationToken);

            // --- Cập nhật tin cuối của từng hội thoại ---
            var lastByConversation = messages
                .GroupBy(m => m.ConversationId)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.CreatedAt).Last());

            foreach (var conversation in new[] { directAnBinh, directBinhChi, group })
            {
                if (lastByConversation.TryGetValue(conversation.Id, out var last))
                {
                    conversation.LastMessageId = last.Id;
                    conversation.LastMessageAt = last.CreatedAt;
                }
            }

            // --- Cập nhật các cột đếm denormalized ---
            var profiles = await db.UserProfiles
                .Where(p => p.UserId == an.Id || p.UserId == binh.Id || p.UserId == chi.Id)
                .ToDictionaryAsync(p => p.UserId, cancellationToken);

            profiles[an.Id].FriendCount = 2;
            profiles[an.Id].FollowerCount = 2;
            profiles[an.Id].FollowingCount = 1;

            profiles[binh.Id].FriendCount = 1;
            profiles[binh.Id].FollowerCount = 0;
            profiles[binh.Id].FollowingCount = 1;

            profiles[chi.Id].FriendCount = 1;
            profiles[chi.Id].FollowerCount = 1;
            profiles[chi.Id].FollowingCount = 1;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Đã tạo dữ liệu demo: 3 tài khoản (an.demo/binh.demo/chi.demo), 3 quan hệ bạn bè, " +
                "3 theo dõi, 5 bài viết, 3 bình luận, 4 lượt thích, 3 hội thoại, 8 tin nhắn");
            return true;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Seed dữ liệu demo thất bại — đã rollback");
            return false;
        }
    }

    private static ApplicationUser NewDemoUser(
        string userName,
        string displayName,
        DateTimeOffset createdAt) => new()
    {
        // Phải gán Id tường minh (ApplicationUser dùng ValueGeneratedNever) — xem ghi chú ở
        // IdentityService/MigrationExtensions.
        Id = Guid.CreateVersion7(),
        UserName = userName,
        Email = $"{userName}@comink.local",
        EmailConfirmed = true,
        AccountStatus = "Active",
        CreatedAt = createdAt,
        Profile = new UserProfile { DisplayName = displayName },
        Settings = new UserSettings()
    };

    private static Message NewTextMessage(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset createdAt) => new()
    {
        ConversationId = conversationId,
        SenderId = senderId,
        Type = MessageType.Text,
        Content = content,
        ClientMessageId = Guid.CreateVersion7(),
        CreatedAt = createdAt
    };
}
