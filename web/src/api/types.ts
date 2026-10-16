/**
 * Kiểu dữ liệu của API, khớp `docs/05_api_contract.md`.
 * Không dùng `any` — mọi phản hồi đều có kiểu tường minh.
 */

/** Phản hồi ProblemDetails có thêm mã lỗi (RFC 7807 + `code`). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** Người dùng trả kèm kết quả xác thực. */
export interface AuthUser {
  id: string
  userName: string
  email: string
  displayName: string
  roles: string[]
}

/** Kết quả đăng ký / đăng nhập / làm mới token. */
export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: AuthUser
}

/** Hồ sơ đầy đủ của chính người dùng đang đăng nhập. */
export interface MyProfile {
  id: string
  userName: string
  email: string
  displayName: string
  bio: string | null
  avatarUrl: string | null
  coverUrl: string | null
  location: string | null
  dateOfBirth: string | null
  followerCount: number
  followingCount: number
  friendCount: number
  language: 'vi' | 'en'
  roles: string[]
  createdAt: string
}

/** Một dòng trong danh sách tài khoản của trang quản trị. */
export interface AdminUserSummary {
  id: string
  userName: string
  email: string
  displayName: string
  accountStatus: string
  createdAt: string
}

/** Kết quả phân trang theo con trỏ. */
export interface PagedResult<T> {
  items: T[]
  nextCursor: string | null
  hasMore: boolean
}

// --- Yêu cầu gửi lên ---

export interface RegisterPayload {
  userName: string
  email: string
  password: string
  displayName: string
}

export interface LoginPayload {
  emailOrUserName: string
  password: string
  deviceName?: string
}

export interface UpdateProfilePayload {
  displayName?: string
  bio?: string
  location?: string
  dateOfBirth?: string
}

export interface UpdateSettingsPayload {
  language?: 'vi' | 'en'
}
