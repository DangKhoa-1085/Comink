import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ImagePlus, Loader2, Send, X } from 'lucide-react'
import { conversationsApi, type ChatMessage } from '../api/conversations'
import { mediaApi } from '../api/media'
import { Avatar } from '../components/social/Avatar'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { timeAgo } from '../lib/format'
import { appendMessageToCache } from '../lib/messageCache'
import { useAuthStore } from '../stores/authStore'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Khung chat 1–1: lịch sử (cuộn lên tải thêm), gửi chữ và ảnh (docs/05_api_contract.md mục 8.5–8.6). */
export function ChatPage() {
  const { t, i18n } = useTranslation()
  const { conversationId } = useParams<{ conversationId: string }>()
  const me = useAuthStore((state) => state.user)
  const queryClient = useQueryClient()

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
  const peer = conversation.members.find((member) => member.userId !== me?.id)
  const peerName = peer?.displayName || peer?.userName || t('chat.title')

  return (
    <div className="mx-auto flex h-[calc(100vh-9rem)] max-w-2xl flex-col rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <header className="flex items-center gap-3 border-b border-slate-200 p-3 dark:border-slate-800">
        <Link to="/messages" className="rounded-lg p-1 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t('chat.back')}>
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <Avatar name={peerName} url={peer?.avatarUrl} size="sm" />
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{peerName}</p>
          {peer && <p className="truncate text-xs text-slate-500">@{peer.userName}</p>}
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
                return (
                  <li key={message.id} className={mine ? 'flex justify-end' : 'flex justify-start'}>
                    <div
                      className={
                        mine
                          ? 'max-w-[75%] rounded-2xl rounded-br-sm bg-indigo-600 px-3 py-2 text-sm text-white'
                          : 'max-w-[75%] rounded-2xl rounded-bl-sm bg-slate-100 px-3 py-2 text-sm text-slate-800 dark:bg-slate-800 dark:text-slate-100'
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
            onChange={(event) => setDraft(event.target.value)}
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
