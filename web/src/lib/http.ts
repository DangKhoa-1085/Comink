/**
 * Client HTTP dùng chung.
 *
 * - Tự gắn access token vào mọi request cần xác thực.
 * - Khi gặp 401, tự gọi `/api/auth/refresh` **một lần** rồi thử lại request cũ.
 *   Nhiều request cùng lúc gặp 401 sẽ dùng chung một lần làm mới (tránh xoay vòng
 *   refresh token nhiều lần, vì token chỉ dùng được một lần — xem docs/05_api_contract.md mục 2).
 * - Làm mới thất bại thì xóa phiên và chuyển về trang đăng nhập.
 */
import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { API_BASE_URL } from '../config/env'
import { session } from './session'
import type { AuthResponse, ProblemDetails } from '../api/types'

/** Lỗi API đã được chuẩn hóa: có mã lỗi và chi tiết theo trường. */
export class ApiError extends Error {
  readonly code: string
  readonly status: number
  readonly fields: Record<string, string[]>

  constructor(code: string, status: number, fields: Record<string, string[]>) {
    super(code)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.fields = fields
  }
}

export const http: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = session.getAccessToken()
  if (token && !config.headers.Authorization) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/** Lần làm mới token đang chạy (nếu có) để nhiều request dùng chung. */
let refreshPromise: Promise<string> | null = null

/** Được gọi khi phiên không thể khôi phục (để điều hướng về trang đăng nhập). */
let onSessionExpired: (() => void) | null = null

export function setSessionExpiredHandler(handler: () => void): void {
  onSessionExpired = handler
}

/** Chuyển lỗi axios thành ApiError với mã lỗi của backend. */
function toApiError(error: AxiosError<ProblemDetails>): ApiError {
  const status = error.response?.status ?? 0
  const data = error.response?.data
  const code = data?.code ?? (status === 0 ? 'NETWORK_ERROR' : 'INTERNAL_ERROR')
  return new ApiError(code, status, data?.errors ?? {})
}

/** Gọi làm mới token; ném ApiError nếu thất bại. */
async function refreshAccessToken(): Promise<string> {
  const refreshToken = session.getRefreshToken()
  if (!refreshToken) {
    throw new ApiError('AUTH_REFRESH_TOKEN_INVALID', 401, {})
  }

  try {
    // Dùng axios trực tiếp (không qua interceptor) để tránh vòng lặp vô hạn.
    const { data } = await axios.post<AuthResponse>(
      `${API_BASE_URL}/api/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } },
    )
    session.set(data)
    return data.accessToken
  } catch (error) {
    session.clear()
    throw error instanceof AxiosError ? toApiError(error) : error
  }
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as InternalAxiosRequestConfig & { _retried?: boolean }
    const status = error.response?.status

    // Chỉ thử làm mới một lần cho mỗi request, bỏ qua các endpoint xác thực.
    const isAuthEndpoint = Boolean(original?.url?.includes('/api/auth/'))
    if (status === 401 && original && !original._retried && !isAuthEndpoint && session.getRefreshToken()) {
      original._retried = true
      try {
        refreshPromise = refreshPromise ?? refreshAccessToken()
        const newToken = await refreshPromise
        refreshPromise = null
        original.headers.Authorization = `Bearer ${newToken}`
        return await http.request(original)
      } catch (refreshError) {
        refreshPromise = null
        onSessionExpired?.()
        throw refreshError
      }
    }

    throw toApiError(error)
  },
)
