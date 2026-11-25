/** Gọi API kiểm tra tình trạng backend. */
import { http } from '../lib/http'

export interface HealthResponse {
  status: string
  timestamp: string
}

export const systemApi = {
  async health(): Promise<HealthResponse> {
    const { data } = await http.get<HealthResponse>('/health')
    return data
  },
}
