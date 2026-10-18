/** Gọi API nhóm hồ sơ và tìm người dùng — `/api/users` (docs/05_api_contract.md mục 3 và 7.1). */
import { http } from '../lib/http'
import type {
  MyProfile,
  PagedResult,
  PublicProfile,
  UpdateProfilePayload,
  UpdateSettingsPayload,
  UserSummary,
} from './types'

export const usersApi = {
  async getMe(): Promise<MyProfile> {
    const { data } = await http.get<MyProfile>('/api/users/me')
    return data
  },

  async updateProfile(payload: UpdateProfilePayload): Promise<MyProfile> {
    const { data } = await http.patch<MyProfile>('/api/users/me/profile', payload)
    return data
  },

  async updateSettings(payload: UpdateSettingsPayload): Promise<MyProfile> {
    const { data } = await http.patch<MyProfile>('/api/users/me/settings', payload)
    return data
  },

  /** Tìm người dùng — gõ không dấu vẫn khớp. */
  async search(search: string, cursor?: string | null): Promise<PagedResult<UserSummary>> {
    const { data } = await http.get<PagedResult<UserSummary>>('/api/users', {
      params: cursor ? { search, cursor } : { search },
    })
    return data
  },

  /** Hồ sơ công khai của một người dùng. */
  async publicProfile(userId: string): Promise<PublicProfile> {
    const { data } = await http.get<PublicProfile>(`/api/users/${userId}`)
    return data
  },
}
