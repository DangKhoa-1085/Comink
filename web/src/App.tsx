import { useEffect } from 'react'
import { RouterProvider } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { router } from './router/router'
import { useAuthStore } from './stores/authStore'
import { setSessionExpiredHandler } from './lib/http'
import { FullPageSpinner } from './components/ui/Feedback'
import { ToastContainer } from './components/ui/ToastContainer'

/**
 * Gốc ứng dụng: khôi phục phiên một lần khi tải trang, sau đó giao lại cho router.
 */
export function App() {
  const { t } = useTranslation()
  const initialized = useAuthStore((state) => state.initialized)
  const restoreSession = useAuthStore((state) => state.restoreSession)
  const logout = useAuthStore((state) => state.logout)

  useEffect(() => {
    // Khi cả refresh token cũng hết hiệu lực, interceptor gọi handler này để xóa phiên.
    setSessionExpiredHandler(() => {
      void logout()
    })

    void restoreSession()
  }, [restoreSession, logout])

  // Chờ biết chắc đã đăng nhập hay chưa để tránh nhấp nháy màn hình.
  if (!initialized) {
    return (
      <>
        <FullPageSpinner label={t('common.loading')} />
        <ToastContainer />
      </>
    )
  }

  return (
    <>
      <RouterProvider router={router} />
      <ToastContainer />
    </>
  )
}

export default App
