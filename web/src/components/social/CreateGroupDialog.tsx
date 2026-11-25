import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useInfiniteQuery, useMutation } from '@tanstack/react-query'
import { Users, X } from 'lucide-react'
import { friendshipsApi } from '../../api/friendships'
import { conversationsApi } from '../../api/conversations'
import { Avatar } from './Avatar'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Alert, Spinner } from '../ui/Feedback'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'

/**
 * Hộp thoại tạo nhóm chat (NC GĐ 3). Nhóm tối thiểu 3 thành viên (kể cả người tạo) và
 * chỉ mời được bạn bè — trùng quy tắc ở backend (`ConversationRules`).
 */
export function CreateGroupDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [selected, setSelected] = useState<string[]>([])

  const friendsQuery = useInfiniteQuery({
    queryKey: ['friends'],
    queryFn: ({ pageParam }) => friendshipsApi.list(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
  })

  const friends =
    friendsQuery.data?.pages
      .flatMap((page) => page.items)
      .filter((friendship) => friendship.status === 'Accepted') ?? []

  const createMutation = useMutation({
    mutationFn: () => conversationsApi.createGroup(name.trim(), selected),
    onSuccess: (conversation) => {
      onClose()
      navigate(`/messages/${conversation.id}`)
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const toggle = (userId: string) => {
    setSelected((previous) =>
      previous.includes(userId) ? previous.filter((id) => id !== userId) : [...previous, userId],
    )
  }

  // Tổng thành viên = 2 người được mời + người tạo ⇒ cần chọn ít nhất 2 bạn.
  const totalMembers = selected.length + 1
  const canSubmit = name.trim().length > 0 && selected.length >= 2 && !createMutation.isPending

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/60 p-4">
      <div
        role="dialog"
        aria-modal="true"
        className="flex max-h-[85vh] w-full max-w-md flex-col rounded-2xl bg-white p-5 shadow-2xl dark:bg-slate-900"
      >
        <div className="mb-3 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-base font-bold">
            <Users className="h-4 w-4" />
            {t('chat.create_group')}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1 hover:bg-slate-100 dark:hover:bg-slate-800"
            aria-label={t('chat.cancel')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <Input
          value={name}
          onChange={(event) => setName(event.target.value)}
          placeholder={t('chat.group_name')}
          maxLength={100}
          aria-label={t('chat.group_name')}
        />

        <p className="mt-3 mb-2 text-xs text-slate-500">
          {t('chat.group_members_hint', { count: totalMembers, min: 3, max: 10 })}
        </p>

        <div className="-mx-1 flex-1 overflow-y-auto px-1">
          {friendsQuery.isLoading ? (
            <div className="flex items-center justify-center gap-2 py-6 text-sm text-slate-500">
              <Spinner /> {t('social.loading')}
            </div>
          ) : friends.length === 0 ? (
            <Alert variant="info">{t('chat.no_friends')}</Alert>
          ) : (
            <ul className="space-y-1">
              {friends.map((friendship) => {
                const chosen = selected.includes(friendship.user.id)
                return (
                  <li key={friendship.user.id}>
                    <button
                      type="button"
                      onClick={() => toggle(friendship.user.id)}
                      className={
                        chosen
                          ? 'flex w-full items-center gap-2 rounded-lg bg-indigo-50 px-2 py-2 text-left dark:bg-indigo-950'
                          : 'flex w-full items-center gap-2 rounded-lg px-2 py-2 text-left hover:bg-slate-100 dark:hover:bg-slate-800'
                      }
                      aria-pressed={chosen}
                    >
                      <Avatar
                        name={friendship.user.displayName || friendship.user.userName}
                        url={friendship.user.avatarUrl}
                        size="sm"
                      />
                      <span className="min-w-0 flex-1 truncate text-sm">
                        {friendship.user.displayName || friendship.user.userName}
                      </span>
                      <span
                        className={
                          chosen
                            ? 'h-4 w-4 rounded border-2 border-indigo-600 bg-indigo-600'
                            : 'h-4 w-4 rounded border-2 border-slate-300 dark:border-slate-600'
                        }
                      />
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        <div className="mt-3 flex items-center justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            {t('chat.cancel')}
          </Button>
          <Button onClick={() => createMutation.mutate()} disabled={!canSubmit}>
            {t('chat.create')}
          </Button>
        </div>
      </div>
    </div>
  )
}
