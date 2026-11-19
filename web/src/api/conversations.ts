/** Gọi API nhắn tin — `/api/conversations` (docs/05_api_contract.md mục 8). */
import { http } from '../lib/http'
import type { PagedResult } from './types'

export interface ConversationPeer {
  userId: string
  userName: string
  displayName: string
  avatarUrl: string | null
  /** Trạng thái online (NC GĐ 7) — chỉ đúng cho người cùng hội thoại. */
  isOnline: boolean
  lastSeenAt: string | null
}

export interface ConversationMember extends ConversationPeer {
  /** Mốc đã đọc của chính thành viên này (dùng hiển thị "Đã đọc"/"Đã xem bởi N"). */
  lastReadMessageId: string | null
  lastReadAt: string | null
}

export interface MessageAttachment {
  mediaId: string
  url: string
  thumbnailUrl: string | null
  width: number | null
  height: number | null
}

/** Ảnh chụp tin được phản hồi (NC Ưu tiên 2). */
export interface MessageQuote {
  messageId: string
  senderId: string
  senderDisplayName: string
  contentPreview: string | null
  type: string
  isDeleted: boolean
}

/** Nguồn của tin chuyển tiếp (NC Ưu tiên 2). */
export interface ForwardedFrom {
  messageId: string
  senderId: string
  senderDisplayName: string
}

/** Tổng hợp một loại cảm xúc trên tin. */
export interface ChatReaction {
  reaction: string
  emoji: string
  count: number
  reactedByMe: boolean
}

export interface ChatMessage {
  id: string
  conversationId: string
  senderId: string
  type: string
  content: string | null
  attachments: MessageAttachment[]
  clientMessageId: string
  createdAt: string
  replyTo: MessageQuote | null
  forwardedFrom: ForwardedFrom | null
  editedAt: string | null
  /** Khác null = tin đã bị thu hồi, mọi trường nội dung đã rỗng. */
  deletedAt: string | null
  reactions: ChatReaction[]
}

export interface ConversationLastMessage {
  id: string
  senderId: string
  preview: string
  type: string
  createdAt: string
  isDeleted: boolean
}

export interface ConversationSummary {
  id: string
  type: string
  title: string | null
  /** Chỉ có với hội thoại 1–1; nhóm dùng `title` + `memberCount`. */
  peer: ConversationPeer | null
  lastMessage: ConversationLastMessage | null
  unreadCount: number
  memberCount: number
}

export interface Conversation {
  id: string
  type: string
  title: string | null
  avatarUrl: string | null
  createdAt: string
  lastMessageAt: string | null
  members: ConversationMember[]
  lastMessage: ChatMessage | null
}

export interface SendMessagePayload {
  clientMessageId: string
  content?: string | null
  mediaIds?: string[]
  /** Tin được phản hồi (NC Ưu tiên 2). */
  replyToMessageId?: string | null
}

/** Sự kiện real-time của phần nhắn tin nâng cao (khớp `ChatEvents` ở backend). */
export interface PresenceEvent {
  userId: string
  isOnline: boolean
  lastSeenAt: string | null
}

export interface TypingEvent {
  conversationId: string
  userId: string
  isTyping: boolean
}

export interface ReadReceipt {
  conversationId: string
  userId: string
  messageId: string
  readAt: string
}

export const conversationsApi = {
  /** Mở hội thoại 1–1 (chỉ bạn bè); đã có thì backend trả lại hội thoại cũ. */
  async openDirect(userId: string): Promise<Conversation> {
    const { data } = await http.post<Conversation>('/api/conversations/direct', { userId })
    return data
  },

  /** Tạo nhóm chat (NC GĐ 3): 3–10 thành viên, tất cả phải là bạn bè. */
  async createGroup(name: string, participantIds: string[]): Promise<Conversation> {
    const { data } = await http.post<Conversation>('/api/conversations/group', {
      name,
      participantIds,
    })
    return data
  },

  async list(cursor?: string | null): Promise<PagedResult<ConversationSummary>> {
    const { data } = await http.get<PagedResult<ConversationSummary>>('/api/conversations', {
      params: cursor ? { cursor } : {},
    })
    return data
  },

  async get(conversationId: string): Promise<Conversation> {
    const { data } = await http.get<Conversation>(`/api/conversations/${conversationId}`)
    return data
  },

  /** Lịch sử tin: `before` để tải cũ hơn, `after` để đồng bộ tin bị lỡ. */
  async messages(
    conversationId: string,
    options: { before?: string | null; after?: string | null; limit?: number } = {},
  ): Promise<PagedResult<ChatMessage>> {
    const params: Record<string, string | number> = { limit: options.limit ?? 30 }
    if (options.before) params.before = options.before
    if (options.after) params.after = options.after

    const { data } = await http.get<PagedResult<ChatMessage>>(
      `/api/conversations/${conversationId}/messages`,
      { params },
    )
    return data
  },

  /** Gửi tin; `clientMessageId` do client sinh để chống trùng khi mất mạng. */
  async send(conversationId: string, payload: SendMessagePayload): Promise<ChatMessage> {
    const { data } = await http.post<ChatMessage>(
      `/api/conversations/${conversationId}/messages`,
      payload,
    )
    return data
  },

  /** Sửa nội dung tin của mình (trong 24 giờ) — NC Ưu tiên 2. */
  async edit(conversationId: string, messageId: string, content: string): Promise<ChatMessage> {
    const { data } = await http.patch<ChatMessage>(
      `/api/conversations/${conversationId}/messages/${messageId}`,
      { content },
    )
    return data
  },

  /** Thu hồi tin của mình (trong 24 giờ) — NC Ưu tiên 2. */
  async recall(conversationId: string, messageId: string): Promise<ChatMessage> {
    const { data } = await http.delete<ChatMessage>(
      `/api/conversations/${conversationId}/messages/${messageId}`,
    )
    return data
  },

  /** Thêm một cảm xúc (idempotent) — NC Ưu tiên 2. */
  async addReaction(
    conversationId: string,
    messageId: string,
    reaction: string,
  ): Promise<ChatMessage> {
    const { data } = await http.post<ChatMessage>(
      `/api/conversations/${conversationId}/messages/${messageId}/reactions`,
      { reaction },
    )
    return data
  },

  /** Bỏ cảm xúc của mình (idempotent) — NC Ưu tiên 2. */
  async removeReaction(
    conversationId: string,
    messageId: string,
    reaction: string,
  ): Promise<ChatMessage> {
    const { data } = await http.delete<ChatMessage>(
      `/api/conversations/${conversationId}/messages/${messageId}/reactions/${encodeURIComponent(reaction)}`,
    )
    return data
  },

  /**
   * Đánh dấu đã đọc tới một tin (NC GĐ 7). Server chỉ **tiến** mốc nên gọi lặp lại vô hại;
   * 204 No Content kể cả khi mốc đã bằng hoặc mới hơn.
   */
  async markRead(conversationId: string, messageId: string): Promise<void> {
    await http.post(`/api/conversations/${conversationId}/read`, { messageId })
  },
}
