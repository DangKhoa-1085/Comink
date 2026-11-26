import { useTranslation } from 'react-i18next'
import { MessagesSquare } from 'lucide-react'
import { ConversationListPanel } from '../components/chat/ConversationListPanel'

/**
 * Trang danh sách hội thoại:
 * - Mobile (< lg): hiển thị danh sách toàn màn hình.
 * - Desktop (>= lg): Split-Pane gồm danh sách bên trái + khung mời chọn hội thoại bên phải.
 */
export function MessagesPage() {
  const { t } = useTranslation()

  return (
    <div className="flex h-full min-h-0 overflow-hidden bg-[#F7F8FC] dark:bg-[#0B0F1A]">
      <ConversationListPanel className="w-full lg:w-[340px] lg:shrink-0 lg:border-r lg:border-slate-200/80 dark:lg:border-slate-800" />

      <div className="hidden flex-1 flex-col items-center justify-center p-8 text-center lg:flex">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-sky-50 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300">
          <MessagesSquare className="h-8 w-8" />
        </div>
        <h2 className="text-lg font-bold text-slate-900 dark:text-slate-100">
          {t('chat.select_conversation_title')}
        </h2>
        <p className="mt-1.5 max-w-sm text-sm text-slate-500 dark:text-slate-400">
          {t('chat.select_conversation_desc')}
        </p>
      </div>
    </div>
  )
}
