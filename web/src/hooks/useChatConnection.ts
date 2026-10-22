import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { HubConnection } from '@microsoft/signalr'
import type { ChatMessage } from '../api/conversations'
import type { CallDto } from '../api/calls'
import { createChatConnection, MESSAGE_CREATED_EVENT } from '../lib/chatConnection'
import { appendMessageToCache, syncMissedMessages } from '../lib/messageCache'
import { useAuthStore } from '../stores/authStore'
import { useCallStore } from '../stores/callStore'

/** Tên sự kiện cuộc gọi — khớp `CallEvents` ở backend. */
const CALL_INCOMING_EVENT = 'CallIncoming'
const CALL_UPDATED_EVENT = 'CallUpdated'
const CALL_ENDED_EVENT = 'CallEnded'

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
    }

    connection.on(MESSAGE_CREATED_EVENT, (message: ChatMessage) => {
      // Hiện tin ngay trong khung chat đang mở (không phải tải lại trang), rồi đồng bộ lại
      // với dữ liệu chuẩn từ server.
      appendMessageToCache(queryClient, message)
      queryClient.invalidateQueries({ queryKey: ['messages', message.conversationId] })
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
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

    connection.on(CALL_ENDED_EVENT, (ended: { callId: string }) => {
      if (useCallStore.getState().incoming?.id === ended.callId) {
        useCallStore.getState().clearIncoming()
      }

      queryClient.invalidateQueries({ queryKey: ['call', ended.callId] })
      queryClient.invalidateQueries({ queryKey: ['calls'] })
    })

    connection.onreconnecting(() => {
      // Mất kết nối (mất mạng, ngủ máy, server restart…): SignalR sẽ tự thử lại.
      if (!disposed) {
        setIsConnected(false)
      }
    })

    connection.onreconnected(() => {
      if (disposed) {
        return
      }

      markConnected()
      void syncNow()
    })

    connection.onclose(() => {
      // Đóng hẳn (không tự kết nối lại được): hiện dải cảnh báo, KHÔNG tự tải lại trang.
      if (!disposed) {
        setIsConnected(false)
      }
    })

    void connection
      .start()
      .then(markConnected)
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
        syncNow
      }
      window.__cominkChat = controlRef.current
    }

    return () => {
      disposed = true
      if (import.meta.env.DEV) {
        delete window.__cominkChat
      }
      controlRef.current = undefined
      void connection.stop()
    }
  }, [enabled, queryClient])

  return { isConnected, wasConnected }
}
