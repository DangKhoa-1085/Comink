import { useTranslation } from 'react-i18next'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { UserCheck, UserMinus, UserPlus, UserRoundPlus } from 'lucide-react'
import { friendshipsApi } from '../../api/friendships'
import { followsApi } from '../../api/follows'
import type { Relationship } from '../../api/types'
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

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['search-users'] })
    queryClient.invalidateQueries({ queryKey: ['friendships'] })
    queryClient.invalidateQueries({ queryKey: ['public-profile'] })
    queryClient.invalidateQueries({ queryKey: ['feed'] })
    onChange?.()
  }

  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    toast.error(translateErrorCode(t, code))
  }

  const sendRequest = useMutation({
    mutationFn: () => friendshipsApi.sendRequest(userId),
    onSuccess: () => {
      toast.success(t('social.request_sent'))
      refresh()
    },
    onError: showError,
  })

  const acceptRequest = useMutation({
    mutationFn: () => friendshipsApi.accept(incomingRequestId ?? ''),
    onSuccess: () => {
      toast.success(t('social.request_accepted'))
      refresh()
    },
    onError: showError,
  })

  const removeFriend = useMutation({
    mutationFn: () => friendshipsApi.removeFriend(userId),
    onSuccess: () => {
      toast.success(t('social.friend_removed'))
      refresh()
    },
    onError: showError,
  })

  const toggleFollow = useMutation({
    mutationFn: () => (relationship.following ? followsApi.unfollow(userId) : followsApi.follow(userId)),
    onSuccess: () => {
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
