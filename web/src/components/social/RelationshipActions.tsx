import { useTranslation } from 'react-i18next'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { UserCheck, UserMinus, UserPlus, UserRoundPlus } from 'lucide-react'
import { friendshipsApi } from '../../api/friendships'
import { followsApi } from '../../api/follows'
import type { Friendship, PagedResult, PublicProfile, Relationship, UserSummary } from '../../api/types'
import { Button } from '../ui/Button'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'

/** Nút quan hệ: kết bạn / chấp nhận / hủy kết bạn và theo dõi. */
export function RelationshipActions({
  userId,
  relationship,
  incomingRequestId,
  onChange,
}: {
  userId: string
  relationship: Relationship
  incomingRequestId?: string
  onChange?: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  /** Cập nhật tức thì quan hệ trong cache Tìm kiếm, Hồ sơ công khai và Danh sách bạn bè. */
  const patchRelationshipInCaches = (updater: (current: Relationship) => Relationship) => {
    queryClient.setQueriesData<PagedResult<UserSummary>>({ queryKey: ['search-users'] }, (old) => {
      if (!old) return old
      return {
        ...old,
        items: old.items.map((item) =>
          item.id === userId ? { ...item, relationship: updater(item.relationship) } : item,
        ),
      }
    })

    queryClient.setQueryData<PublicProfile>(['public-profile', userId], (old) =>
      old ? { ...old, relationship: updater(old.relationship) } : old,
    )

    queryClient.setQueriesData<PagedResult<Friendship>>({ queryKey: ['friendships'] }, (old) => {
      if (!old) return old
      return {
        ...old,
        items: old.items.map((item) =>
          item.user.id === userId
            ? { ...item, user: { ...item.user, relationship: updater(item.user.relationship) } }
            : item,
        ),
      }
    })
  }

  const prependFriendshipIfMissing = (key: readonly string[], friendship: Friendship) => {
    queryClient.setQueryData<PagedResult<Friendship>>(key, (old) => {
      if (!old) return old
      const exists = old.items.some(
        (item) => item.id === friendship.id || item.user.id === friendship.user.id,
      )
      if (exists) {
        return {
          ...old,
          items: old.items.map((item) =>
            item.id === friendship.id || item.user.id === friendship.user.id ? friendship : item,
          ),
        }
      }
      return { ...old, items: [friendship, ...old.items] }
    })
  }

  const removeUserFromFriendshipCache = (key: readonly string[]) => {
    queryClient.setQueryData<PagedResult<Friendship>>(key, (old) => {
      if (!old) return old
      return { ...old, items: old.items.filter((item) => item.user.id !== userId) }
    })
  }

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['search-users'], refetchType: 'all' })
    void queryClient.invalidateQueries({ queryKey: ['friendships'], refetchType: 'all' })
    void queryClient.invalidateQueries({ queryKey: ['public-profile', userId], refetchType: 'all' })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
    onChange?.()
  }

  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    toast.error(translateErrorCode(t, code))
  }

  const sendRequest = useMutation({
    mutationFn: () => friendshipsApi.sendRequest(userId),
    onSuccess: (friendship) => {
      const nextRelation: Relationship['friendship'] =
        friendship.status === 'Accepted' ? 'Friends' : 'OutgoingPending'
      patchRelationshipInCaches((current) => ({ ...current, friendship: nextRelation }))

      if (friendship.status === 'Accepted') {
        removeUserFromFriendshipCache(['friendships', 'incoming'])
        removeUserFromFriendshipCache(['friendships', 'outgoing'])
        prependFriendshipIfMissing(['friendships', 'list'], friendship)
      } else {
        prependFriendshipIfMissing(['friendships', 'outgoing'], friendship)
      }

      toast.success(t('social.request_sent'))
      refresh()
    },
    onError: showError,
  })

  const acceptRequest = useMutation({
    mutationFn: () => friendshipsApi.accept(incomingRequestId ?? ''),
    onSuccess: (friendship) => {
      patchRelationshipInCaches((current) => ({ ...current, friendship: 'Friends' }))
      removeUserFromFriendshipCache(['friendships', 'incoming'])
      prependFriendshipIfMissing(['friendships', 'list'], friendship)
      toast.success(t('social.request_accepted'))
      refresh()
    },
    onError: showError,
  })

  const removeFriend = useMutation({
    mutationFn: () => friendshipsApi.removeFriend(userId),
    onSuccess: () => {
      patchRelationshipInCaches((current) => ({ ...current, friendship: 'None' }))
      removeUserFromFriendshipCache(['friendships', 'list'])
      toast.success(t('social.friend_removed'))
      refresh()
    },
    onError: showError,
  })

  const toggleFollow = useMutation({
    mutationFn: () => (relationship.following ? followsApi.unfollow(userId) : followsApi.follow(userId)),
    onSuccess: () => {
      const nextFollowing = !relationship.following
      patchRelationshipInCaches((current) => ({ ...current, following: nextFollowing }))
      toast.success(relationship.following ? t('social.unfollowed_toast') : t('social.following_toast'))
      refresh()
    },
    onError: showError,
  })

  const busy = sendRequest.isPending || acceptRequest.isPending || removeFriend.isPending || toggleFollow.isPending

  return (
    <div className="flex shrink-0 items-center gap-1.5">
      {relationship.friendship === 'Friends' ? (
        <Button variant="outline" size="sm" onClick={() => removeFriend.mutate()} disabled={busy}>
          <UserMinus className="h-4 w-4" />
          {t('social.friend_remove')}
        </Button>
      ) : relationship.friendship === 'IncomingPending' ? (
        <Button size="sm" onClick={() => acceptRequest.mutate()} disabled={busy || !incomingRequestId}>
          <UserCheck className="h-4 w-4" />
          {t('social.friend_accept')}
        </Button>
      ) : relationship.friendship === 'OutgoingPending' ? (
        <Button variant="outline" size="sm" disabled>
          <UserRoundPlus className="h-4 w-4" />
          {t('social.friend_pending_out')}
        </Button>
      ) : (
        <Button size="sm" onClick={() => sendRequest.mutate()} disabled={busy}>
          <UserPlus className="h-4 w-4" />
          {t('social.friend_add')}
        </Button>
      )}

      <Button variant="ghost" size="sm" onClick={() => toggleFollow.mutate()} disabled={busy}>
        {relationship.following ? t('social.unfollow') : t('social.follow')}
      </Button>
    </div>
  )
}
