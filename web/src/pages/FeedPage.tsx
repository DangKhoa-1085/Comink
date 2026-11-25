import { useTranslation } from 'react-i18next'
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query'
import { postsApi } from '../api/posts'
import { PostCard } from '../components/social/PostCard'
import { PostComposer } from '../components/social/PostComposer'
import { Alert, Spinner } from '../components/ui/Feedback'
import { useInfiniteScroll } from '../hooks/useInfiniteScroll'

/** Bảng tin: bài của bạn bè, người đang theo dõi và của chính mình. */
export function FeedPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const feedQuery = useInfiniteQuery({
    queryKey: ['feed'],
    queryFn: ({ pageParam }) => postsApi.feed(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
  })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['feed'] })
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

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-xl font-bold">{t('social.feed')}</h1>

      <PostComposer onPosted={refresh} />

      {feedQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
          <Spinner /> {t('social.loading')}
        </div>
      ) : feedQuery.isError ? (
        <Alert variant="error">
          <p className="mb-2">{t('social.feed_error')}</p>
          <button type="button" className="underline" onClick={() => void feedQuery.refetch()}>
            {t('common.retry')}
          </button>
        </Alert>
      ) : posts.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700">
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
              className="mx-auto block text-sm text-indigo-600 hover:underline disabled:opacity-60"
              onClick={() => void feedQuery.fetchNextPage()}
              disabled={feedQuery.isFetchingNextPage}
            >
              {feedQuery.isFetchingNextPage ? t('social.loading') : t('social.load_more')}
            </button>
          )}
        </>
      )}
    </div>
  )
}
