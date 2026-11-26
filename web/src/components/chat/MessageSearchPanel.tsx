import { useState } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Loader2, Search, X } from 'lucide-react'
import { conversationsApi, type MessageSearchResult } from '../../api/conversations'
import { useInfiniteScroll } from '../../hooks/useInfiniteScroll'
import { timeAgo } from '../../lib/format'
import { Spinner } from '../ui/Feedback'

interface MessageSearchPanelProps {
  /** null = tìm trong mọi hội thoại tôi là thành viên. */
  conversationId: string | null
  onClose: () => void
  /** `query` là từ khoá đã tìm — dùng để tô sáng trong tin khi mở tới. */
  onPick: (result: MessageSearchResult, query: string) => void
}

/**
 * Tìm tin nhắn (NC Ưu tiên 2). Backend chỉ trả tin trong hội thoại người dùng còn là thành viên;
 * gõ không dấu vẫn khớp.
 */
export function MessageSearchPanel({ conversationId, onClose, onPick }: MessageSearchPanelProps) {
  const { t, i18n } = useTranslation()
  const [keyword, setKeyword] = useState('')
  const [submitted, setSubmitted] = useState('')

  const searchQuery = useInfiniteQuery({
    queryKey: ['message-search', conversationId ?? 'all', submitted],
    queryFn: ({ pageParam }) => conversationsApi.search(conversationId, submitted, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    enabled: submitted.length >= 2,
  })

  const sentinelRef = useInfiniteScroll(
    () => {
      if (searchQuery.hasNextPage && !searchQuery.isFetchingNextPage) {
        void searchQuery.fetchNextPage()
      }
    },
    Boolean(searchQuery.hasNextPage),
  )

  const results = searchQuery.data?.pages.flatMap((page) => page.items) ?? []

  const submit = () => {
    const value = keyword.trim()
    if (value.length >= 2) {
      setSubmitted(value)
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center bg-black/40 p-4 pt-16">
      <div className="flex max-h-[80vh] w-full max-w-lg flex-col rounded-2xl border border-slate-200 bg-white p-4 shadow-xl dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Search className="h-4 w-4" />
            {conversationId ? t('chat.search_in_conversation') : t('chat.search_all_messages')}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('chat.cancel')}
            className="rounded-full p-1 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="mb-2 flex items-center gap-2">
          <input
            autoFocus
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault()
                submit()
              }
            }}
            placeholder={t('chat.search_messages_placeholder')}
            className="h-9 min-w-0 flex-1 rounded-xl border border-slate-200 bg-slate-50 px-3 text-sm outline-none focus:border-sky-500 dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-100"
          />
          <button
            type="button"
            onClick={submit}
            disabled={keyword.trim().length < 2}
            className="rounded-xl bg-sky-600 px-3.5 py-2 text-xs font-semibold text-white disabled:opacity-50 dark:bg-sky-500"
          >
            {searchQuery.isFetching && submitted.length >= 2 ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              t('chat.search')
            )}
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto">
          {submitted.length < 2 ? (
            <p className="py-8 text-center text-xs text-slate-500">
              {t('chat.search_messages_hint')}
            </p>
          ) : searchQuery.isLoading ? (
            <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
              <Spinner /> {t('social.loading')}
            </div>
          ) : searchQuery.isError ? (
            <p className="py-8 text-center text-xs text-rose-500">{t('chat.search_error')}</p>
          ) : results.length === 0 ? (
            <p className="py-8 text-center text-xs text-slate-500">{t('chat.search_no_results')}</p>
          ) : (
            <ul className="space-y-1">
              {results.map((result) => (
                <li key={result.messageId}>
                  <button
                    type="button"
                    onClick={() => onPick(result, submitted)}
                    className="w-full rounded-lg p-2 text-left transition hover:bg-slate-100 dark:hover:bg-slate-800"
                  >
                    <span className="flex items-center justify-between gap-2 text-[11px] text-slate-400">
                      <span className="truncate">
                        {result.conversationTitle ||
                          result.peerDisplayName ||
                          result.senderDisplayName}
                      </span>
                      <span className="shrink-0">
                        {timeAgo(result.createdAt, i18n.language)}
                      </span>
                    </span>
                    <span className="mt-0.5 line-clamp-2 block text-sm text-slate-700 dark:text-slate-200">
                      {result.contentPreview || t('chat.image_preview')}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <div ref={sentinelRef} className="h-4" />
        </div>
      </div>
    </div>
  )
}
