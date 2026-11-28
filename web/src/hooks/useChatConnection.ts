import { useEffect, useRef, useState } from 'react'
import { useQueryClient, type InfiniteData } from '@tanstack/react-query'
import type { HubConnection } from '@microsoft/signalr'
import {
  type ChatMessage,
  type Conversation,
  type ConversationSummary,
  type PinnedMessages,
  type PresenceEvent,
  type ReadReceipt,
  type TypingEvent,
} from '../api/conversations'
import type { PagedResult } from '../api/types'
import type { CallDto } from '../api/calls'
import type { NotificationItem } from '../api/notifications'
import {
  createChatConnection,
  FRIENDSHIP_CHANGED_EVENT,
  LOCKET_CREATED_EVENT,
  LOCKET_DELETED_EVENT,
  LOCKET_UPDATED_EVENT,
  MESSAGE_CREATED_EVENT,
  MESSAGE_UPDATED_EVENT,
  MESSAGES_READ_EVENT,
  NOTIFICATION_CREATED_EVENT,
  PINNED_MESSAGES_CHANGED_EVENT,
  PRESENCE_CHANGED_EVENT,
  sendPresencePing,
  setActiveChatConnection,
  TYPING_CHANGED_EVENT,
} from '../lib/chatConnection'
import { appendMessageToCache, replaceMessageInCache, syncMissedMessages } from '../lib/messageCache'
import i18n from '../lib/i18n'
import { useAuthStore } from '../stores/authStore'
import { useCallStore } from '../stores/callStore'
import { useTypingStore } from '../stores/typingStore'
import { toast } from '../stores/toastStore'

/** Tên sự kiện cuộc gọi — khớp `CallEvents` ở backend. */
const CALL_INCOMING_EVENT = 'CallIncoming'
const CALL_UPDATED_EVENT = 'CallUpdated'
const CALL_ENDED_EVENT = 'CallEnded'

/** Tài khoản bị admin khóa — khớp `AdminEvents.AccountLocked` ở backend. */
const ACCOUNT_LOCKED_EVENT = 'AccountLocked'

/** Nhịp làm mới TTL presence (backend TTL 90 giây). */
const PRESENCE_PING_INTERVAL_MS = 30_000

export interface ChatConnectionState {
  /** Đang có kết nối tới hub. */
  isConnected: boolean
  /**
   * Đã từng kết nối thành công ít nhất một lần.
   * Dùng để **không** hiện dải "mất kết nối" trong khoảnh khắc đầu tiên lúc mới mở trang.
   */
  wasConnected: boolean
}

declare global {
  interface Window {
    /**
     * Công cụ CHỈ DÙNG KHI DEV để test mất mạng (Chrome DevTools không cắt được WebSocket
     * đang mở, nên phải ngắt từ phía client):
     * `await window.__cominkChat.disconnect()` — giả lập mất mạng;
     * `await window.__cominkChat.reconnect()` — kết nối lại và đồng bộ tin bị lỡ.
     */
    __cominkChat?: {
      disconnect: () => Promise<void>
      reconnect: () => Promise<void>
      syncNow: () => Promise<void>
    }
  }
}

/**
 * Mở một kết nối SignalR cho cả ứng dụng trong lúc đang đăng nhập.
 * Khi có `MessageCreated`, chèn tin vào cache và làm mới danh sách hội thoại.
 * Khi kết nối lại sau khi mất mạng, lấy các tin bị lỡ (`after` cursor).
 *
 * NC Giai đoạn 7: nhận `PresenceChanged` / `TypingChanged` / `MessagesRead`, gửi `Ping` định kỳ.
 */
export function useChatConnection(enabled: boolean): ChatConnectionState {
  const queryClient = useQueryClient()
  const [isConnected, setIsConnected] = useState(false)
  const [wasConnected, setWasConnected] = useState(false)
  // Công cụ dev cần truy cập kết nối hiện tại (đặt lại trong effect bên dưới).
  const controlRef = useRef<Window['__cominkChat']>(undefined)

  useEffect(() => {
    if (!enabled) {
      return
    }

    let disposed = false
    const connection: HubConnection = createChatConnection()
    let pingInterval: ReturnType<typeof setInterval> | null = null

    const markConnected = () => {
      if (!disposed) {
        setIsConnected(true)
        setWasConnected(true)
      }
    }

    /** Lấy tin bị lỡ cho các khung chat đang mở + làm mới danh sách hội thoại. */
    const syncNow = async () => {
      await syncMissedMessages(queryClient)
      await queryClient.invalidateQueries({ queryKey: ['conversations'] })
      // Trạng thái online có thể đã đổi trong lúc mất kết nối → đọc lại chi tiết đang mở.
      await queryClient.invalidateQueries({ queryKey: ['conversation'] })
      await queryClient.invalidateQueries({ queryKey: ['notifications'], refetchType: 'all' })
      await queryClient.invalidateQueries({ queryKey: ['friendships'], refetchType: 'all' })
    }

    const invalidateFriendshipQueries = () => {
      void queryClient.invalidateQueries({ queryKey: ['friendships'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['search-users'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['public-profile'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['feed'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['lockets'], refetchType: 'all' })
    }

    connection.on(MESSAGE_CREATED_EVENT, (message: ChatMessage) => {
      // Hiện tin ngay trong khung chat đang mở (không phải tải lại trang), rồi đồng bộ lại
      // với dữ liệu chuẩn từ server.
      appendMessageToCache(queryClient, message)
      queryClient.invalidateQueries({ queryKey: ['messages', message.conversationId] })
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })

    // --- NC GĐ 7: trạng thái online ---
    connection.on(PRESENCE_CHANGED_EVENT, (presence: PresenceEvent) => {
      // Danh sách hội thoại: cập nhật "người còn lại" của chat 1–1.
      queryClient.setQueriesData<InfiniteData<PagedResult<ConversationSummary>>>(
        { queryKey: ['conversations'] },
        (old) => {
          if (!old) {
            return old
          }

          return {
            ...old,
            pages: old.pages.map((page) => ({
              ...page,
              items: page.items.map((item) =>
                item.peer?.userId === presence.userId
                  ? {
                      ...item,
                      peer: {
                        ...item.peer,
                        isOnline: presence.isOnline,
                        lastSeenAt: presence.lastSeenAt,
                      },
                    }
                  : item,
              ),
            })),
          }
        },
      )

      // Khung chat đang mở: cập nhật thành viên tương ứng (1–1 và nhóm).
      queryClient.setQueriesData<Conversation>({ queryKey: ['conversation'] }, (old) => {
        if (!old) {
          return old
        }

        return {
          ...old,
          members: old.members.map((member) =>
            member.userId === presence.userId
              ? { ...member, isOnline: presence.isOnline, lastSeenAt: presence.lastSeenAt }
              : member,
          ),
        }
      })
    })

    // --- NC GĐ 7: "đang nhập" (tín hiệu tạm thời, store tự hết hạn) ---
    connection.on(TYPING_CHANGED_EVENT, (typing: TypingEvent) => {
      useTypingStore.getState().setTyping(typing.conversationId, typing.userId, typing.isTyping)
    })

    // --- NC GĐ 7: đã đọc ---
    connection.on(MESSAGES_READ_EVENT, (receipt: ReadReceipt) => {      queryClient.setQueriesData<Conversation>({ queryKey: ['conversation'] }, (old) => {
        if (!old || old.id !== receipt.conversationId) {
          return old
        }

        return {
          ...old,
          members: old.members.map((member) =>
            member.userId === receipt.userId
              ? {
                  ...member,
                  lastReadAt: receipt.readAt,
                  lastReadMessageId: receipt.messageId,
                }
              : member,
          ),
        }
      })

      // Số tin chưa đọc (badge) thay đổi với mọi tab của người vừa đọc.
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })

    // --- NC Ưu tiên 2: tin bị sửa / thu hồi / đổi cảm xúc ---
    connection.on(MESSAGE_UPDATED_EVENT, (message: ChatMessage) => {
      // Thay nguyên tử trong cache lịch sử + cập nhật trích dẫn đang trỏ tới tin này.
      replaceMessageInCache(queryClient, message)
      // Danh sách hội thoại có thể đổi preview (tin cuối bị thu hồi/sửa).
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })

    // --- NC Ưu tiên 2: danh sách ghim thay đổi (ghim/bỏ ghim/thu hồi tin đang ghim) ---
    connection.on(PINNED_MESSAGES_CHANGED_EVENT, (payload: PinnedMessages) => {
      queryClient.setQueryData(['pinned', payload.conversationId], payload)
    })

    // --- Comink Locket: ảnh mới, cảm xúc hoặc xóa sớm ---
    const refreshLockets = () => {
      void queryClient.invalidateQueries({ queryKey: ['lockets'], refetchType: 'all' })
    }
    connection.on(LOCKET_CREATED_EVENT, refreshLockets)
    connection.on(LOCKET_UPDATED_EVENT, refreshLockets)
    connection.on(LOCKET_DELETED_EVENT, refreshLockets)

    // --- Thông báo & lời mời kết bạn theo thời gian thực ---
    connection.on(NOTIFICATION_CREATED_EVENT, (notification: NotificationItem) => {
      queryClient.setQueryData<PagedResult<NotificationItem>>(['notifications'], (old) => {
        if (!old || old.items.some((item) => item.id === notification.id)) {
          return old
        }
        return { ...old, items: [notification, ...old.items] }
      })
      void queryClient.invalidateQueries({ queryKey: ['notifications'], refetchType: 'all' })

      if (
        notification.type === 'FriendRequestReceived' ||
        notification.type === 'FriendRequestAccepted'
      ) {
        invalidateFriendshipQueries()
      }
    })

    connection.on(FRIENDSHIP_CHANGED_EVENT, () => {
      invalidateFriendshipQueries()
    })

    // --- Cuộc gọi: cuộc gọi đến / cập nhật / kết thúc (GĐ 4) ---
    connection.on(CALL_INCOMING_EVENT, (call: CallDto) => {
      useCallStore.getState().setIncoming(call)
    })

    connection.on(CALL_UPDATED_EVENT, (call: CallDto) => {
      queryClient.invalidateQueries({ queryKey: ['call', call.id] })

      const incoming = useCallStore.getState().incoming
      if (!incoming || incoming.id !== call.id) {
        return
      }

      // ⚠️ Lỗi đã gặp (2026-10-23): trước đây đóng modal ngay khi `status !== 'Ringing'`,
      // nên vừa có MỘT người nghe là modal của những người còn lại biến mất và họ không thể
      // vào cuộc gọi nhóm nữa. Chỉ đóng khi cuộc gọi kết thúc, hoặc chính tôi đã trả lời/từ chối.
      const me = useAuthStore.getState().user?.id
      const mine = call.participants.find((participant) => participant.userId === me)
      const finished = ['Completed', 'Declined', 'Cancelled', 'Missed', 'Failed'].includes(call.status)

      if (finished || (mine !== undefined && mine.status !== 'Invited')) {
        useCallStore.getState().clearIncoming()
      }
    })

    connection.on(
      CALL_ENDED_EVENT,
      (ended: { callId: string; status?: string; endReason?: string }) => {
        const incoming = useCallStore.getState().incoming
        if (incoming?.id === ended.callId) {
          useCallStore.getState().clearIncoming()

          // Người bị gỡ thấy thông báo ngay cả khi đang mở web; app đóng nhận push
          // `MissedCall` từ backend (quyết định D3 — Ưu tiên 3).
          if (ended.status === 'Missed') {
            const caller = incoming.participants.find(
              (participant) => participant.userId === incoming.initiatedBy,
            )
            toast.info(
              i18n.t('call.missed_toast', {
                name: caller?.displayName ?? i18n.t('call.unknown_caller'),
              }),
            )
          }
        }

        queryClient.invalidateQueries({ queryKey: ['call', ended.callId] })
        queryClient.invalidateQueries({ queryKey: ['calls'], refetchType: 'all' })
        queryClient.invalidateQueries({ queryKey: ['messages'], refetchType: 'all' })
        queryClient.invalidateQueries({ queryKey: ['conversations'], refetchType: 'all' })
        queryClient.invalidateQueries({ queryKey: ['notifications'], refetchType: 'all' })
      },
    )

    // --- Quản trị (GĐ 5): tài khoản vừa bị khóa ---
    // Server đã thu hồi mọi refresh token và sẽ ngắt kết nối SignalR; client chủ động xóa phiên
    // ngay để không chờ tới request REST kế tiếp.
    connection.on(ACCOUNT_LOCKED_EVENT, () => {
      useAuthStore.getState().forceLogout()
      toast.error(i18n.t('errors.AUTH_ACCOUNT_LOCKED'))
    })

    connection.onreconnecting(() => {
      // Mất kết nối (mất mạng, ngủ máy, server restart…): SignalR sẽ tự thử lại.
      if (!disposed) {
        setIsConnected(false)
        // Không còn nhận tín hiệu gõ → tránh chữ "đang nhập" treo vĩnh viễn.
        useTypingStore.getState().clearAll()
      }
    })

    connection.onreconnected(() => {
      if (disposed) {
        return
      }

      markConnected()
      void sendPresencePing()
      void syncNow()
    })

    connection.onclose(() => {
      // Đóng hẳn (không tự kết nối lại được): hiện dải cảnh báo, KHÔNG tự tải lại trang.
      if (!disposed) {
        setIsConnected(false)
        useTypingStore.getState().clearAll()
      }
    })

    setActiveChatConnection(connection)

    // Tab quay lại foreground: gia hạn presence (có thể đã hết TTL khi tab bị treo).
    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible' && !disposed) {
        void sendPresencePing()
      }
    }

    void connection
      .start()
      .then(() => {
        if (disposed) {
          return
        }

        markConnected()
        // Làm mới TTL presence định kỳ để trạng thái online không hết hạn khi kết nối vẫn sống.
        pingInterval = setInterval(() => void sendPresencePing(), PRESENCE_PING_INTERVAL_MS)
        document.addEventListener('visibilitychange', onVisibilityChange)
      })
      .catch(() => {
        if (!disposed) {
          setIsConnected(false)
        }
      })

    // Công cụ test mất mạng chỉ tồn tại ở chế độ dev — không lọt vào bản build production.
    if (import.meta.env.DEV) {
      controlRef.current = {
        disconnect: () => connection.stop(),
        reconnect: async () => {
          await connection.start()
          markConnected()
          await syncNow()
        },
        syncNow,
      }
      window.__cominkChat = controlRef.current
    }

    return () => {
      disposed = true
      if (pingInterval) {
        clearInterval(pingInterval)
      }
      document.removeEventListener('visibilitychange', onVisibilityChange)
      setActiveChatConnection(null)
      useTypingStore.getState().clearAll()
      if (import.meta.env.DEV) {
        delete window.__cominkChat
      }
      controlRef.current = undefined
      void connection.stop()
    }
  }, [enabled, queryClient])

  return { isConnected, wasConnected }
}
