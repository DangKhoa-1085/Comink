import { Outlet } from 'react-router-dom'
import { AppHeader } from './AppHeader'
import { ToastContainer } from '../ui/ToastContainer'

/** Khung chung của mọi trang: thanh điều hướng + nội dung + vùng thông báo. */
export function AppLayout() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 transition-colors dark:bg-slate-950 dark:text-slate-100">
      <AppHeader />
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-slate-200 py-6 text-center text-xs text-slate-500 dark:border-slate-800">
        <p>Comink &bull; Đồ án chuyên ngành công nghệ phần mềm &bull; 2026</p>
      </footer>
      <ToastContainer />
    </div>
  )
}
