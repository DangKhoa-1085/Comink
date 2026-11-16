/**
 * Trạng thái "đang nhập" theo hội thoại (NC GĐ 7). Tín hiệu tạm thời, **không** vào TanStack Query:
 * server chỉ chuyển tiếp qua SignalR, client tự ẩn sau vài giây nếu không nhận thêm tín hiệu.
 *
 * Ẩn sau 6 giây > client throttle 5 giây của người gõ, nên chữ không nhấp nháy khi họ gõ liên tục.
 */
import { create } from 'zustand'

/** Thời gian tự ẩn nếu không có tín hiệu mới. */
const HIDE_AFTER_MS = 6000

interface TypingState {
  /** conversationId → danh sách userId đang gõ. */
  byConversation: Record<string, string[]>
  setTyping: (conversationId: string, userId: string, isTyping: boolean) => void
  /** Xoá hết (khi mất kết nối hoặc đăng xuất) để không còn chữ "đang nhập" treo. */
  clearAll: () => void
}

/** Bộ hẹn giờ ẩn từng cặp (hội thoại, người) — key `${conversationId}:${userId}`. */
const hideTimers = new Map<string, ReturnType<typeof setTimeout>>()

export const useTypingStore = create<TypingState>((set) => ({
  byConversation: {},

  setTyping: (conversationId, userId, isTyping) => {
    const key = `${conversationId}:${userId}`
    const existing = hideTimers.get(key)
    if (existing) {
      clearTimeout(existing)
      hideTimers.delete(key)
    }

    set((state) => {
      const current = state.byConversation[conversationId] ?? []
      const next = !isTyping
        ? current.filter((id) => id !== userId)
        : current.includes(userId)
          ? current
          : [...current, userId]

      return {
        byConversation: { ...state.byConversation, [conversationId]: next },
      }
    })

    if (isTyping) {
      hideTimers.set(
        key,
        setTimeout(() => {
          hideTimers.delete(key)
          set((state) => {
            const current = state.byConversation[conversationId] ?? []
            return {
              byConversation: {
                ...state.byConversation,
                [conversationId]: current.filter((id) => id !== userId),
              },
            }
          })
        }, HIDE_AFTER_MS),
      )
    }
  },

  clearAll: () => {
    for (const timer of hideTimers.values()) {
      clearTimeout(timer)
    }
    hideTimers.clear()
    set({ byConversation: {} })
  },
}))
