import { Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AppHeader } from './AppHeader'
import { ToastContainer } from '../ui/ToastContainer'
import { useAuthStore } from '../../stores/authStore'
import { useChatConnection } from '../../hooks/useChatConnection'
import { IncomingCallModal } from '../calls/IncomingCallModal'

/** Khung chung của mọi trang: thanh điều hướng + nội dung + vùng thông báo. */
export function AppLayout() {
  const user = useAuthStore((state) => state.user)
  const { t } = useTranslation()

  // Duy trì một kết nối SignalR khi đã đăng nhập: nhận tin nhắn mới ở mọi trang
  // (kể cả khi đang ở danh sách hội thoại) và tự đồng bộ lại sau khi mất kết nối.
  const { isConnected, wasConnected } = useChatConnection(Boolean(user))

  // Chỉ cảnh báo khi ĐÃ từng kết nối rồi mới mất (tránh nhấp nháy lúc mới mở trang).
  const showOfflineBanner = Boolean(user) && wasConnected && !isConnected

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 transition-colors dark:bg-slate-950 dark:text-slate-100">
      <AppHeader />
      {showOfflineBanner && (
        <div
          role="status"
          className="border-b border-amber-300 bg-amber-100 px-4 py-2 text-center text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200"
        >
          {t('chat.connection_lost')}
        </div>
      )}
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
      {/* Modal cuộc gọi đến — hiện ở mọi trang khi nhận sự kiện CallIncoming. */}
      <IncomingCallModal />
      <footer className="border-t border-slate-200 py-6 text-center text-xs text-slate-500 dark:border-slate-800">
        <p>Comink &bull; Đồ án chuyên ngành công nghệ phần mềm &bull; 2026</p>
      </footer>
      <ToastContainer />
    </div>
  )
}
