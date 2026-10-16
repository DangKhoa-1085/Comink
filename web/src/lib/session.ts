/**
 * Lưu phiên đăng nhập.
 *
 * Quyết định thiết kế:
 * - Refresh token nằm trong `localStorage` để còn phiên khi tải lại trang.
 *   Web không có nơi lưu an toàn như `flutter_secure_storage` của app; đây là
 *   đánh đổi đã biết (rủi ro XSS). Vì vậy nội dung người dùng luôn hiển thị
 *   dưới dạng văn bản thuần, không dùng `dangerouslySetInnerHTML` (AGENTS.md mục 9.4).
 * - Access token chỉ giữ trong bộ nhớ, không ghi xuống localStorage.
 * - refresh_token chỉ lưu băm ở backend; đây là bản thô phía client.
 */
import type { AuthResponse, AuthUser } from '../api/types'

const REFRESH_TOKEN_KEY = 'comink.refreshToken'
const USER_KEY = 'comink.user'

let accessToken: string | null = null
let currentUser: AuthUser | null = readUser()

function readUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as AuthUser) : null
  } catch {
    // Dữ liệu hỏng trong localStorage không được làm sập ứng dụng.
    return null
  }
}

export const session = {
  /** Access token hiện tại (chỉ trong bộ nhớ). */
  getAccessToken(): string | null {
    return accessToken
  },

  /** Refresh token đã lưu (nếu có). */
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY)
  },

  /** Người dùng hiện tại (đọc từ localStorage khi tải lại trang). */
  getUser(): AuthUser | null {
    return currentUser
  },

  /** Có refresh token để thử khôi phục phiên hay không. */
  hasStoredSession(): boolean {
    return Boolean(localStorage.getItem(REFRESH_TOKEN_KEY))
  },

  /** Lưu kết quả đăng nhập/đăng ký/làm mới token. */
  set(auth: AuthResponse): void {
    accessToken = auth.accessToken
    currentUser = auth.user
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
    localStorage.setItem(USER_KEY, JSON.stringify(auth.user))
  },

  /** Cập nhật riêng thông tin người dùng (ví dụ sau khi sửa hồ sơ). */
  setUser(user: AuthUser): void {
    currentUser = user
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  /** Xóa phiên (đăng xuất hoặc token hết hiệu lực). */
  clear(): void {
    accessToken = null
    currentUser = null
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
