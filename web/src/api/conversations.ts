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
  /** Vai trò trong nhóm: nhóm trưởng (`Owner`) hoặc thành viên (`Member`). */
  role?: 'Owner' | 'Admin' | 'Member'
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
  /** NC Ưu tiên 2: tôi đang tắt thông báo hội thoại này. */
  isMuted: boolean
  mutedUntil: string | null
  avatarUrl?: string | null
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
  isMuted: boolean
  mutedUntil: string | null
}

/** Một tin đang ghim; `message` là DTO tin đầy đủ. */
export interface PinnedMessage {
  message: ChatMessage
  pinnedByUserId: string
  pinnedByDisplayName: string
  pinnedAt: string
}

/** Payload của sự kiện `PinnedMessagesChanged` và kết quả endpoint ghim. */
export interface PinnedMessages {
  conversationId: string
  pinned: PinnedMessage[]
}

/** Một kết quả tìm kiếm tin nhắn (chỉ trong hội thoại tôi là thành viên). */
export interface MessageSearchResult {
  messageId: string
  conversationId: string
  conversationTitle: string | null
  conversationType: string
  peerDisplayName: string | null
  senderId: string
  senderDisplayName: string
  contentPreview: string | null
  createdAt: string
}

/** Cửa sổ tin quanh một tin đích (dùng khi nhảy từ kết quả tìm kiếm). */
export interface MessageContext {
  items: ChatMessage[]
  hasMoreBefore: boolean
  hasMoreAfter: boolean
}

/** Thời hạn tắt thông báo — phải khớp `MuteRules` ở backend. */
export type MuteDuration = 'Off' | 'OneHour' | 'EightHours' | 'OneWeek' | 'Forever'

export interface MuteState {
  isMuted: boolean
  mutedUntil: string | null
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

  async list(cursor?: string | null, limit = 20): Promise<PagedResult<ConversationSummary>> {
    const params: Record<string, string | number> = { limit }
    if (cursor) params.cursor = cursor
    const { data } = await http.get<PagedResult<ConversationSummary>>('/api/conversations', {
      params,
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

  /** Danh sách tin ghim (tối đa 10) — NC Ưu tiên 2. */
  async pinned(conversationId: string): Promise<PinnedMessages> {
    const { data } = await http.get<PinnedMessages>(`/api/conversations/${conversationId}/pinned`)
    return data
  },

  async pin(conversationId: string, messageId: string): Promise<PinnedMessages> {
    const { data } = await http.put<PinnedMessages>(
      `/api/conversations/${conversationId}/pinned/${messageId}`,
    )
    return data
  },

  async unpin(conversationId: string, messageId: string): Promise<PinnedMessages> {
    const { data } = await http.delete<PinnedMessages>(
      `/api/conversations/${conversationId}/pinned/${messageId}`,
    )
    return data
  },

  /** Chuyển tiếp tin sang hội thoại đích (server sao chép nội dung + tệp) — NC Ưu tiên 2. */
  async forward(
    targetConversationId: string,
    sourceMessageId: string,
    clientMessageId: string,
  ): Promise<ChatMessage> {
    const { data } = await http.post<ChatMessage>(
      `/api/conversations/${targetConversationId}/messages/forward`,
      { sourceMessageId, clientMessageId },
    )
    return data
  },

  /**
   * Tìm tin nhắn: `conversationId = null` thì tìm trong **mọi** hội thoại tôi còn là thành viên.
   * Gõ không dấu vẫn khớp (backend dùng unaccent + trigram).
   */
  async search(
    conversationId: string | null,
    q: string,
    cursor?: string | null,
  ): Promise<PagedResult<MessageSearchResult>> {
    const url = conversationId
      ? `/api/conversations/${conversationId}/messages/search`
      : '/api/messages/search'
    const params: Record<string, string | number> = { q, limit: 20 }
    if (cursor) params.cursor = cursor

    const { data } = await http.get<PagedResult<MessageSearchResult>>(url, { params })
    return data
  },

  /** Cửa sổ tin quanh một tin đích để mở đúng tin từ kết quả tìm kiếm — NC Ưu tiên 2. */
  async context(
    conversationId: string,
    messageId: string,
    limit = 31,
  ): Promise<MessageContext> {
    const { data } = await http.get<MessageContext>(
      `/api/conversations/${conversationId}/messages/${messageId}/context`,
      { params: { limit } },
    )
    return data
  },

  /** Tắt/bật thông báo hội thoại — chỉ chặn push, số chưa đọc không đổi. */
  async mute(conversationId: string, duration: MuteDuration): Promise<MuteState> {
    const { data } = await http.put<MuteState>(`/api/conversations/${conversationId}/mute`, {
      duration,
    })
    return data
  },

  /**
   * Đánh dấu đã đọc tới một tin (NC GĐ 7). Server chỉ **tiến** mốc nên gọi lặp lại vô hại;
   * 204 No Content kể cả khi mốc đã bằng hoặc mới hơn.
   */
  async markRead(conversationId: string, messageId: string): Promise<void> {
    await http.post(`/api/conversations/${conversationId}/read`, { messageId })
  },

  /** Cập nhật tên hoặc ảnh đại diện nhóm chat. */
  async updateGroup(
    conversationId: string,
    payload: { title?: string; avatarUrl?: string | null },
  ): Promise<Conversation> {
    const { data } = await http.patch<Conversation>(
      `/api/conversations/${conversationId}/group`,
      payload,
    )
    return data
  },

  /** Thêm bạn bè vào nhóm chat đã tạo. */
  async addGroupMembers(conversationId: string, memberIds: string[]): Promise<Conversation> {
    const { data } = await http.post<Conversation>(`/api/conversations/${conversationId}/members`, {
      memberIds,
    })
    return data
  },

  /** Nhóm trưởng xóa thành viên khỏi nhóm. */
  async removeGroupMember(conversationId: string, userId: string): Promise<Conversation> {
    const { data } = await http.delete<Conversation>(
      `/api/conversations/${conversationId}/members/${userId}`,
    )
    return data
  },

  /** Thành viên tự rời khỏi nhóm chat. */
  async leaveGroup(conversationId: string): Promise<void> {
    await http.post(`/api/conversations/${conversationId}/leave`)
  },
}
