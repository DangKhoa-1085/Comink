import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from '../components/layout/AppLayout'
import { ProtectedRoute, RequireRole } from '../components/layout/ProtectedRoute'
import { HomePage } from '../pages/HomePage'
import { LoginPage } from '../pages/LoginPage'
import { RegisterPage } from '../pages/RegisterPage'
import { ProfilePage } from '../pages/ProfilePage'
import { AdminUsersPage } from '../pages/AdminUsersPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { FeedPage } from '../pages/FeedPage'
import { SearchPage } from '../pages/SearchPage'
import { FriendsPage } from '../pages/FriendsPage'
import { UserProfilePage } from '../pages/UserProfilePage'
import { MessagesPage } from '../pages/MessagesPage'
import { ChatPage } from '../pages/ChatPage'

/**
 * Bảng định tuyến của web.
 * - `/login`, `/register`: công khai.
 * - `/` (bảng tin khi đã đăng nhập), `/feed`, `/profile`, `/search`, `/friends`, `/users/:id`:
 *   yêu cầu đăng nhập.
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
        children: [
          { path: 'profile', element: <ProfilePage /> },
          { path: 'search', element: <SearchPage /> },
          { path: 'friends', element: <FriendsPage /> },
          { path: 'users/:id', element: <UserProfilePage /> },
          { path: 'feed', element: <FeedPage /> },
          { path: 'messages', element: <MessagesPage /> },
          { path: 'messages/:conversationId', element: <ChatPage /> },
        ],
      },
      {
        element: <RequireRole role="Admin" />,
        children: [{ path: 'admin/users', element: <AdminUsersPage /> }],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
