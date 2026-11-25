/** Gọi API bạn bè — `/api/friendships` (docs/05_api_contract.md mục 7.2). */
import { http } from '../lib/http'
import type { Friendship, PagedResult } from './types'

export const friendshipsApi = {
  async sendRequest(addresseeId: string): Promise<Friendship> {
    const { data } = await http.post<Friendship>('/api/friendships/requests', { addresseeId })
    return data
  },

  async accept(requestId: string): Promise<Friendship> {
    const { data } = await http.post<Friendship>(`/api/friendships/requests/${requestId}/accept`)
    return data
  },

  async removeRequest(requestId: string): Promise<void> {
    await http.delete(`/api/friendships/requests/${requestId}`)
  },

  async removeFriend(userId: string): Promise<void> {
    await http.delete(`/api/friendships/${userId}`)
  },

  async list(cursor?: string | null): Promise<PagedResult<Friendship>> {
    const { data } = await http.get<PagedResult<Friendship>>('/api/friendships', {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async incoming(cursor?: string | null): Promise<PagedResult<Friendship>> {
    const { data } = await http.get<PagedResult<Friendship>>('/api/friendships/requests/incoming', {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async outgoing(cursor?: string | null): Promise<PagedResult<Friendship>> {
    const { data } = await http.get<PagedResult<Friendship>>('/api/friendships/requests/outgoing', {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },
}
