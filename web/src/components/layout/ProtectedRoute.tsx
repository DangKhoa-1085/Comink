import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '../../stores/authStore'
import { FullPageSpinner } from '../ui/Feedback'

/**
 * Chặn truy cập khi chưa đăng nhập.
 * Đợi khôi phục phiên xong mới quyết định để tránh nhấp nháy về trang đăng nhập.
 */
export function ProtectedRoute() {
  const { t } = useTranslation()
  const user = useAuthStore((state) => state.user)
  const initialized = useAuthStore((state) => state.initialized)
  const location = useLocation()

  if (!initialized) {
    return <FullPageSpinner label={t('common.loading')} />
  }

  if (!user) {
    // Nhớ đường dẫn đang truy cập để quay lại sau khi đăng nhập.
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

/**
 * Chặn truy cập khi người dùng không có role yêu cầu (ví dụ khu vực `/admin`).
 * Backend vẫn kiểm tra lại quyền — đây chỉ là lớp bảo vệ phía giao diện (AGENTS.md mục 9.2).
 */
export function RequireRole({ role }: { role: string }) {
  const { t } = useTranslation()
  const initialized = useAuthStore((state) => state.initialized)
  const user = useAuthStore((state) => state.user)

  if (!initialized) {
    return <FullPageSpinner label={t('common.loading')} />
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (!user.roles.includes(role)) {
    // Không tiết lộ sự tồn tại của khu vực quản trị: đưa về trang chủ.
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
