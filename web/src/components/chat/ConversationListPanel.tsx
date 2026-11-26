import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { useInfiniteQuery } from '@tanstack/react-query'
import { BellOff, MessagesSquare, Plus, Search, Users } from 'lucide-react'
import { conversationsApi, type MessageSearchResult } from '../../api/conversations'
import { Avatar } from '../social/Avatar'
import { MessageSearchPanel } from './MessageSearchPanel'
import { PresenceDot } from './PresenceDot'
import { CreateGroupDialog } from '../social/CreateGroupDialog'
import { Button } from '../ui/Button'
import { Alert, Skeleton } from '../ui/Feedback'
import { useInfiniteScroll } from '../../hooks/useInfiniteScroll'
import { formatCallMessage } from '../../lib/callFormat'
import { timeAgo } from '../../lib/format'
import { cn } from '../../lib/utils'

type ConversationFilter = 'all' | 'unread' | 'groups'

interface ConversationListPanelProps {
  /** Hội thoại đang mở để tô sáng trên Desktop Split-Pane. */
  activeConversationId?: string
  className?: string
}

/**
 * Cột danh sách hội thoại (1–1 và nhóm) dùng chung cho cả `/messages` và Split-Pane ở `/messages/:id`.
 */
export function ConversationListPanel({
  activeConversationId,
  className,
}: ConversationListPanelProps) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [creatingGroup, setCreatingGroup] = useState(false)
  const [searchingMessages, setSearchingMessages] = useState(false)
  const [filter, setFilter] = useState<ConversationFilter>('all')

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

  const allItems = conversationsQuery.data?.pages.flatMap((page) => page.items) ?? []
  const items = allItems.filter((item) => {
    if (filter === 'unread') return item.unreadCount > 0
    if (filter === 'groups') return item.type === 'Group'
    return true
  })

  const filters: { id: ConversationFilter; label: string }[] = [
    { id: 'all', label: t('chat.filter_all') },
    { id: 'unread', label: t('chat.filter_unread') },
    { id: 'groups', label: t('chat.filter_groups') },
  ]

  return (
    <section
      className={cn(
        'flex h-full min-h-0 flex-col overflow-hidden bg-white dark:bg-[#121826]',
        className,
      )}
    >
      <div className="space-y-3 border-b border-slate-200/80 p-3.5 dark:border-slate-800">
        <div className="flex items-center justify-between gap-2">
          <h1 className="flex items-center gap-2 text-lg font-bold text-slate-900 dark:text-slate-100">
            <MessagesSquare className="h-5 w-5 text-sky-600 dark:text-sky-400" />
            <span>{t('chat.title')}</span>
          </h1>
          <div className="flex items-center gap-1.5">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setSearchingMessages(true)}
              title={t('chat.search_all_messages')}
              aria-label={t('chat.search_all_messages')}
            >
              <Search className="h-4 w-4" />
            </Button>
            <Button size="sm" onClick={() => setCreatingGroup(true)}>
              <Plus className="h-4 w-4" />
              <span>{t('chat.create_group')}</span>
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-3 gap-1 rounded-xl bg-slate-100 p-1 dark:bg-[#1A2234]">
          {filters.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setFilter(item.id)}
              className={cn(
                'rounded-lg py-1.5 text-xs font-semibold transition-colors',
                filter === item.id
                  ? 'bg-white text-slate-900 shadow-xs dark:bg-[#121826] dark:text-slate-100'
                  : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200',
              )}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto p-2">
        {conversationsQuery.isLoading ? (
          <div className="space-y-2 p-2">
            {[1, 2, 3, 4].map((item) => (
              <div key={item} className="flex items-center gap-3 rounded-xl p-2">
                <Skeleton className="h-11 w-11 rounded-full" />
                <div className="flex-1 space-y-1.5">
                  <Skeleton className="h-3.5 w-28" />
                  <Skeleton className="h-3 w-44" />
                </div>
              </div>
            ))}
          </div>
        ) : conversationsQuery.isError ? (
          <div className="p-2">
            <Alert variant="error">
              <p className="mb-2">{t('chat.error')}</p>
              <button
                type="button"
                className="font-semibold underline"
                onClick={() => void conversationsQuery.refetch()}
              >
                {t('common.retry')}
              </button>
            </Alert>
          </div>
        ) : items.length === 0 ? (
          <p className="m-2 rounded-2xl border border-dashed border-slate-300 p-8 text-center text-xs leading-relaxed text-slate-500 dark:border-slate-700 dark:text-slate-400">
            {t('chat.empty_list')}
          </p>
        ) : (
          <ul className="space-y-1">
            {items.map((conversation) => {
              const isGroup = conversation.type === 'Group'
              const isSelected = conversation.id === activeConversationId
              const displayName = isGroup
                ? conversation.title || t('chat.group')
                : conversation.peer?.displayName || conversation.peer?.userName || t('chat.title')

              const preview =
                conversation.lastMessage === null
                  ? t('chat.no_message_yet')
                  : conversation.lastMessage.isDeleted
                    ? t('chat.recalled')
                    : conversation.lastMessage.type === 'Call'
                      ? formatCallMessage(conversation.lastMessage.preview, t)
                      : conversation.lastMessage.preview ||
                        (conversation.lastMessage.type === 'Image' ? t('chat.image_preview') : '')

              return (
                <li key={conversation.id}>
                  <Link
                    to={`/messages/${conversation.id}`}
                    className={cn(
                      'flex items-center gap-3 rounded-xl p-2.5 transition-colors',
                      isSelected
                        ? 'bg-sky-50 dark:bg-sky-500/15'
                        : 'hover:bg-slate-100/80 dark:hover:bg-[#1A2234]',
                    )}
                  >
                    {isGroup ? (
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
                        <Users className="h-5 w-5" />
                      </span>
                    ) : (
                      <span className="relative shrink-0">
                        <Avatar
                          name={displayName}
                          url={conversation.peer?.avatarUrl ?? null}
                          size="md"
                        />
                        {conversation.peer && (
                          <PresenceDot
                            online={conversation.peer.isOnline}
                            label={
                              conversation.peer.isOnline ? t('chat.online') : t('chat.offline')
                            }
                            className="absolute -bottom-0.5 -right-0.5"
                          />
                        )}
                      </span>
                    )}

                    <div className="min-w-0 flex-1">
                      <div className="flex items-center justify-between gap-2">
                        <p
                          className={cn(
                            'truncate text-sm font-bold',
                            isSelected
                              ? 'text-sky-700 dark:text-sky-300'
                              : 'text-slate-800 dark:text-slate-100',
                          )}
                        >
                          {displayName}
                          {conversation.isMuted && (
                            <BellOff
                              className="ml-1 inline h-3 w-3 text-slate-400"
                              aria-label={t('chat.muted')}
                            />
                          )}
                        </p>
                        {conversation.lastMessage && (
                          <span className="shrink-0 text-xs text-slate-400">
                            {timeAgo(conversation.lastMessage.createdAt, i18n.language)}
                          </span>
                        )}
                      </div>
                      <div className="mt-0.5 flex items-center justify-between gap-2">
                        <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                          {isGroup && !conversation.lastMessage
                            ? t('chat.member_count', { count: conversation.memberCount })
                            : preview}
                        </p>
                        {conversation.unreadCount > 0 && (
                          <span
                            className="shrink-0 rounded-full bg-sky-600 px-2 py-0.5 text-[11px] font-bold text-white dark:bg-sky-500"
                            aria-label={t('chat.unread_count', {
                              count: conversation.unreadCount,
                            })}
                          >
                            {conversation.unreadCount > 99 ? '99+' : conversation.unreadCount}
                          </span>
                        )}
                      </div>
                    </div>
                  </Link>
                </li>
              )
            })}
          </ul>
        )}
        <div ref={sentinelRef} className="h-4" />
      </div>

      {creatingGroup && <CreateGroupDialog onClose={() => setCreatingGroup(false)} />}

      {searchingMessages && (
        <MessageSearchPanel
          conversationId={null}
          onClose={() => setSearchingMessages(false)}
          onPick={(result: MessageSearchResult, query: string) => {
            setSearchingMessages(false)
            navigate(
              `/messages/${result.conversationId}?focus=${result.messageId}&q=${encodeURIComponent(query)}`,
            )
          }}
        />
      )}
    </section>
  )
}
