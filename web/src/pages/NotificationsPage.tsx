import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Bell, CheckCheck, RefreshCw } from 'lucide-react'
import { notificationsApi, type NotificationItem } from '../api/notifications'
import { NotificationItemRow } from '../components/notifications/NotificationItemRow'
import { Button } from '../components/ui/Button'
import { Alert, Card, Skeleton } from '../components/ui/Feedback'
import { cn } from '../lib/utils'

type FilterMode = 'all' | 'unread'
const RECENT_MS = 24 * 60 * 60 * 1000

/** Trang xem toàn bộ thông báo với bộ lọc Tất cả / Chưa đọc và phân trang cursor. */
export function NotificationsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [filter, setFilter] = useState<FilterMode>('all')
  const [nowMs] = useState(() => Date.now())

  const query = useInfiniteQuery({
    queryKey: ['notifications', 'page'],
    queryFn: ({ pageParam }) => notificationsApi.list(pageParam, 20),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
  })

  const items = query.data?.pages.flatMap((page) => page.items) ?? []
  const unreadItems = items.filter((item) => !item.isRead)

  const markAllMutation = useMutation({
    mutationFn: async (targets: NotificationItem[]) => {
      await Promise.all(targets.map((item) => notificationsApi.markRead(item.id)))
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })

  const filtered = filter === 'unread' ? unreadItems : items
  const recentItems: NotificationItem[] = []
  const earlierItems: NotificationItem[] = []
  for (const item of filtered) {
    if (!item.isRead || nowMs - new Date(item.createdAt).getTime() < RECENT_MS) {
      recentItems.push(item)
    } else {
      earlierItems.push(item)
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <Card className="p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="text-2xl font-extrabold text-slate-900 dark:text-slate-50">
            {t('notifications.title')}
          </h1>
          <div className="flex items-center gap-2">
            {unreadItems.length > 0 && (
              <Button
                variant="soft"
                size="sm"
                onClick={() => markAllMutation.mutate(unreadItems)}
                disabled={markAllMutation.isPending}
              >
                <CheckCheck className="h-4 w-4" />
                {t('notifications.mark_all_read')}
              </Button>
            )}
            <Button
              variant="outline"
              size="sm"
              onClick={() => void query.refetch()}
              title={t('notifications.refresh')}
              aria-label={t('notifications.refresh')}
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="mt-3 flex items-center gap-2">
          <button
            type="button"
            onClick={() => setFilter('all')}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-bold transition-colors',
              filter === 'all'
                ? 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
                : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
            )}
          >
            {t('notifications.filter_all')}
          </button>
          <button
            type="button"
            onClick={() => setFilter('unread')}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-bold transition-colors',
              filter === 'unread'
                ? 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
                : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
            )}
          >
            {t('notifications.filter_unread')}
          </button>
        </div>

        <div className="mt-4">
          {query.isLoading ? (
            <div className="space-y-3 py-2">
              {[1, 2, 3, 4].map((row) => (
                <div key={row} className="flex items-center gap-3">
                  <Skeleton className="h-12 w-12 rounded-full" />
                  <div className="flex-1 space-y-2">
                    <Skeleton className="h-3.5 w-4/5" />
                    <Skeleton className="h-3 w-24" />
                  </div>
                </div>
              ))}
            </div>
          ) : query.isError ? (
            <Alert variant="error">
              <p className="mb-2">{t('notifications.error')}</p>
              <Button variant="outline" size="sm" onClick={() => void query.refetch()}>
                {t('common.retry')}
              </Button>
            </Alert>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-2 py-12 text-center text-sm text-slate-500 dark:text-slate-400">
              <Bell className="h-9 w-9 opacity-40" />
              <p>
                {filter === 'unread' ? t('notifications.empty_unread') : t('notifications.empty')}
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {recentItems.length > 0 && (
                <section className="space-y-1">
                  <h2 className="px-2 text-sm font-bold text-slate-800 dark:text-slate-100">
                    {t('notifications.section_new')}
                  </h2>
                  {recentItems.map((item) => (
                    <NotificationItemRow key={item.id} item={item} />
                  ))}
                </section>
              )}

              {earlierItems.length > 0 && (
                <section className="space-y-1">
                  <h2 className="px-2 text-sm font-bold text-slate-800 dark:text-slate-100">
                    {t('notifications.section_earlier')}
                  </h2>
                  {earlierItems.map((item) => (
                    <NotificationItemRow key={item.id} item={item} />
                  ))}
                </section>
              )}

              {query.hasNextPage && (
                <div className="pt-2 text-center">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => void query.fetchNextPage()}
                    disabled={query.isFetchingNextPage}
                  >
                    {query.isFetchingNextPage ? t('common.loading') : t('notifications.load_more')}
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
      </Card>
    </div>
  )
}
