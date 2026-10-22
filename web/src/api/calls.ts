/** Gọi API cuộc gọi — `/api/calls` (docs/05_api_contract.md mục 9). */
import { http } from '../lib/http'
import type { PagedResult } from './types'

export type CallType = 'Audio' | 'Video'

export interface CallParticipant {
  userId: string
  displayName: string
  status: string
  invitedAt: string
}

export interface CallDto {
  id: string
  conversationId: string
  type: CallType
  status: string
  roomName: string
  initiatedBy: string
  createdAt: string
  answeredAt: string | null
  endedAt: string | null
  durationSeconds: number | null
  endReason: string | null
  participants: CallParticipant[]
}

export interface CallToken {
  url: string
  token: string
  roomName: string
  expiresAt: string
}

export interface CallSummary {
  id: string
  conversationId: string
  type: CallType
  status: string
  endReason: string | null
  createdAt: string
  durationSeconds: number | null
  initiatedBy: string
  iWasInitiator: boolean
}

export interface StartCallPayload {
  conversationId?: string
  participantIds?: string[]
  type: CallType
}

export const callsApi = {
  /** Bắt đầu cuộc gọi 1–1 (trong hội thoại) hoặc nhóm (participantIds). */
  async start(payload: StartCallPayload): Promise<CallDto> {
    const { data } = await http.post<CallDto>('/api/calls', payload)
    return data
  },

  async get(callId: string): Promise<CallDto> {
    const { data } = await http.get<CallDto>(`/api/calls/${callId}`)
    return data
  },

  /** Token LiveKit ngắn hạn (10 phút) — gọi lại nếu cuộc gọi kéo dài. */
  async token(callId: string): Promise<CallToken> {
    const { data } = await http.post<CallToken>(`/api/calls/${callId}/token`)
    return data
  },

  async answer(callId: string): Promise<CallDto> {
    const { data } = await http.post<CallDto>(`/api/calls/${callId}/answer`)
    return data
  },

  async decline(callId: string): Promise<CallDto> {
    const { data } = await http.post<CallDto>(`/api/calls/${callId}/decline`)
    return data
  },

  async end(callId: string): Promise<CallDto> {
    const { data } = await http.post<CallDto>(`/api/calls/${callId}/end`)
    return data
  },

  async history(cursor?: string | null): Promise<PagedResult<CallSummary>> {
    const { data } = await http.get<PagedResult<CallSummary>>('/api/calls', {
      params: cursor ? { cursor } : {},
    })
    return data
  },
}
