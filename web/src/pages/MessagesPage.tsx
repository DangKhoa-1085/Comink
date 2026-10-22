import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useInfiniteQuery } from '@tanstack/react-query'
import { MessagesSquare, Plus, Users } from 'lucide-react'
import { conversationsApi } from '../api/conversations'
import { Avatar } from '../components/social/Avatar'
import { CreateGroupDialog } from '../components/social/CreateGroupDialog'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { useInfiniteScroll } from '../hooks/useInfiniteScroll'
import { timeAgo } from '../lib/format'

/** Danh sách hội thoại (1–1 và nhóm), sắp theo tin mới nhất (docs/05_api_contract.md mục 8.3). */
export function MessagesPage() {
  const { t, i18n } = useTranslation()
  const [creatingGroup, setCreatingGroup] = useState(false)

  const conversationsQuery = useInfiniteQuery({
    queryKey: ['conversations'],
    queryFn: ({ pageParam }) => conversationsApi.list(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
  })

  const sentinelRef = useInfiniteScroll(
    () => {
      if (conversationsQuery.hasNextPage && !conversationsQuery.isFetchingNextPage) {
        void conversationsQuery.fetchNextPage()
      }
    },
    Boolean(conversationsQuery.hasNextPage),
  )

  const items = conversationsQuery.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-4 flex items-center justify-between gap-2">
        <h1 className="flex items-center gap-2 text-xl font-bold">
          <MessagesSquare className="h-5 w-5" />
          {t('chat.title')}
        </h1>
        <Button variant="outline" size="sm" onClick={() => setCreatingGroup(true)}>
          <Plus className="h-4 w-4" />
          {t('chat.create_group')}
        </Button>
      </div>

      {conversationsQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
          <Spinner /> {t('social.loading')}
        </div>
      ) : conversationsQuery.isError ? (
        <Alert variant="error">
          <p className="mb-2">{t('chat.error')}</p>
          <button type="button" className="underline" onClick={() => void conversationsQuery.refetch()}>
            {t('common.retry')}
          </button>
        </Alert>
      ) : items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700">
          {t('chat.empty_list')}
        </p>
      ) : (
        <ul className="space-y-2">
          {items.map((conversation) => {
            const isGroup = conversation.type === 'Group'
            const displayName = isGroup
              ? conversation.title || t('chat.group')
              : conversation.peer?.displayName || conversation.peer?.userName || t('chat.title')

            const preview =
              conversation.lastMessage === null
                ? t('chat.no_message_yet')
                : conversation.lastMessage.preview ||
                  (conversation.lastMessage.type === 'Image' ? t('chat.image_preview') : '')

            return (
              <li key={conversation.id}>
                <Link
                  to={`/messages/${conversation.id}`}
                  className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 hover:border-indigo-300 dark:border-slate-800 dark:bg-slate-900 dark:hover:border-indigo-700"
                >
                  {isGroup ? (
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-300">
                      <Users className="h-5 w-5" />
                    </span>
                  ) : (
                    <Avatar
                      name={displayName}
                      url={conversation.peer?.avatarUrl ?? null}
                      size="md"
                    />
                  )}
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold text-slate-800 dark:text-slate-100">
                      {displayName}
                      {isGroup && (
                        <span className="ml-2 text-xs font-normal text-slate-400">
                          {t('chat.member_count', { count: conversation.memberCount })}
                        </span>
                      )}
                    </p>
                    <p className="truncate text-xs text-slate-500 dark:text-slate-400">{preview}</p>
                  </div>
                  {conversation.lastMessage && (
                    <span className="shrink-0 text-xs text-slate-400">
                      {timeAgo(conversation.lastMessage.createdAt, i18n.language)}
                    </span>
                  )}
                </Link>
              </li>
            )
          })}
        </ul>
      )}

      <div ref={sentinelRef} className="h-6" />

      {creatingGroup && <CreateGroupDialog onClose={() => setCreatingGroup(false)} />}
    </div>
  )
}
