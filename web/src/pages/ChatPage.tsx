import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ImagePlus, Loader2, Phone, Send, Users, Video, X } from 'lucide-react'
import { callsApi, type CallType } from '../api/calls'
import { conversationsApi, type ChatMessage } from '../api/conversations'
import { mediaApi } from '../api/media'
import { Avatar } from '../components/social/Avatar'
import { PresenceDot } from '../components/chat/PresenceDot'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { timeAgo } from '../lib/format'
import { appendMessageToCache } from '../lib/messageCache'
import { sendTyping } from '../lib/chatConnection'
import { useAuthStore } from '../stores/authStore'
import { EMPTY_TYPING_IDS, useTypingStore } from '../stores/typingStore'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Khung chat 1–1: lịch sử (cuộn lên tải thêm), gửi chữ và ảnh (docs/05_api_contract.md mục 8.5–8.6). */
export function ChatPage() {
  const { t, i18n } = useTranslation()
  const { conversationId } = useParams<{ conversationId: string }>()
  const navigate = useNavigate()
  const me = useAuthStore((state) => state.user)
  const queryClient = useQueryClient()

  // Bắt đầu gọi thoại/video trong hội thoại này rồi sang màn hình gọi.
  const startCall = useMutation({
    mutationFn: (type: CallType) => callsApi.start({ conversationId: conversationId!, type }),
    onSuccess: (call) => navigate(`/calls/${call.id}`),
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    }
  })

  const [draft, setDraft] = useState('')
  const [pendingImage, setPendingImage] = useState<{ id: string; url: string } | null>(null)
  const [uploading, setUploading] = useState(false)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const bottomRef = useRef<HTMLDivElement | null>(null)

  const conversationQuery = useQuery({
    queryKey: ['conversation', conversationId],
    queryFn: () => conversationsApi.get(conversationId!),
    enabled: Boolean(conversationId),
  })

  const messagesQuery = useInfiniteQuery({
    queryKey: ['messages', conversationId],
    queryFn: ({ pageParam }) =>
      conversationsApi.messages(conversationId!, { before: pageParam, limit: 30 }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    enabled: Boolean(conversationId),
  })

  // Mỗi trang là "mới nhất trước" và các trang đi ngược thời gian → đảo lại để hiển thị cũ → mới.
  const messages: ChatMessage[] = [...(messagesQuery.data?.pages ?? [])]
    .flatMap((page) => page.items)
    .slice()
    .reverse()

  // Chỉ tự cuộn xuống khi có tin MỚI NHẤT thay đổi. Nếu cuộn theo độ dài mảng thì lúc bấm
  // "Tải tin cũ hơn" khung chat sẽ nhảy xuống cuối, mất chỗ đang đọc.
  const lastMessageId = messages[messages.length - 1]?.id

  useEffect(() => {
    if (lastMessageId) {
      bottomRef.current?.scrollIntoView({ block: 'end' })
    }
  }, [lastMessageId])

  // --- NC GĐ 7: "đang nhập" ---
  // ⚠️ Selector phải trả về CÙNG một tham chiếu khi không có ai gõ. Viết `?? []` ở đây
  // từng làm React render vô hạn ("Maximum update depth exceeded", lỗi thật 2026-11-17).
  const typingUserIds = useTypingStore(
    (state) =>
      (conversationId ? state.byConversation[conversationId] : undefined) ?? EMPTY_TYPING_IDS,
  )
  const typingUsers = typingUserIds.filter((id) => id !== me?.id)

  const typingStopTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const lastTypingSentAtRef = useRef(0)

  const stopTyping = () => {
    if (typingStopTimerRef.current) {
      clearTimeout(typingStopTimerRef.current)
      typingStopTimerRef.current = null
    }
    lastTypingSentAtRef.current = 0
    if (conversationId) {
      void sendTyping(conversationId, false)
    }
  }

  /** Gõ → báo "đang nhập" (throttle 3 giây); ngừng 5 giây thì tự báo ngừng. */
  const notifyTyping = () => {
    if (!conversationId) {
      return
    }

    const now = Date.now()
    if (now - lastTypingSentAtRef.current > 3000) {
      lastTypingSentAtRef.current = now
      void sendTyping(conversationId, true)
    }

    if (typingStopTimerRef.current) {
      clearTimeout(typingStopTimerRef.current)
    }
    typingStopTimerRef.current = setTimeout(stopTyping, 5000)
  }

  useEffect(() => {
    // Rời khung chat khi vẫn đang gõ → báo ngừng để người kia không thấy "đang nhập" treo.
    return () => {
      if (typingStopTimerRef.current) {
        clearTimeout(typingStopTimerRef.current)
      }
      if (conversationId) {
        void sendTyping(conversationId, false)
      }
    }
  }, [conversationId])

  // --- NC GĐ 7: đánh dấu đã đọc (server chỉ tiến mốc, gọi lặp lại vô hại) ---
  const lastMarkedReadRef = useRef<string | null>(null)
  const newestMessageIdRef = useRef<string | null>(null)

  useEffect(() => {
    newestMessageIdRef.current = lastMessageId ?? null
  }, [lastMessageId])

  const markRead = useCallback(
    async (messageId: string) => {
      if (!conversationId || lastMarkedReadRef.current === messageId) {
        return
      }

      lastMarkedReadRef.current = messageId
      try {
        await conversationsApi.markRead(conversationId, messageId)
        queryClient.invalidateQueries({ queryKey: ['conversations'] })
        queryClient.invalidateQueries({ queryKey: ['conversation', conversationId] })
      } catch {
        // Mất mạng: lần mở khung chat/quay lại tab kế tiếp sẽ đánh dấu lại.
        if (lastMarkedReadRef.current === messageId) {
          lastMarkedReadRef.current = null
        }
      }
    },
    [conversationId, queryClient],
  )

  // Đang mở khung chat và tab hiển thị → tin mới nhất được coi là đã đọc.
  useEffect(() => {
    if (lastMessageId && document.visibilityState === 'visible') {
      void markRead(lastMessageId)
    }
  }, [lastMessageId, markRead])

  // Quay lại tab sau khi tab bị ẩn → đánh dấu các tin đến trong lúc ẩn.
  useEffect(() => {
    const onVisibilityChange = () => {
      const newest = newestMessageIdRef.current
      if (document.visibilityState === 'visible' && newest) {
        void markRead(newest)
      }
    }

    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => document.removeEventListener('visibilitychange', onVisibilityChange)
  }, [markRead])

  // Đổi hội thoại → cho phép đánh dấu lại từ đầu ở hội thoại mới.
  useEffect(() => {
    lastMarkedReadRef.current = null
  }, [conversationId])

  const sendMutation = useMutation({
    mutationFn: (payload: { content: string; mediaIds: string[] }) =>
      conversationsApi.send(conversationId!, {
        // clientMessageId sinh ở client để chống trùng khi mất mạng (idempotent phía server).
        clientMessageId: crypto.randomUUID(),
        content: payload.content.length > 0 ? payload.content : null,
        mediaIds: payload.mediaIds,
      }),
    onSuccess: (message) => {
      setDraft('')
      setPendingImage(null)
      // Hiện tin của chính mình ngay lập tức (không phụ thuộc lần tải lại danh sách),
      // rồi đồng bộ lại để lấy dữ liệu chuẩn từ server.
      appendMessageToCache(queryClient, message)
      queryClient.invalidateQueries({ queryKey: ['messages', conversationId] })
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  const handleImage = async (files: FileList | null) => {
    const file = files?.[0]
    if (!file) {
      return
    }

    if (!ALLOWED_TYPES.includes(file.type)) {
      toast.error(t('social.bad_type'))
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      toast.error(t('social.too_large_image'))
      return
    }

    setUploading(true)
    try {
      const signature = await mediaApi.getSignature('Message')
      const uploaded = await mediaApi.upload(signature, file)
      const asset = await mediaApi.register({
        providerPublicId: uploaded.providerPublicId,
        url: uploaded.url,
        thumbnailUrl: uploaded.url,
        mimeType: uploaded.mimeType,
        sizeBytes: uploaded.sizeBytes,
        context: 'Message',
      })
      setPendingImage({ id: asset.id, url: asset.url })
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    } finally {
      setUploading(false)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }
    }
  }

  const submit = () => {
    const content = draft.trim()
    const mediaIds = pendingImage ? [pendingImage.id] : []

    if (content.length === 0 && mediaIds.length === 0) {
      return
    }

    // Gửi xong thì không còn "đang nhập" nữa.
    stopTyping()
    sendMutation.mutate({ content, mediaIds })
  }

  if (conversationQuery.isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
        <Spinner /> {t('social.loading')}
      </div>
    )
  }

  if (conversationQuery.isError || !conversationQuery.data) {
    return <Alert variant="error">{t('chat.not_found')}</Alert>
  }

  const conversation = conversationQuery.data
  const isGroup = conversation.type === 'Group'
  const peer = isGroup ? undefined : conversation.members.find((member) => member.userId !== me?.id)
  const peerName = isGroup
    ? conversation.title || t('chat.group')
    : peer?.displayName || peer?.userName || t('chat.title')

  // --- NC GĐ 7: trạng thái online + "đang nhập" ở đầu khung chat ---
  const typingNames = typingUsers
    .map((userId) => {
      const member = conversation.members.find((item) => item.userId === userId)
      return member?.displayName || member?.userName || ''
    })
    .filter((name) => name.length > 0)

  const presenceLabel = peer?.isOnline ? t('chat.online') : t('chat.offline')
  const onlineMemberCount = conversation.members.filter((member) => member.isOnline).length
  const peerStatus = !peer
    ? null
    : typingUsers.length > 0
      ? t('chat.typing')
      : peer.isOnline
        ? t('chat.online')
        : peer.lastSeenAt
          ? t('chat.last_seen', { time: timeAgo(peer.lastSeenAt, i18n.language) })
          : t('chat.offline')

  // NC GĐ 7: "Đã gửi"/"Đã đọc"/"Đã xem bởi N" chỉ hiện dưới tin CUỐI của chính mình.
  const lastOwnMessage = [...messages].reverse().find((message) => message.senderId === me?.id)
  const peerHasRead =
    !isGroup &&
    peer?.lastReadAt !== null &&
    peer?.lastReadAt !== undefined &&
    lastOwnMessage !== undefined &&
    Date.parse(peer.lastReadAt) >= Date.parse(lastOwnMessage.createdAt)
  const groupSeenCount =
    isGroup && lastOwnMessage
      ? conversation.members.filter(
          (member) =>
            member.userId !== me?.id &&
            member.lastReadAt !== null &&
            Date.parse(member.lastReadAt) >= Date.parse(lastOwnMessage.createdAt),
        ).length
      : 0
  const lastOwnStatus = !lastOwnMessage
    ? null
    : isGroup
      ? groupSeenCount > 0
        ? t('chat.seen_by', { count: groupSeenCount })
        : t('chat.sent')
      : peerHasRead
        ? t('chat.read')
        : t('chat.sent')

  return (
    <div className="mx-auto flex h-[calc(100vh-9rem)] max-w-2xl flex-col rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <header className="flex items-center gap-3 border-b border-slate-200 p-3 dark:border-slate-800">
        <Link to="/messages" className="rounded-lg p-1 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t('chat.back')}>
          <ArrowLeft className="h-5 w-5" />
        </Link>
        {isGroup ? (
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-300">
            <Users className="h-4 w-4" />
          </span>
        ) : (
          <span className="relative shrink-0">
            <Avatar name={peerName} url={peer?.avatarUrl} size="sm" />
            {peer && (
              <PresenceDot
                online={peer.isOnline}
                label={presenceLabel}
                className="absolute -bottom-0.5 -right-0.5"
              />
            )}
          </span>
        )}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{peerName}</p>
          {isGroup ? (
            <p className="truncate text-xs text-slate-500">
              {typingNames.length > 0
                ? t('chat.typing_many', { names: typingNames.join(', ') })
                : `${t('chat.member_count', { count: conversation.members.length })} · ${t(
                    'chat.online_count',
                    { count: onlineMemberCount },
                  )}`}
            </p>
          ) : (
            peer && (
              <p className="truncate text-xs text-slate-500">
                {peerStatus ?? `@${peer.userName}`}
              </p>
            )
          )}
        </div>

        {/* Gọi thoại/video ngay trong hội thoại (docs/05_api_contract.md mục 9.2) */}
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => startCall.mutate('Audio')}
            disabled={startCall.isPending}
            title={t('call.start_audio')}
            aria-label={t('call.start_audio')}
          >
            <Phone className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => startCall.mutate('Video')}
            disabled={startCall.isPending}
            title={t('call.start_video')}
            aria-label={t('call.start_video')}
          >
            <Video className="h-4 w-4" />
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto p-3">
        {messagesQuery.isLoading ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : messagesQuery.isError ? (
          <Alert variant="error">{t('chat.error')}</Alert>
        ) : messages.length === 0 ? (
          <p className="py-8 text-center text-sm text-slate-500">{t('chat.no_messages')}</p>
        ) : (
          <>
            {messagesQuery.hasNextPage && (
              <div className="mb-3 text-center">
                <button
                  type="button"
                  className="text-xs text-indigo-600 hover:underline disabled:opacity-60"
                  onClick={() => void messagesQuery.fetchNextPage()}
                  disabled={messagesQuery.isFetchingNextPage}
                >
                  {messagesQuery.isFetchingNextPage ? t('social.loading') : t('chat.load_older')}
                </button>
              </div>
            )}

            <ul className="space-y-2">
              {messages.map((message) => {
                const mine = message.senderId === me?.id
                // Trong nhóm, phải thấy tin này của ai (yêu cầu người dùng 2026-10-23).
                const sender = isGroup
                  ? conversation.members.find((member) => member.userId === message.senderId)
                  : undefined

                return (
                  <li key={message.id} className={mine ? 'flex justify-end' : 'flex justify-start'}>
                    <div
                      className={
                        mine
                          ? 'flex max-w-[75%] flex-col items-end'
                          : 'flex max-w-[75%] flex-col items-start'
                      }
                    >
                      {!mine && sender && (
                        <span className="mb-0.5 text-[11px] font-medium text-slate-500 dark:text-slate-400">
                          {sender.displayName || sender.userName}
                        </span>
                      )}
                      <div
                        className={
                          mine
                            ? 'rounded-2xl rounded-br-sm bg-indigo-600 px-3 py-2 text-sm text-white'
                            : 'rounded-2xl rounded-bl-sm bg-slate-100 px-3 py-2 text-sm text-slate-800 dark:bg-slate-800 dark:text-slate-100'
                        }
                      >
                      {message.attachments.length > 0 && (
                        <div className="mb-1 space-y-1">
                          {message.attachments.map((attachment) => (
                            <img
                              key={attachment.mediaId}
                              src={attachment.thumbnailUrl ?? attachment.url}
                              alt=""
                              className="max-h-60 rounded-lg object-cover"
                            />
                          ))}
                        </div>
                      )}
                      {message.content && (
                        <p className="whitespace-pre-wrap break-words">{message.content}</p>
                      )}
                      <span className={mine ? 'mt-1 block text-[10px] text-indigo-200' : 'mt-1 block text-[10px] text-slate-400'}>
                        {timeAgo(message.createdAt, i18n.language)}
                      </span>
                      {mine && lastOwnStatus && message.id === lastOwnMessage?.id && (
                        <span className="mt-0.5 block text-[10px] text-indigo-100">
                          {lastOwnStatus}
                        </span>
                      )}
                      </div>
                    </div>
                  </li>
                )
              })}
            </ul>
          </>
        )}
        <div ref={bottomRef} />
      </div>

      <footer className="border-t border-slate-200 p-3 dark:border-slate-800">
        {pendingImage && (
          <div className="mb-2 flex items-center gap-2">
            <img src={pendingImage.url} alt="" className="h-14 w-14 rounded-lg object-cover" />
            <button
              type="button"
              onClick={() => setPendingImage(null)}
              className="rounded-full bg-slate-200 p-1 text-slate-700 dark:bg-slate-700 dark:text-slate-200"
              aria-label={t('chat.remove_image')}
            >
              <X className="h-3 w-3" />
            </button>
          </div>
        )}

        <div className="flex items-center gap-2">
          <input
            ref={fileInputRef}
            type="file"
            accept={ALLOWED_TYPES.join(',')}
            hidden
            onChange={(event) => void handleImage(event.target.files)}
          />
          <Button
            variant="ghost"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading || sendMutation.isPending}
            aria-label={t('chat.add_image')}
          >
            {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
          </Button>

          <input
            value={draft}
            onChange={(event) => {
              setDraft(event.target.value)
              if (event.target.value.trim().length > 0) {
                notifyTyping()
              } else {
                // Xoá hết nội dung → không còn "đang nhập".
                stopTyping()
              }
            }}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault()
                submit()
              }
            }}
            placeholder={t('chat.placeholder')}
            maxLength={4000}
            className="h-10 min-w-0 flex-1 rounded-xl border border-slate-200 bg-slate-50 px-3 text-sm outline-none focus:border-indigo-400 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
          />

          <Button
            size="sm"
            onClick={submit}
            disabled={sendMutation.isPending || uploading || (draft.trim().length === 0 && !pendingImage)}
          >
            {sendMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {t('chat.send')}
          </Button>
        </div>
      </footer>
    </div>
  )
}
