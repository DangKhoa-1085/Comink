import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { friendshipsApi } from '../api/friendships'
import { Avatar } from '../components/social/Avatar'
import { UserCard } from '../components/social/UserCard'
import { Button } from '../components/ui/Button'
import { Alert, Skeleton } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { cn } from '../lib/utils'
import type { Friendship } from '../api/types'

type FriendsTab = 'incoming' | 'outgoing' | 'list'

/** Trang bạn bè: phân tab lời mời nhận được, lời mời đã gửi và danh sách bạn bè. */
export function FriendsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<FriendsTab>('list')

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
    void queryClient.invalidateQueries({ queryKey: ['friendships'] })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
    void queryClient.invalidateQueries({ queryKey: ['public-profile'] })
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

  const incoming = incomingQuery.data?.items ?? []
  const outgoing = outgoingQuery.data?.items ?? []
  const friends = friendsQuery.data?.items ?? []

  const tabs: { id: FriendsTab; label: string; count: number }[] = [
    { id: 'list', label: t('social.friends_list'), count: friends.length },
    { id: 'incoming', label: t('social.requests_incoming'), count: incoming.length },
    { id: 'outgoing', label: t('social.requests_outgoing'), count: outgoing.length },
  ]

  const renderRequestCard = (friendship: Friendship, isIncoming: boolean) => (
    <li
      key={friendship.id}
      className="flex flex-col justify-between gap-3 overflow-hidden rounded-2xl border border-slate-200/90 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-[#121826]"
    >
      <div className="flex min-w-0 items-center gap-3">
        <Avatar name={friendship.user.displayName} url={friendship.user.avatarUrl} size="md" />
        <div className="min-w-0 flex-1">
          <Link
            to={`/users/${friendship.user.id}`}
            className="block truncate text-sm font-bold text-slate-900 hover:text-sky-600 dark:text-slate-100 dark:hover:text-sky-400"
          >
            {friendship.user.displayName}
          </Link>
          <p className="truncate text-xs text-slate-500 dark:text-slate-400">
            @{friendship.user.userName}
          </p>
        </div>
      </div>

      <div className="border-t border-slate-100 pt-2.5 dark:border-slate-800">
        {isIncoming ? (
          <div className="grid grid-cols-2 gap-2">
            <Button
              size="sm"
              onClick={() => acceptMutation.mutate(friendship.id)}
              disabled={acceptMutation.isPending}
            >
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
            className="w-full"
            onClick={() => cancelMutation.mutate(friendship.id)}
            disabled={cancelMutation.isPending}
          >
            {t('social.cancel_request')}
          </Button>
        )}
      </div>
    </li>
  )

  const activeQuery =
    tab === 'incoming' ? incomingQuery : tab === 'outgoing' ? outgoingQuery : friendsQuery

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-xl font-bold text-slate-900 dark:text-slate-100">
          {t('social.friends')}
        </h1>
        <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
          {t('social.friends_subtitle')}
        </p>
      </div>

      <div className="flex items-center gap-1.5 overflow-x-auto pb-0.5">
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={cn(
              'shrink-0 whitespace-nowrap rounded-xl px-3.5 py-2 text-xs font-semibold transition-colors',
              tab === item.id
                ? 'bg-sky-600 text-white shadow-xs dark:bg-sky-500'
                : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-slate-800 dark:bg-[#121826] dark:text-slate-300 dark:hover:bg-slate-800',
            )}
          >
            {item.label} ({item.count})
          </button>
        ))}
      </div>

      {activeQuery.isLoading ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {[1, 2, 3, 4].map((item) => (
            <div
              key={item}
              className="space-y-3 rounded-2xl border border-slate-200/80 bg-white p-4 dark:border-slate-800 dark:bg-[#121826]"
            >
              <div className="flex items-center gap-3">
                <Skeleton className="h-10 w-10 rounded-full" />
                <div className="flex-1 space-y-1.5">
                  <Skeleton className="h-3.5 w-28" />
                  <Skeleton className="h-3 w-20" />
                </div>
              </div>
              <Skeleton className="h-8 w-full" />
            </div>
          ))}
        </div>
      ) : activeQuery.isError ? (
        <Alert variant="error">
          <p className="mb-2">{t('common.error_title')}</p>
          <Button variant="outline" size="sm" onClick={() => void activeQuery.refetch()}>
            {t('common.retry')}
          </Button>
        </Alert>
      ) : tab === 'incoming' ? (
        incoming.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
            {t('social.no_requests')}
          </p>
        ) : (
          <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {incoming.map((item) => renderRequestCard(item, true))}
          </ul>
        )
      ) : tab === 'outgoing' ? (
        outgoing.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
            {t('social.no_requests')}
          </p>
        ) : (
          <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {outgoing.map((item) => renderRequestCard(item, false))}
          </ul>
        )
      ) : friends.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
          {t('social.no_friends')}
        </p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {friends.map((friendship) => (
            <li key={friendship.id}>
              <UserCard user={friendship.user} onChange={invalidate} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
