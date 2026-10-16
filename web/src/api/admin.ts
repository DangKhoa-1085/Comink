/** Gọi API nhóm quản trị — `/api/admin` (docs/05_api_contract.md mục 4). */
import { http } from '../lib/http'
import type { AdminUserSummary, PagedResult } from './types'

export const adminApi = {
  async listUsers(params: { cursor?: string; limit?: number; search?: string }): Promise<PagedResult<AdminUserSummary>> {
    const { data } = await http.get<PagedResult<AdminUserSummary>>('/api/admin/users', { params })
    return data
  },
}
