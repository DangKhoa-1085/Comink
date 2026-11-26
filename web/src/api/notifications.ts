import { http } from '../lib/http'
import type { PagedResult } from './types'

/** Một thông báo trong hộp thư (`/api/notifications`). */
export interface NotificationItem {
  id: string
  type: string
  actorId: string | null
  actorDisplayName: string | null
  actorAvatarUrl: string | null
  entityType: string
  entityId: string | null
  data: string | null
  isRead: boolean
  createdAt: string
  readAt: string | null
}

/** Giải mã JSON trong trường `data` của thông báo (an toàn khi rỗng/lỗi). */
export function parseNotificationData(raw: string | null): Record<string, string> {
  if (!raw || !raw.trim()) {
    return {}
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return {}
    }

    const result: Record<string, string> = {}
    for (const [key, value] of Object.entries(parsed)) {
      if (value !== null && value !== undefined) {
        result[key] = String(value)
      }
    }
    return result
  } catch {
    return {}
  }
}

export const notificationsApi = {
  /** Lấy danh sách thông báo theo con trỏ `(created_at, id)`. */
  async list(cursor?: string | null, limit = 20): Promise<PagedResult<NotificationItem>> {
    const { data } = await http.get<PagedResult<NotificationItem>>('/api/notifications', {
      params: cursor ? { cursor, limit } : { limit },
    })
    return data
  },

  /** Đánh dấu một thông báo là đã đọc. */
  async markRead(id: string): Promise<void> {
    await http.post(`/api/notifications/${id}/read`)
  },
}
