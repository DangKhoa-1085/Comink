/** Gọi API nhóm quản trị — `/api/admin` (docs/05_api_contract.md mục 4). */
import { http } from '../lib/http'
import type { AdminUserSummary, PagedResult } from './types'

export const adminApi = {
  async listUsers(params: { cursor?: string; limit?: number; search?: string }): Promise<PagedResult<AdminUserSummary>> {
    const { data } = await http.get<PagedResult<AdminUserSummary>>('/api/admin/users', { params })
    return data
  },

  /** Khóa tài khoản; `reason` tùy chọn (≤ 500 ký tự). Trả về dòng tài khoản sau khi khóa. */
  async lockUser(id: string, reason?: string): Promise<AdminUserSummary> {
    const { data } = await http.post<AdminUserSummary>(`/api/admin/users/${id}/lock`, {
      reason: reason?.trim() ? reason.trim() : null,
    })
    return data
  },

  /** Mở khóa tài khoản (idempotent). */
  async unlockUser(id: string): Promise<AdminUserSummary> {
    const { data } = await http.post<AdminUserSummary>(`/api/admin/users/${id}/unlock`)
    return data
  },
}
