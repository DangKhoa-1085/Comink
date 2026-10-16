import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '../components/layout/AppLayout'
import { ProtectedRoute, RequireRole } from '../components/layout/ProtectedRoute'
import { HomePage } from '../pages/HomePage'
import { LoginPage } from '../pages/LoginPage'
import { RegisterPage } from '../pages/RegisterPage'
import { ProfilePage } from '../pages/ProfilePage'
import { AdminUsersPage } from '../pages/AdminUsersPage'
import { NotFoundPage } from '../pages/NotFoundPage'

/**
 * Bảng định tuyến của web.
 * - `/login`, `/register`: công khai.
 * - `/profile`: yêu cầu đăng nhập.
 * - `/admin/users`: yêu cầu role `Admin` (backend vẫn kiểm tra lại — AGENTS.md mục 9.2).
 */
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      {
        element: <ProtectedRoute />,
        children: [{ path: 'profile', element: <ProfilePage /> }],
      },
      {
        element: <RequireRole role="Admin" />,
        children: [{ path: 'admin/users', element: <AdminUsersPage /> }],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
