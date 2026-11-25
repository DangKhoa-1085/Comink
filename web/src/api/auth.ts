/** Gọi API nhóm xác thực — `/api/auth` (docs/05_api_contract.md mục 2). */
import { http } from '../lib/http'
import type { AuthResponse, LoginPayload, RegisterPayload } from './types'

/** Tên thiết bị gửi kèm khi đăng nhập (để quản lý phiên sau này). */
function detectDeviceName(): string {
  const ua = navigator.userAgent
  const browser = ua.includes('Edg/')
    ? 'Edge'
    : ua.includes('Chrome/')
      ? 'Chrome'
      : ua.includes('Firefox/')
        ? 'Firefox'
        : ua.includes('Safari/')
          ? 'Safari'
          : 'Trình duyệt'
  return `${browser} trên web`
}

export const authApi = {
  async register(payload: RegisterPayload): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/api/auth/register', payload)
    return data
  },

  async login(payload: LoginPayload): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/api/auth/login', {
      ...payload,
      deviceName: payload.deviceName ?? detectDeviceName(),
    })
    return data
  },

  async logout(refreshToken: string): Promise<void> {
    await http.post('/api/auth/logout', { refreshToken })
  },
}
