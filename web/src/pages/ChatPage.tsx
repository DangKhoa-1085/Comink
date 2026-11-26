import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  Bell,
  BellOff,
  ImagePlus,
  Loader2,
  Pencil,
  Phone,
  Pin,
  Reply,
  Search,
  Send,
  Users,
  Video,
  X,
} from 'lucide-react'
import { callsApi, type CallType } from '../api/calls'
import {
  conversationsApi,
  type ChatMessage,
  type Conversation,
  type MessageContext,
  type MessageSearchResult,
  type MuteDuration,
} from '../api/conversations'
import { mediaApi } from '../api/media'
import { Avatar } from '../components/social/Avatar'
import { ConversationListPanel } from '../components/chat/ConversationListPanel'
import { ForwardDialog } from '../components/chat/ForwardDialog'
import { MessageBubble } from '../components/chat/MessageBubble'
import { MessageSearchPanel } from '../components/chat/MessageSearchPanel'
import { PinnedPanel } from '../components/chat/PinnedPanel'
import { PresenceDot } from '../components/chat/PresenceDot'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { toast } from '../stores/toastStore'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { timeAgo } from '../lib/format'
import { appendMessageToCache, replaceMessageInCache } from '../lib/messageCache'
import { sendTyping } from '../lib/chatConnection'
import { generateUuid } from '../lib/uuid'
import { useAuthStore } from '../stores/authStore'
import { EMPTY_TYPING_IDS, useTypingStore } from '../stores/typingStore'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Các lựa chọn tắt thông báo — khoá phải khớp `MuteRules` ở backend. */
const MUTE_OPTIONS: { duration: MuteDuration; label: string }[] = [
  { duration: 'Off', label: 'chat.mute_off' },
  { duration: 'OneHour', label: 'chat.mute_1h' },
  { duration: 'EightHours', label: 'chat.mute_8h' },
  { duration: 'OneWeek', label: 'chat.mute_1w' },
  { duration: 'Forever', label: 'chat.mute_forever' },
]

/** Đồng hồ cho throttle "đang nhập" — tách ngoài component để không bị coi là gọi trong render. */
function currentTimeMs(): number {
  return Date.now()
}

/** Khung chat 1–1 & nhóm (Split-Pane trên Desktop, toàn màn hình trên Mobile). */
export function ChatPage() {
  const { conversationId } = useParams<{ conversationId: string }>()

  // Đổi hội thoại → remount để xoá trạng thái soạn thảo (trả lời/sửa/nháp) của hội thoại cũ.
  return (
    <div className="flex h-full min-h-0 overflow-hidden bg-[#F7F8FC] dark:bg-[#0B0F1A]">
      <ConversationListPanel
        activeConversationId={conversationId}
        className="hidden lg:flex lg:w-[340px] lg:shrink-0 lg:border-r lg:border-slate-200/80 dark:lg:border-slate-800"
      />
      <div className="flex h-full min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <ChatPageView key={conversationId} />
      </div>
    </div>
  )
}

function ChatPageView() {
  const { t, i18n } = useTranslation()
  const { conversationId } = useParams<{ conversationId: string }>()
  const navigate = useNavigate()
  const me = useAuthStore((state) => state.user)
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const loadedFocusRef = useRef<string | null>(null)

  /** Hiện lỗi nghiệp vụ từ backend bằng chuỗi đã dịch (AGENTS.md mục 3). */
  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    toast.error(translateErrorCode(t, code))
  }

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
  // NC Ưu tiên 2: đang trả lời tin nào / đang sửa tin nào / tin đang được làm nổi bật khi nhảy tới.
  const [replyTo, setReplyTo] = useState<ChatMessage | null>(null)
  const [editing, setEditing] = useState<ChatMessage | null>(null)
  const [highlightedId, setHighlightedId] = useState<string | null>(null)
  // NC Ưu tiên 2 — Đợt B: panel ghim, tìm kiếm, chuyển tiếp, tắt thông báo, chế độ ngữ cảnh.
  const [pinnedOpen, setPinnedOpen] = useState(false)
  const [muteOpen, setMuteOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  /** Từ khoá đang được tô sáng trong nội dung tin (sau khi mở từ kết quả tìm kiếm). */
  const [searchTerm, setSearchTerm] = useState('')
  const [forwarding, setForwarding] = useState<ChatMessage | null>(null)
  const [context, setContext] = useState<{ targetId: string; data: MessageContext } | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const draftInputRef = useRef<HTMLInputElement | null>(null)
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

  // NC Ưu tiên 2: danh sách ghim (cập nhật real-time qua PinnedMessagesChanged) + thao tác.
  const pinnedQuery = useQuery({
    queryKey: ['pinned', conversationId],
    queryFn: () => conversationsApi.pinned(conversationId!),
    enabled: Boolean(conversationId),
  })
  const pinnedMessages = pinnedQuery.data?.pinned ?? []
  const pinnedIds = new Set(pinnedMessages.map((item) => item.message.id))

  const pinMutation = useMutation({
    mutationFn: (payload: { messageId: string; pinned: boolean }) =>
      payload.pinned
        ? conversationsApi.unpin(conversationId!, payload.messageId)
        : conversationsApi.pin(conversationId!, payload.messageId),
    onSuccess: (data) => {
      queryClient.setQueryData(['pinned', conversationId], data)
    },
    onError: showError,
  })

  const muteMutation = useMutation({
    mutationFn: (duration: MuteDuration) => conversationsApi.mute(conversationId!, duration),
    onSuccess: (state) => {
      queryClient.setQueryData<Conversation>(['conversation', conversationId], (old) =>
        old ? { ...old, isMuted: state.isMuted, mutedUntil: state.mutedUntil } : old,
      )
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      setMuteOpen(false)
    },
    onError: showError,
  })

  // Mở khung chat từ kết quả tìm kiếm toàn bộ (`/messages/{id}?focus={messageId}&q={query}`).
  useEffect(() => {
    const focus = searchParams.get('focus')
    const term = searchParams.get('q') ?? ''
    // Mỗi tin chỉ nạp ngữ cảnh một lần; KHÔNG xoá `focus` khỏi URL trong lúc fetch vì cleanup
    // của effect sẽ huỷ kết quả (lỗi thật gặp khi kiểm chứng 2026-11-20).
    if (!focus || !conversationId || loadedFocusRef.current === focus) {
      return
    }

    loadedFocusRef.current = focus
    // KHÔNG cleanup/huỷ fetch: StrictMode ở dev chạy effect 2 lần (setup → cleanup → setup),
    // nếu huỷ theo cleanup thì lần chạy lại bị chặn bởi loadedFocusRef và ngữ cảnh không bao giờ hiện
    // (lỗi thật gặp khi kiểm chứng 2026-11-20). Cập nhật state sau khi unmount là vô hại.
    const loadContext = async () => {
      try {
        const data = await conversationsApi.context(conversationId, focus, 31)
        setContext({ targetId: focus, data })
        setHighlightedId(focus)
        // Tô sáng đúng từ khoá người dùng đã tìm (không phân biệt dấu/hoa-thường).
        setSearchTerm(term)
        window.setTimeout(() => {
          document.getElementById(`message-${focus}`)?.scrollIntoView({ block: 'center' })
        }, 120)
      } catch {
        // Tin không còn (bị xoá cứng) → mở khung chat bình thường.
      }
    }

    void loadContext()
  }, [conversationId, searchParams])

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

    const now = currentTimeMs()
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

  // Chi tiết hội thoại đang mở: chỉ giữ trạng thái "đang sửa" khi tin còn tồn tại và chưa bị thu hồi.
  const activeEditing = editing
    ? (messages.find((message) => message.id === editing.id && message.deletedAt === null) ?? null)
    : null

  const sendMutation = useMutation({
    mutationFn: (payload: { content: string; mediaIds: string[] }) =>
      conversationsApi.send(conversationId!, {
        // clientMessageId sinh ở client để chống trùng khi mất mạng (idempotent phía server).
        clientMessageId: generateUuid(),
        content: payload.content.length > 0 ? payload.content : null,
        mediaIds: payload.mediaIds,
        replyToMessageId: replyTo?.id ?? null,
      }),
    onSuccess: (message) => {
      setDraft('')
      setPendingImage(null)
      setReplyTo(null)
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

  // --- NC Ưu tiên 2: sửa / thu hồi / cảm xúc ---
  const editMutation = useMutation({
    mutationFn: (payload: { message: ChatMessage; content: string }) =>
      conversationsApi.edit(conversationId!, payload.message.id, payload.content),
    onSuccess: (updated) => {
      replaceMessageInCache(queryClient, updated)
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      setDraft('')
      setEditing(null)
    },
    onError: showError,
  })

  const recallMutation = useMutation({
    mutationFn: (message: ChatMessage) => conversationsApi.recall(conversationId!, message.id),
    onSuccess: (updated) => {
      replaceMessageInCache(queryClient, updated)
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      // Huỷ trạng thái soạn thảo nếu đang trả lời/sửa chính tin vừa thu hồi.
      if (replyTo?.id === updated.id) {
        setReplyTo(null)
      }
      if (editing?.id === updated.id) {
        setEditing(null)
        setDraft('')
      }
    },
    onError: showError,
  })

  const reactionMutation = useMutation({
    mutationFn: (payload: { message: ChatMessage; reaction: string }) => {
      const mine = payload.message.reactions.some(
        (reaction) => reaction.reaction === payload.reaction && reaction.reactedByMe,
      )
      return mine
        ? conversationsApi.removeReaction(conversationId!, payload.message.id, payload.reaction)
        : conversationsApi.addReaction(conversationId!, payload.message.id, payload.reaction)
    },
    onSuccess: (updated) => {
      replaceMessageInCache(queryClient, updated)
    },
    onError: showError,
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

  /** Bắt đầu phản hồi / sửa; huỷ trạng thái soạn thảo. */
  const startReply = (message: ChatMessage) => {
    setEditing(null)
    setReplyTo(message)
    draftInputRef.current?.focus()
  }

  const startEdit = (message: ChatMessage) => {
    setReplyTo(null)
    setEditing(message)
    setDraft(message.content ?? '')
    draftInputRef.current?.focus()
  }

  const cancelComposerAction = () => {
    setReplyTo(null)
    if (editing) {
      setEditing(null)
      setDraft('')
    }
  }

  const confirmRecall = (message: ChatMessage) => {
    if (window.confirm(t('chat.recall_confirm'))) {
      recallMutation.mutate(message)
    }
  }

  /**
   * Nhảy tới một tin (từ trích dẫn hoặc kết quả tìm kiếm): cuộn nếu đã có trong DOM, ngược lại
   * tải **cửa sổ ngữ cảnh** quanh tin đó và hiện chế độ ngữ cảnh (không phải tải ngược nhiều trang).
   */
  const jumpToMessage = async (messageId: string) => {
    const scrollTo = () => {
      const element = document.getElementById(`message-${messageId}`)
      if (!element) {
        return false
      }

      element.scrollIntoView({ block: 'center', behavior: 'smooth' })
      setHighlightedId(messageId)
      window.setTimeout(() => setHighlightedId(null), 2500)
      return true
    }

    if (scrollTo()) {
      return
    }

    try {
      const data = await conversationsApi.context(conversationId!, messageId, 31)
      setContext({ targetId: messageId, data })
      setHighlightedId(messageId)
      window.setTimeout(() => {
        document.getElementById(`message-${messageId}`)?.scrollIntoView({ block: 'center' })
      }, 120)
      window.setTimeout(() => setHighlightedId(null), 3000)
    } catch (error) {
      showError(error)
    }
  }

  const pickSearchResult = (result: MessageSearchResult, query: string) => {
    setSearchOpen(false)
    setSearchTerm(query)
    if (result.conversationId === conversationId) {
      void jumpToMessage(result.messageId)
      return
    }

    navigate(
      `/messages/${result.conversationId}?focus=${result.messageId}&q=${encodeURIComponent(query)}`,
    )
  }

  const submit = () => {
    const content = draft.trim()
    const mediaIds = pendingImage ? [pendingImage.id] : []

    if (content.length === 0 && mediaIds.length === 0) {
      return
    }

    // Gửi xong thì không còn "đang nhập" nữa.
    stopTyping()

    // Đang sửa: chỉ đổi nội dung, không tạo tin mới (NC Ưu tiên 2).
    if (activeEditing) {
      editMutation.mutate({ message: activeEditing, content })
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
  const isGroup = conversation.type === 'Group'
  const isMuted = conversation.isMuted
  // Chế độ ngữ cảnh (mở từ kết quả tìm kiếm): hiển thị cửa sổ quanh tin đích thay cho lịch sử cuộn.
  const visibleMessages = context ? context.data.items : messages
  const peer = isGroup ? undefined : conversation.members.find((member) => member.userId !== me?.id)
  const peerName = isGroup
    ? conversation.title || t('chat.group')
    : peer?.displayName || peer?.userName || t('chat.title')

  // --- NC GĐ 7: "đang nhập" hiển thị NGAY TRÊN KHUNG NHẬP (không thay dòng trạng thái ở tiêu đề) ---
  const typingNames = typingUsers
    .map((userId) => {
      const member = conversation.members.find((item) => item.userId === userId)
      return member?.displayName || member?.userName || ''
    })
    .filter((name) => name.length > 0)

  const typingText = isGroup
    ? typingNames.length === 1
      ? `${typingNames[0]} ${t('chat.typing')}`
      : typingNames.length > 1
        ? `${typingNames.slice(0, 3).join(', ')}${
            typingNames.length > 3
              ? ` ${t('chat.and_more', { count: typingNames.length - 3 })}`
              : ''
          } ${t('chat.typing_many')}`
        : typingUsers.length > 0
          ? t('chat.typing_many')
          : ''
    : typingUsers.length > 0
      ? `${peerName} ${t('chat.typing')}`
      : ''

  const presenceLabel = peer?.isOnline ? t('chat.online') : t('chat.offline')
  const onlineMemberCount = conversation.members.filter((member) => member.isOnline).length
  // Tiêu đề chỉ còn trạng thái presence; "đang nhập" đã chuyển xuống trên khung nhập.
  const peerStatus = !peer
    ? null
    : peer.isOnline
      ? t('chat.online')
      : peer.lastSeenAt
        ? t('chat.last_seen', { time: timeAgo(peer.lastSeenAt, i18n.language) })
        : t('chat.offline')

  // NC GĐ 7: "Đã gửi"/"Đã đọc"/"Đã xem bởi <tên>" chỉ hiện dưới tin CUỐI của chính mình.
  const lastOwnMessage = [...messages].reverse().find((message) => message.senderId === me?.id)
  const peerHasRead =
    !isGroup &&
    peer?.lastReadAt !== null &&
    peer?.lastReadAt !== undefined &&
    lastOwnMessage !== undefined &&
    Date.parse(peer.lastReadAt) >= Date.parse(lastOwnMessage.createdAt)

  // Nhóm: liệt kê TÊN người đã đọc (mốc đọc là mốc mới nhất của từng người).
  const groupSeenNames =
    isGroup && lastOwnMessage
      ? conversation.members
          .filter((member) => {
            if (member.userId === me?.id || member.lastReadAt === null) {
              return false
            }
            return Date.parse(member.lastReadAt) >= Date.parse(lastOwnMessage.createdAt)
          })
          .sort((first, second) => Date.parse(first.lastReadAt ?? '') - Date.parse(second.lastReadAt ?? ''))
          .map((member) => member.displayName || member.userName)
          .filter((name) => name.length > 0)
      : []

  const lastOwnStatus = !lastOwnMessage
    ? null
    : isGroup
      ? groupSeenNames.length === 0
        ? t('chat.sent')
        : `${t('chat.seen_by')} ${groupSeenNames.slice(0, 3).join(', ')}${
            groupSeenNames.length > 3
              ? ` ${t('chat.and_more', { count: groupSeenNames.length - 3 })}`
              : ''
          }`
      : peerHasRead
        ? t('chat.read')
        : t('chat.sent')

  // Khi danh sách bị rút gọn, rê chuột vào thấy đầy đủ tên.
  const lastOwnStatusTitle =
    isGroup && groupSeenNames.length > 3
      ? `${t('chat.seen_by')} ${groupSeenNames.join(', ')}`
      : undefined

  return (
    <div className="flex h-full min-h-0 flex-1 flex-col overflow-hidden bg-[#F7F8FC] dark:bg-[#0B0F1A]">
      <header className="flex h-16 shrink-0 items-center gap-3 border-b border-slate-200/80 bg-white/85 px-4 backdrop-blur-md dark:border-slate-800 dark:bg-[#121826]/85">
        <Link
          to="/messages"
          className="rounded-xl p-1.5 hover:bg-slate-100 dark:hover:bg-slate-800 lg:hidden"
          aria-label={t('chat.back')}
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
        {!isGroup && peer ? (
          <Link
            to={`/users/${peer.userId}`}
            className="group -ml-1.5 flex min-w-0 flex-1 items-center gap-3 rounded-xl px-1.5 py-1 transition-colors hover:bg-slate-100/80 dark:hover:bg-slate-800/70"
          >
            <span className="relative shrink-0">
              <Avatar name={peerName} url={peer.avatarUrl} size="sm" />
              <PresenceDot
                online={peer.isOnline}
                label={presenceLabel}
                className="absolute -bottom-0.5 -right-0.5"
              />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold transition-colors group-hover:text-sky-600 dark:group-hover:text-sky-400">
                {peerName}
              </p>
              <p className="truncate text-xs text-slate-500">
                {peerStatus ?? `@${peer.userName}`}
              </p>
            </div>
          </Link>
        ) : (
          <div className="flex min-w-0 flex-1 items-center gap-3">
            {isGroup ? (
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300">
                <Users className="h-4 w-4" />
              </span>
            ) : (
              <Avatar name={peerName} url={null} size="sm" />
            )}
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold">{peerName}</p>
              {isGroup && (
                <p className="truncate text-xs text-slate-500">
                  {`${t('chat.member_count', { count: conversation.members.length })} · ${t(
                    'chat.online_count',
                    { count: onlineMemberCount },
                  )}`}
                </p>
              )}
            </div>
          </div>
        )}

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

          {/* NC Ưu tiên 2: ghim, tìm kiếm trong hội thoại, tắt thông báo. */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setPinnedOpen((open) => !open)}
            title={t('chat.pinned')}
            aria-label={t('chat.pinned')}
            aria-expanded={pinnedOpen}
            className="relative"
          >
            <Pin className="h-4 w-4" />
            {pinnedMessages.length > 0 && (
              <span className="absolute -right-1 -top-1 rounded-full bg-sky-600 px-1 text-[9px] font-semibold leading-4 text-white dark:bg-sky-500">
                {pinnedMessages.length}
              </span>
            )}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setSearchOpen(true)}
            title={t('chat.search_in_conversation')}
            aria-label={t('chat.search_in_conversation')}
          >
            <Search className="h-4 w-4" />
          </Button>
          <div className="relative">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setMuteOpen((open) => !open)}
              title={isMuted ? t('chat.unmute') : t('chat.mute')}
              aria-label={isMuted ? t('chat.unmute') : t('chat.mute')}
              aria-expanded={muteOpen}
            >
              {isMuted ? <BellOff className="h-4 w-4" /> : <Bell className="h-4 w-4" />}
            </Button>
            {muteOpen && (
              <div
                role="menu"
                className="absolute right-0 top-full z-30 mt-1 w-48 overflow-hidden rounded-xl border border-slate-200 bg-white p-1 text-sm text-slate-700 shadow-lg dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
              >
                {MUTE_OPTIONS.map((option) => (
                  <button
                    key={option.duration}
                    type="button"
                    role="menuitem"
                    onClick={() => muteMutation.mutate(option.duration)}
                    className="block w-full rounded-lg px-2 py-1.5 text-left hover:bg-slate-100 dark:hover:bg-slate-800"
                  >
                    {t(option.label)}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </header>

      {pinnedOpen && (
        <PinnedPanel
          pinned={pinnedMessages}
          loading={pinnedQuery.isLoading}
          error={pinnedQuery.isError}
          onClose={() => setPinnedOpen(false)}
          onJump={(messageId) => void jumpToMessage(messageId)}
          onUnpin={(messageId) => pinMutation.mutate({ messageId, pinned: true })}
        />
      )}

      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
        {messagesQuery.isLoading ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500">
            <Spinner /> {t('social.loading')}
          </div>
        ) : messagesQuery.isError ? (
          <Alert variant="error">{t('chat.error')}</Alert>
        ) : visibleMessages.length === 0 ? (
          <p className="py-8 text-center text-sm text-slate-500">{t('chat.no_messages')}</p>
        ) : (
          <>
            {context ? (
              <div className="mb-2 flex items-center justify-between gap-2 rounded-lg bg-amber-50 px-2 py-1 text-xs text-amber-700 dark:bg-amber-950 dark:text-amber-200">
                <span>{t('chat.context_banner')}</span>
                <button
                  type="button"
                  onClick={() => {
                    setContext(null)
                    setSearchTerm('')
                  }}
                  className="shrink-0 font-semibold underline"
                >
                  {t('chat.context_latest')}
                </button>
              </div>
            ) : (
              messagesQuery.hasNextPage && (
                <div className="mb-3 text-center">
                  <button
                    type="button"
                    className="text-xs font-semibold text-sky-600 hover:underline disabled:opacity-60 dark:text-sky-400"
                    onClick={() => void messagesQuery.fetchNextPage()}
                    disabled={messagesQuery.isFetchingNextPage}
                  >
                    {messagesQuery.isFetchingNextPage ? t('social.loading') : t('chat.load_older')}
                  </button>
                </div>
              )
            )}

            <ul className="space-y-2">
              {visibleMessages.map((message) => {
                const mine = message.senderId === me?.id
                // Trong nhóm, phải thấy tin này của ai (yêu cầu người dùng 2026-10-23).
                const sender = isGroup
                  ? conversation.members.find((member) => member.userId === message.senderId)
                  : undefined
                const isLastOwn = mine && message.id === lastOwnMessage?.id

                return (
                  <MessageBubble
                    key={message.id}
                    message={message}
                    isMine={mine}
                    isPinned={pinnedIds.has(message.id)}
                    senderName={sender?.displayName || sender?.userName}
                    showSender={isGroup && !mine}
                    readStatus={isLastOwn ? lastOwnStatus ?? undefined : undefined}
                    readStatusTitle={isLastOwn ? lastOwnStatusTitle : undefined}
                    highlighted={highlightedId === message.id}
                    highlightTerm={searchTerm || undefined}
                    language={i18n.language}
                    onReply={startReply}
                    onEdit={startEdit}
                    onRecall={confirmRecall}
                    onReact={(target, reaction) => reactionMutation.mutate({ message: target, reaction })}
                    onJumpTo={(messageId) => void jumpToMessage(messageId)}
                    onPin={(target) => pinMutation.mutate({ messageId: target.id, pinned: false })}
                    onUnpin={(target) => pinMutation.mutate({ messageId: target.id, pinned: true })}
                    onForward={setForwarding}
                  />
                )
              })}
            </ul>
          </>
        )}
        <div ref={bottomRef} />
      </div>

      <footer className="shrink-0 border-t border-slate-200/80 bg-white px-4 py-3 dark:border-slate-800 dark:bg-[#121826]">
        {(replyTo || activeEditing) && (
          <div className="mb-2 flex items-start gap-2 rounded-xl border-l-2 border-sky-500 bg-slate-50 px-3 py-1.5 dark:bg-[#1A2234]">
            <div className="min-w-0 flex-1">
              <p className="flex items-center gap-1 text-xs font-semibold text-sky-700 dark:text-sky-300">
                {activeEditing ? <Pencil className="h-3 w-3" /> : <Reply className="h-3 w-3" />}
                {activeEditing
                  ? t('chat.editing')
                  : `${t('chat.reply_to')} ${
                      conversation.members.find((member) => member.userId === replyTo?.senderId)
                        ?.displayName ?? ''
                    }`}
              </p>
              <p className="line-clamp-1 text-xs text-slate-500 dark:text-slate-400">
                {(activeEditing ?? replyTo)?.content || t('chat.image_preview')}
              </p>
            </div>
            <button
              type="button"
              onClick={cancelComposerAction}
              className="rounded-full p-1 text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-800"
              aria-label={t('chat.cancel')}
            >
              <X className="h-3 w-3" />
            </button>
          </div>
        )}

        {/*
          "Đang nhập" ngay trên khung nhập (theo yêu cầu 2026-11-17). Giữ chiều cao cố định
          để ô nhập không bị giật lên/xuống khi có/không có người gõ.
        */}
        <p
          className="h-5 truncate px-1 text-xs text-slate-500 dark:text-slate-400"
          aria-live="polite"
        >
          {typingText}
        </p>

        {pendingImage && !activeEditing && (
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
            disabled={uploading || sendMutation.isPending || activeEditing !== null}
            aria-label={t('chat.add_image')}
          >
            {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
          </Button>

          <input
            ref={draftInputRef}
            value={draft}
            onChange={(event) => {
              setDraft(event.target.value)
              if (event.target.value.trim().length > 0 && !activeEditing) {
                notifyTyping()
              } else {
                // Xoá hết nội dung (hoặc đang sửa) → không còn "đang nhập".
                stopTyping()
              }
            }}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault()
                submit()
              }
            }}
            placeholder={activeEditing ? t('chat.edit_placeholder') : t('chat.placeholder')}
            maxLength={4000}
            className="h-10 min-w-0 flex-1 rounded-xl border border-slate-200 bg-slate-50 px-3.5 text-sm outline-none focus:border-sky-500 focus:bg-white dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-100 dark:focus:bg-[#0B0F1A]"
          />

          <Button
            size="sm"
            onClick={submit}
            disabled={
              sendMutation.isPending ||
              editMutation.isPending ||
              uploading ||
              (draft.trim().length === 0 && !pendingImage)
            }
          >
            {sendMutation.isPending || editMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Send className="h-4 w-4" />
            )}
            {activeEditing ? t('chat.save') : t('chat.send')}
          </Button>
        </div>
      </footer>

      {searchOpen && (
        <MessageSearchPanel
          conversationId={conversationId!}
          onClose={() => setSearchOpen(false)}
          onPick={pickSearchResult}
        />
      )}

      {forwarding && (
        <ForwardDialog
          sourceConversationId={conversationId!}
          message={forwarding}
          onClose={() => setForwarding(null)}
          onForwarded={(forwarded) => {
            if (forwarded.conversationId === conversationId) {
              appendMessageToCache(queryClient, forwarded)
            }
            queryClient.invalidateQueries({ queryKey: ['conversations'] })
          }}
        />
      )}
    </div>
  )
}
