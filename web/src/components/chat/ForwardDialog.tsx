import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Loader2, Send, Users, X } from 'lucide-react'
import { conversationsApi, type ChatMessage } from '../../api/conversations'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'
import { toast } from '../../stores/toastStore'
import { Avatar } from '../social/Avatar'
import { Button } from '../ui/Button'
import { Alert, Spinner } from '../ui/Feedback'

interface ForwardDialogProps {
  sourceConversationId: string
  message: ChatMessage
  onClose: () => void
  onForwarded: (message: ChatMessage) => void
}

/**
 * Chọn hội thoại đích để chuyển tiếp tin (NC Ưu tiên 2). Server mới là bên sao chép
 * nội dung + tệp và ghi `forwarded_from_message_id` (quyết định D4).
 */
export function ForwardDialog({
  sourceConversationId,
  message,
  onClose,
  onForwarded,
}: ForwardDialogProps) {
  const { t } = useTranslation()
  const [keyword, setKeyword] = useState('')

  const conversationsQuery = useQuery({
    queryKey: ['conversations', 'forward-picker'],
    queryFn: () => conversationsApi.list(null, 50),
  })

  const forwardMutation = useMutation({
    mutationFn: (targetConversationId: string) =>
      conversationsApi.forward(targetConversationId, message.id, crypto.randomUUID()),
    onSuccess: (forwarded) => {
      toast.success(t('chat.forwarded'))
      onForwarded(forwarded)
      onClose()
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const normalized = keyword.trim().toLowerCase()
  const conversations = (conversationsQuery.data?.items ?? []).filter((conversation) => {
    if (normalized.length === 0) {
      return true
    }

    const name =
      conversation.type === 'Group'
        ? conversation.title ?? ''
        : conversation.peer?.displayName ?? conversation.peer?.userName ?? ''
    return name.toLowerCase().includes(normalized)
  })

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center bg-black/40 p-4 pt-16">
      <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-4 shadow-xl dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Send className="h-4 w-4" />
            {t('chat.forward_to')}
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

        <p className="mb-3 line-clamp-2 rounded-lg bg-slate-50 p-2 text-xs text-slate-500 dark:bg-slate-950">
          {message.content || t('chat.image_preview')}
        </p>

        <input
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
          placeholder={t('chat.forward_search_placeholder')}
          className="mb-2 h-9 w-full rounded-lg border border-slate-200 bg-slate-50 px-2 text-sm outline-none focus:border-indigo-400 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
        />

        {conversationsQuery.isLoading ? (
          <div className="flex items-center justify-center gap-2 py-6 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : conversationsQuery.isError ? (
          <Alert variant="error">{t('chat.forward_error')}</Alert>
        ) : conversations.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-500">{t('chat.forward_empty')}</p>
        ) : (
          <ul className="max-h-72 space-y-1 overflow-y-auto">
            {conversations.map((conversation) => {
              const isGroup = conversation.type === 'Group'
              const name = isGroup
                ? conversation.title || t('chat.group')
                : conversation.peer?.displayName || conversation.peer?.userName || t('chat.title')

              return (
                <li key={conversation.id}>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="w-full justify-start"
                    disabled={forwardMutation.isPending}
                    onClick={() => forwardMutation.mutate(conversation.id)}
                  >
                    {forwardMutation.isPending && forwardMutation.variables === conversation.id ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : isGroup ? (
                      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-300">
                        <Users className="h-3.5 w-3.5" />
                      </span>
                    ) : (
                      <Avatar name={name} url={conversation.peer?.avatarUrl ?? null} size="sm" />
                    )}
                    <span className="truncate">{name}</span>
                    {conversation.id === sourceConversationId && (
                      <span className="ml-auto text-[10px] text-slate-400">
                        {t('chat.forward_current')}
                      </span>
                    )}
                  </Button>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </div>
  )
}
