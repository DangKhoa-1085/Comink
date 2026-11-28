import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MessageSquare } from 'lucide-react'
import { conversationsApi } from '../api/conversations'
import { friendshipsApi } from '../api/friendships'
import { postsApi } from '../api/posts'
import { PresenceDot } from '../components/chat/PresenceDot'
import { Avatar } from '../components/social/Avatar'
import { LocketWidget } from '../components/social/LocketWidget'
import { PostCard } from '../components/social/PostCard'
import { PostComposer } from '../components/social/PostComposer'
import { Button } from '../components/ui/Button'
import { Alert, Skeleton } from '../components/ui/Feedback'
import { useInfiniteScroll } from '../hooks/useInfiniteScroll'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { toast } from '../stores/toastStore'

/** Bảng tin: cột chính chứa bài viết + cột phải (Desktop) hiển thị bạn đang online và lời mời kết bạn. */
export function FeedPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const feedQuery = useInfiniteQuery({
    queryKey: ['feed'],
    queryFn: ({ pageParam }) => postsApi.feed(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
  })

  // Tận dụng cache danh sách hội thoại & lời mời kết bạn cho cột phải (chỉ hiển thị dữ liệu thật).
  const conversationsQuery = useInfiniteQuery({
    queryKey: ['conversations'],
    queryFn: ({ pageParam }) => conversationsApi.list(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    staleTime: 30_000,
  })

  const incomingQuery = useQuery({
    queryKey: ['friendships', 'incoming'],
    queryFn: () => friendshipsApi.incoming(),
    staleTime: 15_000,
  })

  const invalidateSocial = () => {
    void queryClient.invalidateQueries({ queryKey: ['friendships'] })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
    void queryClient.invalidateQueries({ queryKey: ['public-profile'] })
  }

  const acceptMutation = useMutation({
    mutationFn: (id: string) => friendshipsApi.accept(id),
    onSuccess: () => {
      toast.success(t('social.request_accepted'))
      invalidateSocial()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const declineMutation = useMutation({
    mutationFn: (id: string) => friendshipsApi.removeRequest(id),
    onSuccess: () => {
      toast.success(t('social.request_declined'))
      invalidateSocial()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
  }

  const sentinelRef = useInfiniteScroll(
    () => {
      if (feedQuery.hasNextPage && !feedQuery.isFetchingNextPage) {
        void feedQuery.fetchNextPage()
      }
    },
    Boolean(feedQuery.hasNextPage),
  )

  const posts = feedQuery.data?.pages.flatMap((page) => page.items) ?? []

  // Lọc các hội thoại 1–1 có bạn đang trực tuyến để hiện trên cột phải.
  const onlinePeers = (conversationsQuery.data?.pages ?? [])
    .flatMap((page) => page.items)
    .filter((conv) => conv.type === 'Direct' && conv.peer?.isOnline)
    .slice(0, 6)

  const incomingItems = (incomingQuery.data?.items ?? []).slice(0, 3)

  return (
    <div className="flex items-start justify-center gap-6">
      <div className="w-full max-w-2xl">
        <div className="mb-4 xl:hidden">
          <LocketWidget />
        </div>

        <PostComposer onPosted={refresh} />

        {feedQuery.isLoading ? (
          <div className="space-y-4">
            {[1, 2].map((item) => (
              <div
                key={item}
                className="space-y-3 rounded-2xl border border-slate-200/80 bg-white p-5 dark:border-slate-800 dark:bg-[#121826]"
              >
                <div className="flex items-center gap-3">
                  <Skeleton className="h-10 w-10 rounded-full" />
                  <div className="flex-1 space-y-1.5">
                    <Skeleton className="h-3.5 w-32" />
                    <Skeleton className="h-3 w-24" />
                  </div>
                </div>
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-4/5" />
                <Skeleton className="h-40 w-full" />
              </div>
            ))}
          </div>
        ) : feedQuery.isError ? (
          <Alert variant="error">
            <p className="mb-2">{t('social.feed_error')}</p>
            <Button variant="outline" size="sm" onClick={() => void feedQuery.refetch()}>
              {t('common.retry')}
            </Button>
          </Alert>
        ) : posts.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
            {t('social.empty_feed')}
          </p>
        ) : (
          <>
            {posts.map((post) => (
              <PostCard key={post.id} post={post} onChange={refresh} />
            ))}

            <div ref={sentinelRef} className="h-8" />

            {feedQuery.hasNextPage && (
              <button
                type="button"
                className="mx-auto block text-sm font-semibold text-sky-600 hover:underline disabled:opacity-60 dark:text-sky-400"
                onClick={() => void feedQuery.fetchNextPage()}
                disabled={feedQuery.isFetchingNextPage}
              >
                {feedQuery.isFetchingNextPage ? t('social.loading') : t('social.load_more')}
              </button>
            )}
          </>
        )}
      </div>

      {/* Cột phải (Desktop >= xl): Comink Locket trên đầu + Bạn bè đang hoạt động + Lời mời kết bạn */}
      <aside className="sticky top-6 hidden w-[300px] shrink-0 space-y-4 xl:block">
        <LocketWidget />

        <section className="space-y-3 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-[#121826]">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {t('social.online_friends')}
            </h2>
            {onlinePeers.length > 0 && (
              <span className="text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                {onlinePeers.length} online
              </span>
            )}
          </div>

          {onlinePeers.length === 0 ? (
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {t('social.online_friends_empty')}
            </p>
          ) : (
            <ul className="space-y-2.5">
              {onlinePeers.map((conv) => {
                const name = conv.peer?.displayName || conv.peer?.userName || t('chat.title')
                return (
                  <li key={conv.id} className="flex items-center justify-between gap-2">
                    <Link
                      to={conv.peer ? `/users/${conv.peer.userId}` : `/messages/${conv.id}`}
                      className="flex min-w-0 flex-1 items-center gap-2.5"
                    >
                      <span className="relative shrink-0">
                        <Avatar name={name} url={conv.peer?.avatarUrl} size="sm" />
                        <PresenceDot
                          online
                          label={t('chat.online')}
                          className="absolute -bottom-0.5 -right-0.5"
                        />
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-xs font-semibold text-slate-800 dark:text-slate-100">
                          {name}
                        </p>
                        <p className="truncate text-xs text-slate-400">{t('chat.online')}</p>
                      </div>
                    </Link>
                    <button
                      type="button"
                      onClick={() => navigate(`/messages/${conv.id}`)}
                      className="shrink-0 rounded-lg bg-slate-100 p-1.5 text-slate-500 transition-colors hover:bg-sky-50 hover:text-sky-600 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-sky-500/20 dark:hover:text-sky-300"
                      title={t('chat.open')}
                      aria-label={t('chat.open')}
                    >
                      <MessageSquare className="h-3.5 w-3.5" />
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </section>

        <section className="space-y-3 rounded-2xl border border-slate-200/90 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-[#121826]">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {t('social.requests_incoming')}
            </h2>
            <Link
              to="/friends"
              className="text-xs font-semibold text-sky-600 hover:underline dark:text-sky-400"
            >
              {t('social.view_all')}
            </Link>
          </div>

          {incomingItems.length === 0 ? (
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {t('social.no_requests')}
            </p>
          ) : (
            <ul className="space-y-2.5">
              {incomingItems.map((req) => (
                <li
                  key={req.id}
                  className="space-y-2.5 rounded-xl border border-slate-100 bg-slate-50/80 p-3 dark:border-slate-800 dark:bg-[#1A2234]"
                >
                  <Link to={`/users/${req.user.id}`} className="flex items-center gap-2.5">
                    <Avatar name={req.user.displayName} url={req.user.avatarUrl} size="sm" />
                    <div className="min-w-0">
                      <p className="truncate text-xs font-bold text-slate-800 dark:text-slate-100">
                        {req.user.displayName}
                      </p>
                      <p className="truncate text-xs text-slate-400">@{req.user.userName}</p>
                    </div>
                  </Link>
                  <div className="grid grid-cols-2 gap-1.5">
                    <Button
                      size="sm"
                      onClick={() => acceptMutation.mutate(req.id)}
                      disabled={acceptMutation.isPending}
                    >
                      {t('social.accept')}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => declineMutation.mutate(req.id)}
                      disabled={declineMutation.isPending}
                    >
                      {t('social.decline')}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </aside>
    </div>
  )
}
