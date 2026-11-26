import { useTranslation } from 'react-i18next'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MessageCircle } from 'lucide-react'
import { conversationsApi } from '../api/conversations'
import { postsApi } from '../api/posts'
import { usersApi } from '../api/users'
import { Avatar } from '../components/social/Avatar'
import { PostCard } from '../components/social/PostCard'
import { RelationshipActions } from '../components/social/RelationshipActions'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { useInfiniteScroll } from '../hooks/useInfiniteScroll'
import { useIncomingRequestIds } from '../hooks/useIncomingRequestIds'
import { useAuthStore } from '../stores/authStore'
import { timeAgo } from '../lib/format'

/** Trang cá nhân công khai: hồ sơ, số liệu và bài viết (lọc theo quyền xem ở backend). */
export function UserProfilePage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
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

  // Mở (hoặc lấy lại) hội thoại 1–1 với người này rồi chuyển sang khung chat.
  // Backend chỉ cho bạn bè mở hội thoại → hiển thị lỗi MESSAGING_NOT_FRIENDS qua toast.
  const openChat = useMutation({
    mutationFn: () => conversationsApi.openDirect(id!),
    onSuccess: (conversation) => {
      void queryClient.invalidateQueries({ queryKey: ['conversations'] })
      navigate(`/messages/${conversation.id}`)
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['user-posts', id] })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
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
        <Spinner className="text-sky-600 dark:text-sky-400" /> {t('social.loading')}
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
    <div className="mx-auto max-w-2xl space-y-5">
      <section className="overflow-hidden rounded-2xl border border-slate-200/90 bg-white shadow-xs dark:border-slate-800 dark:bg-[#121826]">
        <div className="relative h-36 bg-gradient-to-r from-sky-700 via-sky-500 to-cyan-400 sm:h-44">
          {profile.coverUrl && (
            <img
              src={profile.coverUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          )}
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/40 via-transparent to-black/10" />
          <span className="absolute bottom-3 right-4 rounded-full bg-black/35 px-3 py-1 text-xs font-semibold text-white backdrop-blur-xs">
            {t('social.joined')}: {timeAgo(profile.createdAt, i18n.language)}
          </span>
        </div>

        <div className="px-5 pb-5">
          <div className="relative z-10 -mt-10 flex flex-wrap items-end justify-between gap-3">
            <div className="flex min-w-0 items-end gap-3.5">
              <div className="shrink-0 rounded-full bg-white p-1 ring-2 ring-white dark:bg-[#121826] dark:ring-[#121826]">
                <Avatar name={profile.displayName} url={profile.avatarUrl} size="lg" />
              </div>
              <div className="min-w-0 pt-11">
                <h1 className="truncate text-xl font-bold text-slate-900 dark:text-slate-100">
                  {profile.displayName}
                </h1>
                <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                  @{profile.userName}
                </p>
              </div>
            </div>

            <div className="flex shrink-0 flex-wrap items-center gap-2">
              {isSelf ? (
                <Link
                  to="/profile"
                  className="rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2 text-xs font-semibold text-sky-600 hover:bg-slate-100 dark:border-slate-700 dark:bg-[#1A2234] dark:text-sky-400"
                >
                  {t('social.edit_profile')}
                </Link>
              ) : (
                <>
                  <RelationshipActions
                    userId={profile.id}
                    relationship={profile.relationship}
                    incomingRequestId={incomingRequests.get(profile.id)}
                    onChange={() => void profileQuery.refetch()}
                  />
                  <Button
                    variant="soft"
                    size="sm"
                    onClick={() => openChat.mutate()}
                    disabled={openChat.isPending}
                    title={t('chat.open')}
                  >
                    <MessageCircle className="h-4 w-4" />
                    {t('chat.open')}
                  </Button>
                </>
              )}
            </div>
          </div>

          {profile.bio && (
            <p className="mt-3 text-sm leading-relaxed text-slate-700 dark:text-slate-200">
              {profile.bio}
            </p>
          )}

          <dl className="mt-4 grid grid-cols-3 gap-2.5 border-t border-slate-100 pt-4 text-center dark:border-slate-800">
            <div className="rounded-xl bg-slate-50 px-2 py-2.5 dark:bg-[#1A2234]">
              <dd className="text-lg font-bold text-sky-600 dark:text-sky-400">
                {profile.friendCount}
              </dd>
              <dt className="text-xs text-slate-500 dark:text-slate-400">
                {t('social.stat_friends')}
              </dt>
            </div>
            <div className="rounded-xl bg-slate-50 px-2 py-2.5 dark:bg-[#1A2234]">
              <dd className="text-lg font-bold text-sky-600 dark:text-sky-400">
                {profile.followerCount}
              </dd>
              <dt className="text-xs text-slate-500 dark:text-slate-400">
                {t('social.stat_followers')}
              </dt>
            </div>
            <div className="rounded-xl bg-slate-50 px-2 py-2.5 dark:bg-[#1A2234]">
              <dd className="text-lg font-bold text-sky-600 dark:text-sky-400">
                {profile.followingCount}
              </dd>
              <dt className="text-xs text-slate-500 dark:text-slate-400">
                {t('social.stat_following')}
              </dt>
            </div>
          </dl>
        </div>
      </section>

      <h2 className="text-sm font-bold text-slate-600 dark:text-slate-300">{t('social.posts')}</h2>

      {postsQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
          <Spinner className="text-sky-600 dark:text-sky-400" /> {t('social.loading')}
        </div>
      ) : postsQuery.isError ? (
        <Alert variant="error">
          <p className="mb-2">{t('social.feed_error')}</p>
          <button type="button" className="underline" onClick={() => void postsQuery.refetch()}>
            {t('common.retry')}
          </button>
        </Alert>
      ) : posts.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-slate-300 bg-white/60 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-[#121826]/60 dark:text-slate-400">
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
