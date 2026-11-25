import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { friendshipsApi } from '../api/friendships'
import { Avatar } from '../components/social/Avatar'
import { UserCard } from '../components/social/UserCard'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import type { Friendship } from '../api/types'

/** Trang bạn bè: lời mời nhận được, lời mời đã gửi và danh sách bạn bè. */
export function FriendsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const incomingQuery = useQuery({
    queryKey: ['friendships', 'incoming'],
    queryFn: () => friendshipsApi.incoming(),
  })
  const outgoingQuery = useQuery({
    queryKey: ['friendships', 'outgoing'],
    queryFn: () => friendshipsApi.outgoing(),
  })
  const friendsQuery = useQuery({
    queryKey: ['friendships', 'list'],
    queryFn: () => friendshipsApi.list(),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['friendships'] })
    queryClient.invalidateQueries({ queryKey: ['feed'] })
    queryClient.invalidateQueries({ queryKey: ['public-profile'] })
  }

  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    toast.error(translateErrorCode(t, code))
  }

  const acceptMutation = useMutation({
    mutationFn: (id: string) => friendshipsApi.accept(id),
    onSuccess: () => {
      toast.success(t('social.request_accepted'))
      invalidate()
    },
    onError: showError,
  })

  const declineMutation = useMutation({
    mutationFn: (id: string) => friendshipsApi.removeRequest(id),
    onSuccess: () => {
      toast.success(t('social.request_declined'))
      invalidate()
    },
    onError: showError,
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => friendshipsApi.removeRequest(id),
    onSuccess: () => {
      toast.success(t('social.request_cancelled'))
      invalidate()
    },
    onError: showError,
  })

  const renderRequestRow = (friendship: Friendship, incoming: boolean) => (
    <li
      key={friendship.id}
      className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900"
    >
      <Avatar name={friendship.user.displayName} url={friendship.user.avatarUrl} size="md" />
      <div className="min-w-0 flex-1">
        <Link
          to={`/users/${friendship.user.id}`}
          className="block truncate text-sm font-semibold hover:text-indigo-600"
        >
          {friendship.user.displayName}
        </Link>
        <p className="truncate text-xs text-slate-500 dark:text-slate-400">@{friendship.user.userName}</p>
      </div>
      {incoming ? (
        <div className="flex gap-1.5">
          <Button size="sm" onClick={() => acceptMutation.mutate(friendship.id)} disabled={acceptMutation.isPending}>
            {t('social.accept')}
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => declineMutation.mutate(friendship.id)}
            disabled={declineMutation.isPending}
          >
            {t('social.decline')}
          </Button>
        </div>
      ) : (
        <Button
          variant="outline"
          size="sm"
          onClick={() => cancelMutation.mutate(friendship.id)}
          disabled={cancelMutation.isPending}
        >
          {t('social.cancel_request')}
        </Button>
      )}
    </li>
  )

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <h1 className="text-xl font-bold">{t('social.friends')}</h1>

      <section>
        <h2 className="mb-2 text-sm font-bold text-slate-600 dark:text-slate-300">
          {t('social.requests_incoming')}
        </h2>
        {incomingQuery.isLoading ? (
          <div className="flex gap-2 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : incomingQuery.isError ? (
          <Alert variant="error">{t('common.error_title')}</Alert>
        ) : (incomingQuery.data?.items.length ?? 0) === 0 ? (
          <p className="text-sm text-slate-500 dark:text-slate-400">{t('social.no_requests')}</p>
        ) : (
          <ul className="space-y-2">{incomingQuery.data?.items.map((item) => renderRequestRow(item, true))}</ul>
        )}
      </section>

      <section>
        <h2 className="mb-2 text-sm font-bold text-slate-600 dark:text-slate-300">
          {t('social.requests_outgoing')}
        </h2>
        {outgoingQuery.isLoading ? (
          <div className="flex gap-2 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : outgoingQuery.isError ? (
          <Alert variant="error">{t('common.error_title')}</Alert>
        ) : (outgoingQuery.data?.items.length ?? 0) === 0 ? (
          <p className="text-sm text-slate-500 dark:text-slate-400">{t('social.no_requests')}</p>
        ) : (
          <ul className="space-y-2">{outgoingQuery.data?.items.map((item) => renderRequestRow(item, false))}</ul>
        )}
      </section>

      <section>
        <h2 className="mb-2 text-sm font-bold text-slate-600 dark:text-slate-300">{t('social.friends_list')}</h2>
        {friendsQuery.isLoading ? (
          <div className="flex gap-2 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : friendsQuery.isError ? (
          <Alert variant="error">{t('common.error_title')}</Alert>
        ) : (friendsQuery.data?.items.length ?? 0) === 0 ? (
          <p className="text-sm text-slate-500 dark:text-slate-400">{t('social.no_friends')}</p>
        ) : (
          <ul className="space-y-2">
            {friendsQuery.data?.items.map((friendship) => (
              <li key={friendship.id}>
                <UserCard user={friendship.user} onChange={invalidate} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
