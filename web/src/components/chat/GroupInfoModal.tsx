import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Camera,
  Check,
  Crown,
  Loader2,
  LogOut,
  Pencil,
  Trash2,
  UserMinus,
  UserPlus,
  Users,
  X,
} from 'lucide-react'
import { conversationsApi, type Conversation } from '../../api/conversations'
import { friendshipsApi } from '../../api/friendships'
import { mediaApi } from '../../api/media'
import { Avatar } from '../social/Avatar'
import { PresenceDot } from './PresenceDot'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Alert, Spinner } from '../ui/Feedback'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'
import { cn } from '../../lib/utils'

const MAX_AVATAR_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const MAX_GROUP_MEMBERS = 10

interface GroupInfoModalProps {
  conversation: Conversation
  currentUserId: string
  onClose: () => void
  onLeftGroup: () => void
}

/**
 * Hộp thoại thông tin & quản lý nhóm chat:
 * đổi ảnh đại diện/tên nhóm, xem danh sách thành viên (hiện Nhóm trưởng),
 * thêm bạn bè mới vào nhóm, nhóm trưởng kick thành viên, và rời nhóm.
 */
export function GroupInfoModal({
  conversation,
  currentUserId,
  onClose,
  onLeftGroup,
}: GroupInfoModalProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const avatarInputRef = useRef<HTMLInputElement | null>(null)

  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [editingTitle, setEditingTitle] = useState(false)
  const [titleDraft, setTitleDraft] = useState(conversation.title ?? '')
  const [addingOpen, setAddingOpen] = useState(false)
  const [selectedFriends, setSelectedFriends] = useState<string[]>([])

  const meMember = conversation.members.find((member) => member.userId === currentUserId)
  const isOwner = meMember?.role === 'Owner'
  const slotsLeft = Math.max(0, MAX_GROUP_MEMBERS - conversation.members.length)
  const groupTitle = conversation.title || t('chat.group')

  const friendsQuery = useInfiniteQuery({
    queryKey: ['friends'],
    queryFn: ({ pageParam }) => friendshipsApi.list(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    enabled: addingOpen,
  })

  const memberIds = new Set(conversation.members.map((member) => member.userId))
  const availableFriends = (
    friendsQuery.data?.pages
      .flatMap((page) => page.items)
      .filter((friendship) => friendship.status === 'Accepted') ?? []
  ).filter((friendship) => !memberIds.has(friendship.user.id))

  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    toast.error(translateErrorCode(t, code))
  }

  const syncConversation = (updated: Conversation) => {
    queryClient.setQueryData(['conversation', updated.id], updated)
    void queryClient.invalidateQueries({ queryKey: ['conversations'], refetchType: 'all' })
  }

  const renameMutation = useMutation({
    mutationFn: (title: string) => conversationsApi.updateGroup(conversation.id, { title }),
    onSuccess: (updated) => {
      syncConversation(updated)
      setEditingTitle(false)
      toast.success(t('chat.group_renamed'))
    },
    onError: showError,
  })

  const addMembersMutation = useMutation({
    mutationFn: (ids: string[]) => conversationsApi.addGroupMembers(conversation.id, ids),
    onSuccess: (updated) => {
      syncConversation(updated)
      setSelectedFriends([])
      setAddingOpen(false)
      toast.success(t('chat.group_members_added'))
    },
    onError: showError,
  })

  const kickMutation = useMutation({
    mutationFn: (userId: string) => conversationsApi.removeGroupMember(conversation.id, userId),
    onSuccess: (updated) => {
      syncConversation(updated)
      toast.success(t('chat.group_member_removed'))
    },
    onError: showError,
  })

  const leaveMutation = useMutation({
    mutationFn: () => conversationsApi.leaveGroup(conversation.id),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: ['conversation', conversation.id] })
      queryClient.removeQueries({ queryKey: ['messages', conversation.id] })
      void queryClient.invalidateQueries({ queryKey: ['conversations'], refetchType: 'all' })
      toast.success(t('chat.group_left'))
      onClose()
      onLeftGroup()
    },
    onError: showError,
  })

  const handleAvatarSelected = async (files: FileList | null) => {
    const file = files?.[0]
    if (!file) {
      return
    }

    if (!ALLOWED_TYPES.includes(file.type)) {
      toast.error(t('social.bad_type'))
      return
    }
    if (file.size > MAX_AVATAR_BYTES) {
      toast.error(t('social.too_large_image'))
      return
    }

    setUploadingAvatar(true)
    try {
      const signature = await mediaApi.getSignature('Avatar')
      const uploaded = await mediaApi.upload(signature, file)
      const asset = await mediaApi.register({
        providerPublicId: uploaded.providerPublicId,
        url: uploaded.url,
        thumbnailUrl: uploaded.url,
        mimeType: uploaded.mimeType,
        sizeBytes: uploaded.sizeBytes,
        context: 'Avatar',
      })
      const updated = await conversationsApi.updateGroup(conversation.id, { avatarUrl: asset.url })
      syncConversation(updated)
      toast.success(t('chat.group_avatar_updated'))
    } catch (error) {
      showError(error)
    } finally {
      setUploadingAvatar(false)
      if (avatarInputRef.current) {
        avatarInputRef.current.value = ''
      }
    }
  }

  const handleRemoveAvatar = async () => {
    setUploadingAvatar(true)
    try {
      const updated = await conversationsApi.updateGroup(conversation.id, { avatarUrl: '' })
      syncConversation(updated)
      toast.success(t('chat.group_avatar_updated'))
    } catch (error) {
      showError(error)
    } finally {
      setUploadingAvatar(false)
    }
  }

  const toggleFriend = (userId: string) => {
    setSelectedFriends((prev) => {
      if (prev.includes(userId)) {
        return prev.filter((id) => id !== userId)
      }
      if (prev.length >= slotsLeft) {
        return prev
      }
      return [...prev, userId]
    })
  }

  const handleKick = (userId: string, name: string) => {
    if (window.confirm(t('chat.group_kick_confirm', { name }))) {
      kickMutation.mutate(userId)
    }
  }

  const handleLeave = () => {
    const prompt =
      isOwner && conversation.members.length > 1
        ? t('chat.group_leave_owner_confirm')
        : t('chat.group_leave_confirm')
    if (window.confirm(prompt)) {
      leaveMutation.mutate()
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-xs">
      <div
        role="dialog"
        aria-modal="true"
        aria-label={t('chat.group_info')}
        className="flex max-h-[88vh] w-full max-w-md flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-[#121826]"
      >
        {/* Tiêu đề modal */}
        <div className="flex items-center justify-between border-b border-slate-100 px-5 py-3.5 dark:border-slate-800">
          <h2 className="flex items-center gap-2 text-base font-bold text-slate-900 dark:text-slate-100">
            <Users className="h-4 w-4 text-sky-600 dark:text-sky-400" />
            <span>{t('chat.group_info')}</span>
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('chat.cancel')}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Nội dung cuộn được */}
        <div className="flex-1 space-y-5 overflow-y-auto p-5">
          {/* Ảnh đại diện & tên nhóm */}
          <div className="flex flex-col items-center gap-3 text-center">
            <input
              ref={avatarInputRef}
              type="file"
              accept={ALLOWED_TYPES.join(',')}
              hidden
              onChange={(event) => void handleAvatarSelected(event.target.files)}
            />
            <div className="relative">
              {conversation.avatarUrl ? (
                <Avatar name={groupTitle} url={conversation.avatarUrl} size="lg" />
              ) : (
                <div className="flex h-20 w-20 items-center justify-center rounded-full bg-sky-100 text-sky-700 ring-2 ring-sky-200 dark:bg-sky-500/20 dark:text-sky-300 dark:ring-sky-500/30">
                  <Users className="h-9 w-9" />
                </div>
              )}
              <button
                type="button"
                onClick={() => avatarInputRef.current?.click()}
                disabled={uploadingAvatar}
                title={t('chat.group_change_avatar')}
                aria-label={t('chat.group_change_avatar')}
                className="absolute bottom-0 right-0 flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-sky-600 text-white shadow-sm transition-colors hover:bg-sky-700 disabled:opacity-60 dark:border-[#121826] dark:bg-sky-500"
              >
                {uploadingAvatar ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Camera className="h-3.5 w-3.5" />
                )}
              </button>
            </div>

            {conversation.avatarUrl && (
              <button
                type="button"
                onClick={() => void handleRemoveAvatar()}
                disabled={uploadingAvatar}
                className="inline-flex items-center gap-1 text-xs font-medium text-slate-500 hover:text-rose-600 dark:text-slate-400 dark:hover:text-rose-400"
              >
                <Trash2 className="h-3 w-3" />
                <span>{t('chat.group_remove_avatar')}</span>
              </button>
            )}

            {editingTitle ? (
              <div className="flex w-full max-w-xs items-center gap-1.5">
                <Input
                  value={titleDraft}
                  onChange={(event) => setTitleDraft(event.target.value)}
                  maxLength={100}
                  aria-label={t('chat.group_name')}
                  className="h-9 text-sm"
                />
                <Button
                  size="sm"
                  disabled={titleDraft.trim().length === 0 || renameMutation.isPending}
                  onClick={() => renameMutation.mutate(titleDraft.trim())}
                  aria-label={t('chat.save')}
                >
                  {renameMutation.isPending ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Check className="h-3.5 w-3.5" />
                  )}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setEditingTitle(false)
                    setTitleDraft(conversation.title ?? '')
                  }}
                  aria-label={t('chat.cancel')}
                >
                  <X className="h-3.5 w-3.5" />
                </Button>
              </div>
            ) : (
              <div className="flex items-center justify-center gap-1.5">
                <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                  {groupTitle}
                </h3>
                <button
                  type="button"
                  onClick={() => {
                    setTitleDraft(conversation.title ?? '')
                    setEditingTitle(true)
                  }}
                  title={t('chat.group_rename')}
                  aria-label={t('chat.group_rename')}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800 dark:hover:text-slate-200"
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
              </div>
            )}

            <p className="text-xs text-slate-500 dark:text-slate-400">
              {t('chat.member_count', { count: conversation.members.length })} / {MAX_GROUP_MEMBERS}
            </p>
          </div>

          {/* Khu vực thêm thành viên */}
          <div className="space-y-2 border-t border-slate-100 pt-4 dark:border-slate-800">
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                {t('chat.group_members')} ({conversation.members.length})
              </p>
              {slotsLeft > 0 ? (
                <Button
                  variant={addingOpen ? 'outline' : 'soft'}
                  size="sm"
                  onClick={() => {
                    setAddingOpen((open) => !open)
                    setSelectedFriends([])
                  }}
                >
                  <UserPlus className="h-3.5 w-3.5" />
                  <span>{addingOpen ? t('chat.cancel') : t('chat.group_add_members')}</span>
                </Button>
              ) : (
                <span className="text-[11px] font-medium text-amber-600 dark:text-amber-400">
                  {t('chat.group_full')}
                </span>
              )}
            </div>

            {addingOpen && (
              <div className="space-y-2 rounded-2xl border border-sky-200/80 bg-sky-50/50 p-3 dark:border-sky-500/20 dark:bg-sky-500/10">
                <p className="text-xs text-slate-600 dark:text-slate-300">
                  {t('chat.group_slots_left', { count: slotsLeft })}
                </p>

                {friendsQuery.isLoading ? (
                  <div className="flex items-center justify-center gap-2 py-4 text-xs text-slate-500">
                    <Spinner className="text-sky-600" /> {t('social.loading')}
                  </div>
                ) : friendsQuery.isError ? (
                  <Alert variant="error">
                    <div className="flex items-center justify-between gap-2">
                      <span>{t('common.error_title')}</span>
                      <button
                        type="button"
                        onClick={() => void friendsQuery.refetch()}
                        className="font-semibold underline"
                      >
                        {t('common.retry')}
                      </button>
                    </div>
                  </Alert>
                ) : availableFriends.length === 0 ? (
                  <p className="py-2 text-center text-xs text-slate-500 dark:text-slate-400">
                    {t('chat.group_no_more_friends')}
                  </p>
                ) : (
                  <>
                    <ul className="max-h-40 space-y-1 overflow-y-auto pr-1">
                      {availableFriends.map((friendship) => {
                        const friend = friendship.user
                        const chosen = selectedFriends.includes(friend.id)
                        const disabled = !chosen && selectedFriends.length >= slotsLeft
                        return (
                          <li key={friend.id}>
                            <button
                              type="button"
                              disabled={disabled}
                              onClick={() => toggleFriend(friend.id)}
                              className={cn(
                                'flex w-full items-center gap-2 rounded-xl px-2.5 py-1.5 text-left text-xs transition-colors',
                                chosen
                                  ? 'bg-sky-100 text-sky-900 dark:bg-sky-500/25 dark:text-sky-100'
                                  : 'hover:bg-white/80 disabled:opacity-45 dark:hover:bg-slate-800/60',
                              )}
                            >
                              <Avatar
                                name={friend.displayName || friend.userName}
                                url={friend.avatarUrl}
                                size="sm"
                              />
                              <span className="min-w-0 flex-1 truncate font-medium">
                                {friend.displayName || friend.userName}
                              </span>
                              <span
                                className={cn(
                                  'h-4 w-4 shrink-0 rounded border-2',
                                  chosen
                                    ? 'border-sky-600 bg-sky-600 dark:border-sky-400 dark:bg-sky-400'
                                    : 'border-slate-300 dark:border-slate-600',
                                )}
                              />
                            </button>
                          </li>
                        )
                      })}
                    </ul>
                    <div className="flex justify-end pt-1">
                      <Button
                        size="sm"
                        disabled={selectedFriends.length === 0 || addMembersMutation.isPending}
                        onClick={() => addMembersMutation.mutate(selectedFriends)}
                      >
                        {addMembersMutation.isPending ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <UserPlus className="h-3.5 w-3.5" />
                        )}
                        <span>{t('chat.group_add_submit', { count: selectedFriends.length })}</span>
                      </Button>
                    </div>
                  </>
                )}
              </div>
            )}

            {/* Danh sách thành viên hiện tại */}
            <ul className="space-y-1.5">
              {conversation.members.map((member) => {
                const isMemberOwner = member.role === 'Owner'
                const isMe = member.userId === currentUserId
                const displayName = member.displayName || member.userName
                return (
                  <li
                    key={member.userId}
                    className="flex items-center justify-between gap-2 rounded-xl border border-slate-100 bg-slate-50/70 px-3 py-2 dark:border-slate-800 dark:bg-[#1A2234]/70"
                  >
                    <Link
                      to={isMe ? '/profile' : `/users/${member.userId}`}
                      onClick={onClose}
                      className="flex min-w-0 flex-1 items-center gap-2.5"
                    >
                      <span className="relative shrink-0">
                        <Avatar name={displayName} url={member.avatarUrl} size="sm" />
                        <PresenceDot
                          online={member.isOnline}
                          label={member.isOnline ? t('chat.online') : t('chat.offline')}
                          className="absolute -bottom-0.5 -right-0.5"
                        />
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <span className="truncate text-xs font-bold text-slate-900 dark:text-slate-100">
                            {displayName}
                          </span>
                          {isMemberOwner && (
                            <span className="inline-flex items-center gap-0.5 rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] font-bold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                              <Crown className="h-2.5 w-2.5" />
                              {t('chat.group_owner')}
                            </span>
                          )}
                          {isMe && (
                            <span className="rounded-full bg-sky-100 px-1.5 py-0.5 text-[10px] font-semibold text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
                              {t('chat.group_you')}
                            </span>
                          )}
                        </div>
                        <p className="truncate text-[11px] text-slate-500 dark:text-slate-400">
                          @{member.userName}
                        </p>
                      </div>
                    </Link>

                    {isOwner && !isMe && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={kickMutation.isPending}
                        onClick={() => handleKick(member.userId, displayName)}
                        title={t('chat.group_kick')}
                        aria-label={t('chat.group_kick')}
                        className="shrink-0 text-rose-600 hover:bg-rose-50 hover:text-rose-700 dark:text-rose-400 dark:hover:bg-rose-950/40"
                      >
                        <UserMinus className="h-3.5 w-3.5" />
                      </Button>
                    )}
                  </li>
                )
              })}
            </ul>
          </div>
        </div>

        {/* Chân modal: Nút rời nhóm */}
        <div className="border-t border-slate-100 px-5 py-3 dark:border-slate-800">
          <Button
            variant="danger"
            size="sm"
            className="w-full"
            disabled={leaveMutation.isPending}
            onClick={handleLeave}
          >
            {leaveMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <LogOut className="h-4 w-4" />
            )}
            <span>{t('chat.group_leave')}</span>
          </Button>
        </div>
      </div>
    </div>
  )
}
