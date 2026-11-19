import { useTranslation } from 'react-i18next'
import { Pin, PinOff, X } from 'lucide-react'
import type { PinnedMessage } from '../../api/conversations'
import { timeAgo } from '../../lib/format'

interface PinnedPanelProps {
  pinned: PinnedMessage[]
  loading: boolean
  error: boolean
  onClose: () => void
  onJump: (messageId: string) => void
  onUnpin: (messageId: string) => void
}

/**
 * Panel danh sách tin ghim (NC Ưu tiên 2). Mọi thay đổi từ tab khác đến qua
 * `PinnedMessagesChanged` nên danh sách luôn khớp server.
 */
export function PinnedPanel({ pinned, loading, error, onClose, onJump, onUnpin }: PinnedPanelProps) {
  const { t, i18n } = useTranslation()

  return (
    <section
      aria-label={t('chat.pinned')}
      className="border-b border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-950"
    >
      <div className="mb-2 flex items-center justify-between">
        <p className="flex items-center gap-1 text-sm font-semibold">
          <Pin className="h-4 w-4" />
          {t('chat.pinned_count', { count: pinned.length })}
        </p>
        <button
          type="button"
          onClick={onClose}
          aria-label={t('chat.cancel')}
          className="rounded-full p-1 text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-800"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {loading ? (
        <p className="py-2 text-center text-xs text-slate-500">{t('social.loading')}</p>
      ) : error ? (
        <p className="py-2 text-center text-xs text-rose-500">{t('chat.pinned_error')}</p>
      ) : pinned.length === 0 ? (
        <p className="py-2 text-center text-xs text-slate-500">{t('chat.no_pinned')}</p>
      ) : (
        <ul className="max-h-56 space-y-1 overflow-y-auto">
          {pinned.map((item) => (
            <li
              key={item.message.id}
              className="flex items-start gap-2 rounded-lg bg-white p-2 text-xs dark:bg-slate-900"
            >
              <button
                type="button"
                onClick={() => onJump(item.message.id)}
                className="min-w-0 flex-1 text-left"
              >
                <span className="block truncate font-semibold text-slate-700 dark:text-slate-200">
                  {item.message.content || t('chat.image_preview')}
                </span>
                <span className="block truncate text-slate-400">
                  {t('chat.pinned_by', {
                    name: item.pinnedByDisplayName,
                    time: timeAgo(item.pinnedAt, i18n.language),
                  })}
                </span>
              </button>
              <button
                type="button"
                onClick={() => onUnpin(item.message.id)}
                aria-label={t('chat.unpin')}
                title={t('chat.unpin')}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800"
              >
                <PinOff className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
