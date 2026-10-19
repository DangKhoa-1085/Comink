/** Thao tác trên cache tin nhắn — hiển thị tin ngay, không phải chờ gọi lại API. */
import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import { conversationsApi, type ChatMessage } from '../api/conversations'
import type { PagedResult } from '../api/types'
import { encodeCursor } from './cursor'

type MessagesCache = InfiniteData<PagedResult<ChatMessage>>

/**
 * Chèn tin mới vào **trang đầu** của cache tin nhắn (trang đầu là mới nhất và sắp DESC,
 * nên chèn ở đầu mảng). Bỏ qua nếu tin đã có (chống trùng khi vừa setQueryData vừa nhận
 * sự kiện SignalR).
 */
export function appendMessageToCache(queryClient: QueryClient, message: ChatMessage): void {
  queryClient.setQueryData<MessagesCache>(['messages', message.conversationId], (old) => {
    if (!old || old.pages.length === 0) {
      return old
    }

    if (old.pages.some((page) => page.items.some((item) => item.id === message.id))) {
      return old
    }

    const [first, ...rest] = old.pages
    return { ...old, pages: [{ ...first, items: [message, ...first.items] }, ...rest] }
  })
}

/**
 * Sau khi mất kết nối rồi có lại: với mỗi khung chat đang mở, hỏi server **các tin mới hơn
 * tin cuối cùng đã có** (`after` cursor — docs/05_api_contract.md mục 8.5) rồi chèn vào cache.
 * Đây là phần "lấy tin bị lỡ" mà roadmap GĐ 3 yêu cầu.
 */
export async function syncMissedMessages(queryClient: QueryClient): Promise<void> {
  const entries = queryClient.getQueriesData<MessagesCache>({ queryKey: ['messages'] })

  for (const [queryKey, data] of entries) {
    const conversationId = queryKey[1]
    // Trang đầu là mới nhất và sắp DESC → phần tử [0] là tin mới nhất đang hiển thị.
    const newest = data?.pages?.[0]?.items?.[0]

    if (typeof conversationId !== 'string' || !newest) {
      continue
    }

    try {
      const page = await conversationsApi.messages(conversationId, {
        after: encodeCursor(newest.createdAt, newest.id),
        limit: 50,
      })

      for (const message of page.items) {
        appendMessageToCache(queryClient, message)
      }
    } catch {
      // Bỏ qua lỗi mạng: lần làm mới kế tiếp sẽ bù lại, không được làm hỏng UI.
    }
  }
}
