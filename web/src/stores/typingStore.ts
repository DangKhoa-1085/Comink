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

/**
 * Mảng rỗng DÙNG CHUNG cho selector.
 * ⚠️ Không được viết `?? []` trong selector: mỗi lần đọc trả một mảng mới → React
 * (`useSyncExternalStore`) thấy snapshot luôn "đổi" và render vô hạn
 * ("Maximum update depth exceeded" — lỗi thật gặp 2026-11-17 khi mở khung chat).
 */
export const EMPTY_TYPING_IDS: string[] = []

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
      const current = state.byConversation[conversationId] ?? EMPTY_TYPING_IDS

      if (!isTyping) {
        // Người này vốn không có trong danh sách → không tạo state mới (tránh render thừa).
        if (!current.includes(userId)) {
          return state
        }

        return {
          byConversation: {
            ...state.byConversation,
            [conversationId]: current.filter((id) => id !== userId),
          },
        }
      }

      // Đã có trong danh sách: chỉ cần gia hạn timer (làm ở trên), không đổi state.
      if (current.includes(userId)) {
        return state
      }

      return {
        byConversation: {
          ...state.byConversation,
          [conversationId]: [...current, userId],
        },
      }
    })

    if (isTyping) {
      hideTimers.set(
        key,
        setTimeout(() => {
          hideTimers.delete(key)
          set((state) => {
            const current = state.byConversation[conversationId] ?? EMPTY_TYPING_IDS
            if (!current.includes(userId)) {
              return state
            }
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
