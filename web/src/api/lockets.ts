/** API client cho tính năng Comink Locket (`/api/lockets`). */
import { http } from '../lib/http'
import type { PagedResult } from './types'

export type LocketAudienceType = 'AllFriends' | 'SelectedFriends'

export interface LocketAuthor {
  id: string
  userName: string
  displayName: string
  avatarUrl: string | null
}

export interface LocketReactionSummary {
  emoji: string
  count: number
  reactedByMe: boolean
}

export interface LocketMoment {
  id: string
  author: LocketAuthor
  imageUrl: string
  caption: string | null
  audienceType: LocketAudienceType
  createdAt: string
  expiresAt: string
  isMine: boolean
  reactions: LocketReactionSummary[]
}

export interface CreateLocketPayload {
  mediaAssetId: string
  caption?: string | null
  audienceType: LocketAudienceType
  recipientIds?: string[]
}

export interface LocketReplyResult {
  conversationId: string
  messageId: string
}

export const LOCKET_ALLOWED_REACTIONS = ['❤️', '😂', '😮', '🔥', '🥹'] as const

export const locketsApi = {
  async list(options?: { authorId?: string | null; cursor?: string | null; limit?: number }): Promise<PagedResult<LocketMoment>> {
    const params: Record<string, string | number> = { limit: options?.limit ?? 30 }
    if (options?.authorId) {
      params.authorId = options.authorId
    }
    if (options?.cursor) {
      params.cursor = options.cursor
    }
    const { data } = await http.get<PagedResult<LocketMoment>>('/api/lockets', { params })
    return data
  },

  async create(payload: CreateLocketPayload): Promise<LocketMoment> {
    const { data } = await http.post<LocketMoment>('/api/lockets', payload)
    return data
  },

  async react(momentId: string, emoji: string): Promise<LocketMoment> {
    const { data } = await http.post<LocketMoment>(`/api/lockets/${momentId}/reactions`, { emoji })
    return data
  },

  async reply(momentId: string, content: string): Promise<LocketReplyResult> {
    const { data } = await http.post<LocketReplyResult>(`/api/lockets/${momentId}/reply`, { content })
    return data
  },

  async remove(momentId: string): Promise<void> {
    await http.delete(`/api/lockets/${momentId}`)
  },
}
