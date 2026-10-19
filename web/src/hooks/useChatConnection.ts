import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { HubConnection } from '@microsoft/signalr'
import type { ChatMessage } from '../api/conversations'
import { createChatConnection, MESSAGE_CREATED_EVENT } from '../lib/chatConnection'
import { appendMessageToCache, syncMissedMessages } from '../lib/messageCache'

/**
 * Mở một kết nối SignalR cho cả ứng dụng trong lúc đang đăng nhập.
 * Khi có `MessageCreated`, làm mới danh sách hội thoại và khung chat tương ứng.
 * Khi kết nối lại sau khi mất mạng, làm mới danh sách để bắt tin bị lỡ.
 */
export function useChatConnection(enabled: boolean) {
  const queryClient = useQueryClient()
  const [isConnected, setIsConnected] = useState(false)

  useEffect(() => {
    if (!enabled) {
      return
    }

    let disposed = false
    const connection: HubConnection = createChatConnection()

    connection.on(MESSAGE_CREATED_EVENT, (message: ChatMessage) => {
      // Hiện tin ngay trong khung chat đang mở (không phải tải lại trang), rồi đồng bộ lại
      // với dữ liệu chuẩn từ server.
      appendMessageToCache(queryClient, message)
      queryClient.invalidateQueries({ queryKey: ['messages', message.conversationId] })
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })

    connection.onreconnected(() => {
      setIsConnected(true)
      // Đồng bộ phần bị lỡ: lấy các tin MỚI HƠN tin cuối đã có cho từng khung chat đang mở,
      // đồng thời làm mới danh sách hội thoại.
      void syncMissedMessages(queryClient)
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    })

    connection.onclose(() => setIsConnected(false))

    void connection
      .start()
      .then(() => {
        if (!disposed) {
          setIsConnected(true)
        }
      })
      .catch(() => {
        if (!disposed) {
          setIsConnected(false)
        }
      })

    return () => {
      disposed = true
      void connection.stop()
    }
  }, [enabled, queryClient])

  return isConnected
}
