import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient, type InfiniteData } from '@tanstack/react-query'
import {
  AtSign,
  Bell,
  MessageCircle,
  PhoneMissed,
  Share2,
  ThumbsUp,
  UserPlus,
  Users,
  Video,
} from 'lucide-react'
import {
  notificationsApi,
  parseNotificationData,
  type NotificationItem,
} from '../../api/notifications'
import type { ConversationSummary } from '../../api/conversations'
import type { Friendship, PagedResult } from '../../api/types'
import { timeAgo } from '../../lib/format'
import { cn } from '../../lib/utils'
import { Avatar } from '../social/Avatar'

interface ResolvedActor {
  displayName: string | null
  avatarUrl: string | null
}

/**
 * Tra nhanh tên/avatar từ dữ liệu backend hoặc cache hội thoại/bạn bè đã tải sẵn
 * (không gọi thêm API).
 */
function resolveActor(
  queryClient: ReturnType<typeof useQueryClient>,
  item: NotificationItem,
): ResolvedActor {
  if (item.actorDisplayName) {
    return { displayName: item.actorDisplayName, avatarUrl: item.actorAvatarUrl }
  }
  if (!item.actorId) {
    return { displayName: null, avatarUrl: item.actorAvatarUrl }
  }

  const conversations =
    queryClient.getQueryData<InfiniteData<PagedResult<ConversationSummary>>>(['conversations'])
  for (const page of conversations?.pages ?? []) {
    for (const conv of page.items) {
      if (conv.peer?.userId === item.actorId) {
        return {
          displayName: conv.peer.displayName || conv.peer.userName,
          avatarUrl: conv.peer.avatarUrl,
        }
      }
    }
  }

  for (const key of [
    ['friendships', 'friends'],
    ['friendships', 'incoming'],
    ['friendships', 'outgoing'],
  ]) {
    const list = queryClient.getQueryData<PagedResult<Friendship>>(key)
    for (const friendship of list?.items ?? []) {
      if (friendship.user.id === item.actorId) {
        return {
          displayName: friendship.user.displayName,
          avatarUrl: friendship.user.avatarUrl,
        }
      }
    }
  }

  return { displayName: null, avatarUrl: item.actorAvatarUrl }
}

/** Một dòng thông báo kiểu Facebook: avatar + huy hiệu loại ở góc + nội dung in đậm tên + chấm chưa đọc. */
export function NotificationItemRow({
  item,
  onNavigate,
}: {
  item: NotificationItem
  onNavigate?: () => void
}) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const payload = parseNotificationData(item.data)
  const actor = resolveActor(queryClient, item)

  const badgeSpec = (() => {
    switch (item.type) {
      case 'MissedCall':
        return {
          Icon: payload.callType === 'Video' ? Video : PhoneMissed,
          bg: 'bg-rose-500',
        }
      case 'FriendRequestReceived':
        return { Icon: UserPlus, bg: 'bg-sky-600' }
      case 'FriendRequestAccepted':
        return { Icon: Users, bg: 'bg-emerald-500' }
      case 'NewFollower':
        return { Icon: UserPlus, bg: 'bg-sky-500' }
      case 'PostLiked':
        return { Icon: ThumbsUp, bg: 'bg-sky-500' }
      case 'PostCommented':
      case 'CommentReplied':
        return { Icon: MessageCircle, bg: 'bg-emerald-500' }
      case 'PostShared':
        return { Icon: Share2, bg: 'bg-sky-600' }
      case 'Mentioned':
        return { Icon: AtSign, bg: 'bg-sky-500' }
      case 'AddedToGroup':
        return { Icon: Users, bg: 'bg-sky-600' }
      default:
        return { Icon: Bell, bg: 'bg-sky-600' }
    }
  })()

  const actionText = (() => {
    switch (item.type) {
      case 'MissedCall':
        return payload.callType === 'Video'
          ? t('notifications.action_missed_video_call')
          : t('notifications.action_missed_call')
      case 'FriendRequestReceived':
        return t('notifications.action_friend_request')
      case 'FriendRequestAccepted':
        return t('notifications.action_friend_accepted')
      case 'NewFollower':
        return t('notifications.action_new_follower')
      case 'PostLiked':
        return t('notifications.action_post_liked')
      case 'PostCommented':
        return t('notifications.action_post_commented')
      case 'CommentReplied':
        return t('notifications.action_comment_replied')
      case 'PostShared':
        return t('notifications.action_post_shared')
      case 'Mentioned':
        return t('notifications.action_mentioned')
      case 'AddedToGroup':
        return t('notifications.action_added_to_group')
      default:
        return t('notifications.action_other')
    }
  })()

  const fallbackTitle = (() => {
    switch (item.type) {
      case 'MissedCall':
        return t('notifications.fallback_missed_call')
      case 'FriendRequestReceived':
        return t('notifications.fallback_friend_request')
      case 'FriendRequestAccepted':
        return t('notifications.fallback_friend_accepted')
      default:
        return t('notifications.fallback_other')
    }
  })()

  const handleClick = async () => {
    if (!item.isRead) {
      try {
        await notificationsApi.markRead(item.id)
        void queryClient.invalidateQueries({ queryKey: ['notifications'] })
      } catch {
        // Vẫn cho phép điều hướng ngay cả khi đánh dấu đọc lỗi.
      }
    }

    onNavigate?.()

    switch (item.type) {
      case 'FriendRequestReceived':
        navigate('/friends?tab=incoming')
        return
      case 'FriendRequestAccepted':
        navigate('/friends?tab=list')
        return
      case 'NewFollower':
        if (item.actorId) {
          navigate(`/users/${item.actorId}`)
          return
        }
        navigate('/friends')
        return
      case 'MissedCall':
      case 'AddedToGroup':
        if (payload.conversationId) {
          navigate(`/messages/${payload.conversationId}`)
          return
        }
        navigate('/messages')
        return
      default:
        if (payload.conversationId) {
          navigate(`/messages/${payload.conversationId}`)
          return
        }
        navigate('/')
    }
  }

  const BadgeIcon = badgeSpec.Icon

  return (
    <button
      type="button"
      onClick={() => void handleClick()}
      className={cn(
        'flex w-full items-center gap-3 rounded-xl px-2.5 py-2.5 text-left transition-colors',
        item.isRead
          ? 'hover:bg-slate-100/90 dark:hover:bg-slate-800/70'
          : 'bg-sky-50/70 hover:bg-sky-100/80 dark:bg-sky-500/10 dark:hover:bg-sky-500/15',
      )}
    >
      <div className="relative shrink-0">
        {actor.displayName ? (
          <Avatar
            name={actor.displayName}
            url={actor.avatarUrl}
            size="md"
            className="h-12 w-12 text-base"
          />
        ) : (
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
            <BadgeIcon className="h-5 w-5" />
          </span>
        )}
        <span
          className={cn(
            'absolute -bottom-0.5 -right-0.5 flex h-5 w-5 items-center justify-center rounded-full text-white ring-2 ring-white dark:ring-[#121826]',
            badgeSpec.bg,
          )}
        >
          <BadgeIcon className="h-3 w-3" />
        </span>
      </div>

      <div className="min-w-0 flex-1">
        <p className="line-clamp-3 text-[13.5px] leading-snug text-slate-700 dark:text-slate-200">
          {actor.displayName ? (
            <>
              <span className="font-bold text-slate-900 dark:text-slate-50">
                {actor.displayName}
              </span>{' '}
              <span>{actionText}</span>
            </>
          ) : (
            <span className="font-semibold text-slate-900 dark:text-slate-50">
              {fallbackTitle}
            </span>
          )}
        </p>
        <p
          className={cn(
            'mt-1 text-xs',
            item.isRead
              ? 'text-slate-500 dark:text-slate-400'
              : 'font-semibold text-sky-600 dark:text-sky-400',
          )}
        >
          {timeAgo(item.createdAt, i18n.language)}
        </p>
      </div>

      {!item.isRead && (
        <span
          aria-hidden="true"
          className="h-2.5 w-2.5 shrink-0 rounded-full bg-sky-500 dark:bg-sky-400"
        />
      )}
    </button>
  )
}
