/** Gọi API nhóm hồ sơ — `/api/users` (docs/05_api_contract.md mục 3). */
import { http } from '../lib/http'
import type { MyProfile, UpdateProfilePayload, UpdateSettingsPayload } from './types'

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
}
