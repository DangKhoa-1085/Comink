import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { postsApi } from '../api/posts'
import { usersApi } from '../api/users'
import { Avatar } from '../components/social/Avatar'
import { PostCard } from '../components/social/PostCard'
import { RelationshipActions } from '../components/social/RelationshipActions'
import { Alert, Spinner } from '../components/ui/Feedback'
import { useInfiniteScroll } from '../hooks/useInfiniteScroll'
import { useIncomingRequestIds } from '../hooks/useIncomingRequestIds'
import { useAuthStore } from '../stores/authStore'
import { timeAgo } from '../lib/format'

/** Trang cá nhân công khai: hồ sơ, số liệu và bài viết (lọc theo quyền xem ở backend). */
export function UserProfilePage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const me = useAuthStore((state) => state.user)
  const incomingRequests = useIncomingRequestIds()

  const profileQuery = useQuery({
    queryKey: ['public-profile', id],
    queryFn: () => usersApi.publicProfile(id!),
    enabled: Boolean(id),
  })

  const postsQuery = useInfiniteQuery({
    queryKey: ['user-posts', id],
    queryFn: ({ pageParam }) => postsApi.userPosts(id!, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    enabled: Boolean(id),
  })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['user-posts', id] })
    queryClient.invalidateQueries({ queryKey: ['feed'] })
  }

  const sentinelRef = useInfiniteScroll(
    () => {
      if (postsQuery.hasNextPage && !postsQuery.isFetchingNextPage) {
        void postsQuery.fetchNextPage()
      }
    },
    Boolean(postsQuery.hasNextPage),
  )

  if (profileQuery.isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
        <Spinner /> {t('social.loading')}
      </div>
    )
  }

  if (profileQuery.isError || !profileQuery.data) {
    return <Alert variant="error">{t('social.profile_not_found')}</Alert>
  }

  const profile = profileQuery.data
  const isSelf = me?.id === profile.id
  const posts = postsQuery.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <div className="mx-auto max-w-2xl">
      <section className="mb-5 rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex items-start gap-4">
          <Avatar name={profile.displayName} url={profile.avatarUrl} size="lg" />
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-xl font-bold">{profile.displayName}</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">@{profile.userName}</p>
            {profile.bio && <p className="mt-2 text-sm text-slate-700 dark:text-slate-200">{profile.bio}</p>}
            <p className="mt-1 text-xs text-slate-400">
              {t('social.joined')}: {timeAgo(profile.createdAt, i18n.language)}
            </p>

            <dl className="mt-3 flex gap-5 text-sm">
              <div>
                <dt className="text-xs text-slate-500">{t('social.stat_friends')}</dt>
                <dd className="font-bold">{profile.friendCount}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">{t('social.stat_followers')}</dt>
                <dd className="font-bold">{profile.followerCount}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">{t('social.stat_following')}</dt>
                <dd className="font-bold">{profile.followingCount}</dd>
              </div>
            </dl>
          </div>

          <div className="shrink-0">
            {isSelf ? (
              <Link to="/profile" className="text-sm font-medium text-indigo-600 hover:underline">
                {t('social.edit_profile')}
              </Link>
            ) : (
              <RelationshipActions
                userId={profile.id}
                relationship={profile.relationship}
                incomingRequestId={incomingRequests.get(profile.id)}
                onChange={() => void profileQuery.refetch()}
              />
            )}
          </div>
        </div>
      </section>

      <h2 className="mb-3 text-sm font-bold text-slate-600 dark:text-slate-300">{t('social.posts')}</h2>

      {postsQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
          <Spinner /> {t('social.loading')}
        </div>
      ) : postsQuery.isError ? (
        <Alert variant="error">
          <p className="mb-2">{t('social.feed_error')}</p>
          <button type="button" className="underline" onClick={() => void postsQuery.refetch()}>
            {t('common.retry')}
          </button>
        </Alert>
      ) : posts.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500 dark:border-slate-700">
          {t('social.no_posts')}
        </p>
      ) : (
        <>
          {posts.map((post) => (
            <PostCard key={post.id} post={post} onChange={refresh} />
          ))}
          <div ref={sentinelRef} className="h-8" />
        </>
      )}
    </div>
  )
}
