// Nghiệp vụ Comink Locket: đăng ảnh 24h, xem feed bạn bè, thả cảm xúc, trả lời chat và dọn hết hạn.
using Comink.Application.Common;
using Comink.Application.Messaging.Abstractions;
using Comink.Application.Messaging.Dtos;
using Comink.Application.Social.Abstractions;
using Comink.Application.Social.Dtos;
using Comink.Domain.Enums;
using Comink.Domain.Social;
using Comink.Infrastructure.Persistence;
using Comink.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comink.Infrastructure.Social;

/// <summary>Dịch vụ xử lý nghiệp vụ khoảnh khắc Locket (thời hạn 24 giờ).</summary>
public class LocketService : ILocketService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;
    private readonly IConversationService? _conversations;
    private readonly IMessageService? _messages;
    private readonly ILocketNotifier? _notifier;

    public LocketService(
        ApplicationDbContext db,
        TimeProvider? time = null,
        IConversationService? conversations = null,
        IMessageService? messages = null,
        ILocketNotifier? notifier = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
        _conversations = conversations;
        _messages = messages;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<OperationResult<LocketDto>> CreateAsync(
        Guid currentUserId,
        CreateLocketRequest request,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MediaAssetId && m.DeletedAt == null, cancellationToken);

        if (asset is null)
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.MediaNotFound);
        }

        if (asset.OwnerId != currentUserId || asset.Context != MediaContext.Locket)
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.MediaNotOwned);
        }

        var audience = ParseAudience(request.AudienceType);
        if (audience is null)
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.ValidationFailed);
        }

        var friendIds = await GetAcceptedFriendIdsAsync(currentUserId, cancellationToken);
        var distinctRecipients = (request.RecipientIds ?? [])
            .Where(id => id != Guid.Empty && id != currentUserId)
            .Distinct()
            .ToList();

        if (audience == LocketAudience.SelectedFriends)
        {
            if (distinctRecipients.Count == 0)
            {
                return OperationResult<LocketDto>.Failure(ErrorCodes.ValidationFailed);
            }

            if (distinctRecipients.Any(id => !friendIds.Contains(id)))
            {
                return OperationResult<LocketDto>.Failure(ErrorCodes.LocketNotFriends);
            }
        }

        var now = _time.GetUtcNow();
        var caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim();
        if (caption is not null && caption.Length > LocketRules.MaxCaptionLength)
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.ValidationFailed);
        }

        var moment = new LocketMoment
        {
            AuthorId = currentUserId,
            MediaAssetId = asset.Id,
            Caption = caption,
            AudienceType = audience.Value,
            CreatedAt = now,
            ExpiresAt = now.Add(LocketRules.Lifetime)
        };

        if (audience == LocketAudience.SelectedFriends)
        {
            foreach (var recipientId in distinctRecipients)
            {
                moment.Recipients.Add(new LocketRecipient
                {
                    MomentId = moment.Id,
                    RecipientUserId = recipientId
                });
            }
        }

        _db.LocketMoments.Add(moment);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = (await MapToDtosAsync(currentUserId, [moment], cancellationToken)).First();

        var viewerIds = audience == LocketAudience.AllFriends
            ? friendIds.Append(currentUserId).Distinct().ToList()
            : distinctRecipients.Append(currentUserId).Distinct().ToList();

        if (_notifier is not null)
        {
            await _notifier.LocketCreatedAsync(viewerIds, dto, cancellationToken);
        }

        return OperationResult<LocketDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedList<LocketDto>>> GetFeedAsync(
        Guid currentUserId,
        Guid? authorId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var pageSize = SocialPaging.Normalize(limit);
        var decoded = CursorCodec.TryDecode(cursor);

        var friendIdsQuery = _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && f.RequesterId == currentUserId)
            .Select(f => f.AddresseeId)
            .Union(_db.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted && f.AddresseeId == currentUserId)
                .Select(f => f.RequesterId));

        var query = _db.LocketMoments
            .AsNoTracking()
            .Where(m => m.DeletedAt == null && m.ExpiresAt > now)
            .Where(m => m.AuthorId == currentUserId
                        || (friendIdsQuery.Contains(m.AuthorId)
                            && (m.AudienceType == LocketAudience.AllFriends
                                || m.Recipients.Any(r => r.RecipientUserId == currentUserId))));

        if (authorId is not null && authorId.Value != Guid.Empty)
        {
            query = query.Where(m => m.AuthorId == authorId.Value);
        }

        if (decoded is not null)
        {
            var cursorCreatedAt = decoded.Value.CreatedAt;
            var cursorId = decoded.Value.Id;
            query = query.Where(m =>
                m.CreatedAt < cursorCreatedAt ||
                (m.CreatedAt == cursorCreatedAt && m.Id.CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageItems = hasMore ? rows.Take(pageSize).ToList() : rows;
        var dtos = await MapToDtosAsync(currentUserId, pageItems, cancellationToken);

        var nextCursor = hasMore && pageItems.Count > 0
            ? CursorCodec.Encode(pageItems[^1].CreatedAt, pageItems[^1].Id)
            : null;

        return OperationResult<PagedList<LocketDto>>.Success(
            new PagedList<LocketDto>(dtos, nextCursor, hasMore));
    }

    /// <inheritdoc />
    public async Task<OperationResult<LocketDto>> ReactAsync(
        Guid currentUserId,
        Guid momentId,
        ReactLocketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!LocketRules.IsValidReaction(request.Emoji))
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.LocketReactionInvalid);
        }

        var now = _time.GetUtcNow();
        var moment = await _db.LocketMoments
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == momentId && m.DeletedAt == null, cancellationToken);

        if (moment is null)
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.LocketNotFound);
        }

        if (!await CanViewMomentAsync(currentUserId, moment, now, checkExpiry: false, cancellationToken))
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.LocketNotFound);
        }

        if (LocketRules.IsExpired(moment.ExpiresAt, now))
        {
            return OperationResult<LocketDto>.Failure(ErrorCodes.LocketExpired);
        }

        var normalizedEmoji = request.Emoji.Trim();
        var existing = await _db.LocketReactions
            .FirstOrDefaultAsync(r => r.MomentId == momentId && r.UserId == currentUserId, cancellationToken);

        if (existing is not null)
        {
            if (existing.Emoji == normalizedEmoji)
            {
                _db.LocketReactions.Remove(existing);
            }
            else
            {
                existing.Emoji = normalizedEmoji;
                existing.CreatedAt = now;
            }
        }
        else
        {
            _db.LocketReactions.Add(new LocketReaction
            {
                MomentId = momentId,
                UserId = currentUserId,
                Emoji = normalizedEmoji,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_notifier is not null)
        {
            var viewerIds = await ResolveAudienceUserIdsAsync(moment, cancellationToken);
            await _notifier.LocketUpdatedAsync(viewerIds, momentId, cancellationToken);
        }

        var dto = (await MapToDtosAsync(currentUserId, [moment], cancellationToken)).First();
        return OperationResult<LocketDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<OperationResult<LocketReplyResultDto>> ReplyAsync(
        Guid currentUserId,
        Guid momentId,
        ReplyLocketRequest request,
        CancellationToken cancellationToken = default)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(content) || content.Length > LocketRules.MaxReplyLength)
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.ValidationFailed);
        }

        var now = _time.GetUtcNow();
        var moment = await _db.LocketMoments
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == momentId && m.DeletedAt == null, cancellationToken);

        if (moment is null)
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.LocketNotFound);
        }

        if (moment.AuthorId == currentUserId)
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.LocketCannotReplySelf);
        }

        if (!await CanViewMomentAsync(currentUserId, moment, now, checkExpiry: false, cancellationToken))
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.LocketNotFound);
        }

        if (LocketRules.IsExpired(moment.ExpiresAt, now))
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.LocketExpired);
        }

        if (_conversations is null || _messages is null)
        {
            return OperationResult<LocketReplyResultDto>.Failure(ErrorCodes.InternalError);
        }

        var conversationResult = await _conversations.OpenDirectAsync(currentUserId, moment.AuthorId, cancellationToken);
        if (!conversationResult.Succeeded || conversationResult.Value is null)
        {
            return OperationResult<LocketReplyResultDto>.Failure(
                conversationResult.ErrorCode ?? ErrorCodes.MessagingNotFriends);
        }

        var header = string.IsNullOrWhiteSpace(moment.Caption)
            ? "[Comink Locket]"
            : $"[Comink Locket · \"{moment.Caption}\"]";
        var fullMessage = $"{header}\n{content}";

        var sendResult = await _messages.SendAsync(
            currentUserId,
            conversationResult.Value.Conversation.Id,
            new SendMessageRequest(Guid.CreateVersion7(), fullMessage, MediaIds: null),
            cancellationToken);

        if (!sendResult.Succeeded || sendResult.Value is null)
        {
            return OperationResult<LocketReplyResultDto>.Failure(
                sendResult.ErrorCode ?? ErrorCodes.InternalError);
        }

        return OperationResult<LocketReplyResultDto>.Success(new LocketReplyResultDto(
            conversationResult.Value.Conversation.Id,
            sendResult.Value.Message.Id));
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> DeleteAsync(
        Guid currentUserId,
        Guid momentId,
        CancellationToken cancellationToken = default)
    {
        var moment = await _db.LocketMoments
            .FirstOrDefaultAsync(m => m.Id == momentId && m.DeletedAt == null, cancellationToken);

        if (moment is null)
        {
            return OperationResult<bool>.Failure(ErrorCodes.LocketNotFound);
        }

        if (moment.AuthorId != currentUserId)
        {
            return OperationResult<bool>.Failure(ErrorCodes.LocketNotOwned);
        }

        var viewerIds = await ResolveAudienceUserIdsAsync(moment, cancellationToken);
        moment.DeletedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        if (_notifier is not null)
        {
            await _notifier.LocketDeletedAsync(viewerIds, momentId, cancellationToken);
        }

        return OperationResult<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        return await _db.LocketMoments
            .Where(m => m.DeletedAt == null && m.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.DeletedAt, now), cancellationToken);
    }

    private async Task<HashSet<Guid>> GetAcceptedFriendIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ids = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private async Task<bool> CanViewMomentAsync(
        Guid viewerId,
        LocketMoment moment,
        DateTimeOffset now,
        bool checkExpiry,
        CancellationToken cancellationToken)
    {
        if (moment.DeletedAt is not null)
        {
            return false;
        }

        if (checkExpiry && LocketRules.IsExpired(moment.ExpiresAt, now))
        {
            return false;
        }

        if (viewerId == moment.AuthorId)
        {
            return true;
        }

        var isFriend = await SocialLookup.AreFriendsAsync(_db, viewerId, moment.AuthorId, cancellationToken);
        var isExplicit = moment.AudienceType == LocketAudience.AllFriends
            || await _db.LocketRecipients
                .AsNoTracking()
                .AnyAsync(r => r.MomentId == moment.Id && r.RecipientUserId == viewerId, cancellationToken);

        return LocketRules.CanView(
            viewerId,
            moment.AuthorId,
            isFriend,
            moment.AudienceType,
            isExplicit,
            moment.ExpiresAt,
            moment.DeletedAt,
            checkExpiry ? now : moment.ExpiresAt.AddMinutes(-1));
    }

    private async Task<IReadOnlyList<Guid>> ResolveAudienceUserIdsAsync(
        LocketMoment moment,
        CancellationToken cancellationToken)
    {
        if (moment.AudienceType == LocketAudience.SelectedFriends)
        {
            var recipientIds = await _db.LocketRecipients
                .AsNoTracking()
                .Where(r => r.MomentId == moment.Id)
                .Select(r => r.RecipientUserId)
                .ToListAsync(cancellationToken);

            return recipientIds.Append(moment.AuthorId).Distinct().ToList();
        }

        var friendIds = await GetAcceptedFriendIdsAsync(moment.AuthorId, cancellationToken);
        return friendIds.Append(moment.AuthorId).Distinct().ToList();
    }

    private async Task<List<LocketDto>> MapToDtosAsync(
        Guid viewerId,
        IReadOnlyList<LocketMoment> moments,
        CancellationToken cancellationToken)
    {
        if (moments.Count == 0)
        {
            return [];
        }

        var authorIds = moments.Select(m => m.AuthorId).Distinct().ToList();
        var mediaIds = moments.Select(m => m.MediaAssetId).Distinct().ToList();
        var momentIds = moments.Select(m => m.Id).ToList();

        var authors = await _db.Users
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                UserName = u.UserName ?? string.Empty,
                DisplayName = u.Profile != null ? u.Profile.DisplayName : string.Empty,
                AvatarUrl = u.Profile != null ? u.Profile.AvatarUrl : null
            })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var mediaUrls = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, Url = m.ThumbnailUrl ?? m.Url })
            .ToDictionaryAsync(m => m.Id, m => m.Url, cancellationToken);

        var reactions = await _db.LocketReactions
            .AsNoTracking()
            .Where(r => momentIds.Contains(r.MomentId))
            .Select(r => new { r.MomentId, r.UserId, r.Emoji })
            .ToListAsync(cancellationToken);

        var reactionsByMoment = reactions
            .GroupBy(r => r.MomentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LocketReactionSummaryDto>)g
                    .GroupBy(item => item.Emoji)
                    .Select(eg => new LocketReactionSummaryDto(
                        eg.Key,
                        eg.Count(),
                        eg.Any(item => item.UserId == viewerId)))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Emoji, StringComparer.Ordinal)
                    .ToList());

        var result = new List<LocketDto>(moments.Count);
        foreach (var moment in moments)
        {
            var author = authors.TryGetValue(moment.AuthorId, out var a)
                ? new LocketAuthorDto(a.Id, a.UserName, a.DisplayName, a.AvatarUrl)
                : new LocketAuthorDto(moment.AuthorId, string.Empty, string.Empty, null);

            var imageUrl = mediaUrls.TryGetValue(moment.MediaAssetId, out var url) ? url : string.Empty;
            var momentReactions = reactionsByMoment.TryGetValue(moment.Id, out var list) ? list : [];

            result.Add(new LocketDto(
                moment.Id,
                author,
                imageUrl,
                moment.Caption,
                moment.AudienceType.ToString(),
                moment.CreatedAt,
                moment.ExpiresAt,
                moment.AuthorId == viewerId,
                momentReactions));
        }

        return result;
    }

    private static LocketAudience? ParseAudience(string? value) => value switch
    {
        "AllFriends" or null or "" => LocketAudience.AllFriends,
        "SelectedFriends" => LocketAudience.SelectedFriends,
        _ => null
    };
}
