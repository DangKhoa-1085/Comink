/**
 * Trạng thái phiên phía client (Zustand chỉ dùng cho trạng thái UI/phiên — AGENTS.md mục 8.3).
 * Dữ liệu server (hồ sơ, danh sách tài khoản) do TanStack Query quản lý.
 */
import { create } from 'zustand'
import { authApi } from '../api/auth'
import { usersApi } from '../api/users'
import type { AuthUser, LoginPayload, RegisterPayload } from '../api/types'
import { ApiError } from '../lib/http'
import { session } from '../lib/session'

interface AuthState {
  /** Người dùng hiện tại; null nghĩa là chưa đăng nhập. */
  user: AuthUser | null
  /** Đã thử khôi phục phiên từ refresh token chưa (để tránh nhấp nháy khi tải trang). */
  initialized: boolean
  /** Đang thực hiện một thao tác xác thực. */
  loading: boolean
  /** Mã lỗi của lần thao tác gần nhất (client tự dịch — AGENTS.md mục 3). */
  errorCode: string | null
  /** Chi tiết lỗi validate theo từng trường. */
  fieldErrors: Record<string, string[]>

  login: (payload: LoginPayload) => Promise<boolean>
  register: (payload: RegisterPayload) => Promise<boolean>
  logout: () => Promise<void>
  /** Khôi phục phiên khi tải lại trang bằng refresh token đã lưu. */
  restoreSession: () => Promise<void>
  /** Cập nhật thông tin người dùng sau khi sửa hồ sơ. */
  setUser: (user: AuthUser) => void
  clearError: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  user: session.getUser(),
  initialized: false,
  loading: false,
  errorCode: null,
  fieldErrors: {},

  async login(payload) {
    set({ loading: true, errorCode: null, fieldErrors: {} })
    try {
      const auth = await authApi.login(payload)
      session.set(auth)
      set({ user: auth.user, loading: false })
      return true
    } catch (error) {
      const apiError = error instanceof ApiError ? error : null
      set({
        loading: false,
        errorCode: apiError?.code ?? 'INTERNAL_ERROR',
        fieldErrors: apiError?.fields ?? {},
      })
      return false
    }
  },

  async register(payload) {
    set({ loading: true, errorCode: null, fieldErrors: {} })
    try {
      const auth = await authApi.register(payload)
      session.set(auth)
      set({ user: auth.user, loading: false })
      return true
    } catch (error) {
      const apiError = error instanceof ApiError ? error : null
      set({
        loading: false,
        errorCode: apiError?.code ?? 'INTERNAL_ERROR',
        fieldErrors: apiError?.fields ?? {},
      })
      return false
    }
  },

  async logout() {
    const refreshToken = session.getRefreshToken()
    // Thu hồi refresh token ở backend; lỗi mạng không được chặn việc đăng xuất tại client.
    if (refreshToken) {
      try {
        await authApi.logout(refreshToken)
      } catch {
        // Bỏ qua: phiên phía client vẫn được xóa bên dưới.
      }
    }
    session.clear()
    set({ user: null, errorCode: null, fieldErrors: {} })
  },

  async restoreSession() {
    if (!session.hasStoredSession()) {
      set({ user: null, initialized: true })
      return
    }

    try {
      // Gọi một endpoint cần xác thực: interceptor sẽ tự dùng refresh token để lấy access token mới.
      const profile = await usersApi.getMe()
      const user: AuthUser = {
        id: profile.id,
        userName: profile.userName,
        email: profile.email,
        displayName: profile.displayName,
        roles: profile.roles,
      }
      session.setUser(user)
      set({ user, initialized: true })
    } catch {
      // Refresh token hết hiệu lực hoặc tài khoản bị khóa → coi như chưa đăng nhập.
      session.clear()
      set({ user: null, initialized: true })
    }
  },

  setUser(user) {
    session.setUser(user)
    set({ user })
  },

  clearError() {
    set({ errorCode: null, fieldErrors: {} })
  },
}))

/** Tiện ích đọc trạng thái ngoài component (dùng trong route guard). */
export function isAuthenticated(): boolean {
  return useAuthStore.getState().user !== null
}
