/** Gọi API theo dõi — `/api/follows` (docs/05_api_contract.md mục 7.3). */
import { http } from '../lib/http'
import type { PagedResult, UserSummary } from './types'

export const followsApi = {
  async follow(userId: string): Promise<void> {
    await http.post(`/api/follows/${userId}`)
  },

  async unfollow(userId: string): Promise<void> {
    await http.delete(`/api/follows/${userId}`)
  },

  async followers(userId: string, cursor?: string | null): Promise<PagedResult<UserSummary>> {
    const { data } = await http.get<PagedResult<UserSummary>>(`/api/users/${userId}/followers`, {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async following(userId: string, cursor?: string | null): Promise<PagedResult<UserSummary>> {
    const { data } = await http.get<PagedResult<UserSummary>>(`/api/users/${userId}/following`, {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },
}
