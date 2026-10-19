/** Gọi API nhắn tin — `/api/conversations` (docs/05_api_contract.md mục 8). */
import { http } from '../lib/http'
import type { PagedResult } from './types'

export interface ConversationPeer {
  userId: string
  userName: string
  displayName: string
  avatarUrl: string | null
}

export interface ConversationMember extends ConversationPeer {}

export interface MessageAttachment {
  mediaId: string
  url: string
  thumbnailUrl: string | null
  width: number | null
  height: number | null
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
}

export interface ConversationLastMessage {
  id: string
  senderId: string
  preview: string
  type: string
  createdAt: string
}

export interface ConversationSummary {
  id: string
  type: string
  title: string | null
  peer: ConversationPeer
  lastMessage: ConversationLastMessage | null
  unreadCount: number
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
}

export const conversationsApi = {
  /** Mở hội thoại 1–1 (chỉ bạn bè); đã có thì backend trả lại hội thoại cũ. */
  async openDirect(userId: string): Promise<Conversation> {
    const { data } = await http.post<Conversation>('/api/conversations/direct', { userId })
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
}
