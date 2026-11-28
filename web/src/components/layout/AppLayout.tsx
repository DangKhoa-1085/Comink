import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useInfiniteQuery } from '@tanstack/react-query'
import { Home, MessageSquare, Search, ShieldCheck, User, Users } from 'lucide-react'
import { conversationsApi } from '../../api/conversations'
import { AppHeader } from './AppHeader'
import { ApkDownloadBanner } from './ApkDownloadBanner'
import { SettingsMenu } from './SettingsMenu'
import { NotificationMenu } from '../notifications/NotificationMenu'
import { CominkLogo } from '../brand/CominkLogo'
import { Avatar } from '../social/Avatar'
import { ToastContainer } from '../ui/ToastContainer'
import { useAuthStore } from '../../stores/authStore'
import type { AuthUser } from '../../api/types'
import { useChatConnection } from '../../hooks/useChatConnection'
import { useIncomingRequestIds } from '../../hooks/useIncomingRequestIds'
import { IncomingCallModal } from '../calls/IncomingCallModal'
import { cn } from '../../lib/utils'

/** Khung chung của mọi trang: tự thích ứng giữa khách, ứng dụng đã đăng nhập và phòng gọi. */
export function AppLayout() {
  const user = useAuthStore((state) => state.user)
  const { t } = useTranslation()
  const location = useLocation()

  // Duy trì một kết nối SignalR khi đã đăng nhập: nhận tin nhắn mới ở mọi trang
  // (kể cả khi đang ở danh sách hội thoại) và tự đồng bộ lại sau khi mất kết nối.
  const { isConnected, wasConnected } = useChatConnection(Boolean(user))

  // Chỉ cảnh báo khi ĐÃ từng kết nối rồi mới mất (tránh nhấp nháy lúc mới mở trang).
  const showOfflineBanner = Boolean(user) && wasConnected && !isConnected

  const isCallScreen = location.pathname.startsWith('/calls/')

  if (!user) {
    return (
      <div className="min-h-screen bg-[#F7F8FC] text-slate-900 transition-colors dark:bg-[#0B0F1A] dark:text-slate-100">
        <AppHeader />
        <main className="mx-auto max-w-6xl px-4 py-8">
          <Outlet />
        </main>
        <footer className="border-t border-slate-200/80 py-6 text-center text-xs text-slate-500 dark:border-slate-800">
          <p>Comink &bull; Đồ án chuyên ngành công nghệ phần mềm &bull; 2026</p>
        </footer>
        <ToastContainer />
      </div>
    )
  }

  if (isCallScreen) {
    return (
      <div className="flex h-dvh flex-col overflow-hidden bg-[#080B14] text-slate-100">
        {showOfflineBanner && (
          <div
            role="status"
            className="shrink-0 border-b border-amber-800 bg-amber-950/90 px-4 py-2 text-center text-xs text-amber-200"
          >
            {t('chat.connection_lost')}
          </div>
        )}
        <div className="min-h-0 flex-1 overflow-hidden">
          <Outlet />
        </div>
        <IncomingCallModal />
        <ToastContainer />
      </div>
    )
  }

  return <AuthenticatedShell user={user} showOfflineBanner={showOfflineBanner} />
}

/**
 * Khung ứng dụng chính khi đã đăng nhập:
 * - Desktop (`lg:`): Sidebar trái (`244px`, tự thu thành Icon Rail `76px` ở `/messages*`).
 * - Mobile (`< lg`): Sticky Top Icon Bar gọn gồm logo `C` về Bảng tin + dãy icon chức năng + nút Cài đặt.
 */
function AuthenticatedShell({
  user,
  showOfflineBanner,
}: {
  user: AuthUser
  showOfflineBanner: boolean
}) {
  const { t } = useTranslation()
  const location = useLocation()
  const isAdmin = user.roles.includes('Admin')
  const isMessagesRoute = location.pathname.startsWith('/messages')
  const isFeedActive = location.pathname === '/' || location.pathname === '/feed'

  // Dùng chung cache `['conversations']` với MessagesPage để badge tin chưa đọc cập nhật real-time.
  const conversationsBadgeQuery = useInfiniteQuery({
    queryKey: ['conversations'],
    queryFn: ({ pageParam }) => conversationsApi.list(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => (last.hasMore && last.nextCursor ? last.nextCursor : undefined),
    staleTime: 30_000,
  })
  const incomingRequests = useIncomingRequestIds()

  const unreadTotal = (conversationsBadgeQuery.data?.pages ?? [])
    .flatMap((page) => page.items)
    .reduce((sum, item) => sum + item.unreadCount, 0)
  const pendingFriendsCount = incomingRequests.size

  const navItems = [
    { to: '/', label: t('social.feed'), icon: Home, end: true, badge: 0 },
    {
      to: '/messages',
      label: t('chat.title'),
      icon: MessageSquare,
      end: false,
      badge: unreadTotal,
    },
    {
      to: '/friends',
      label: t('social.friends'),
      icon: Users,
      end: false,
      badge: pendingFriendsCount,
    },
    { to: '/search', label: t('social.search'), icon: Search, end: false, badge: 0 },
    { to: '/profile', label: t('nav.profile'), icon: User, end: false, badge: 0 },
    ...(isAdmin
      ? [{ to: '/admin/users', label: t('nav.admin'), icon: ShieldCheck, end: false, badge: 0 }]
      : []),
  ]

  // Trên thanh Mobile, logo C bên trái đảm nhiệm về Bảng tin nên dãy icon giữa hiển thị các chức năng còn lại.
  const mobileActionItems = navItems.filter((item) => item.to !== '/')

  return (
    <div
      className={cn(
        'flex flex-col bg-[#F7F8FC] text-slate-900 transition-colors dark:bg-[#0B0F1A] dark:text-slate-100 lg:flex-row',
        isMessagesRoute ? 'h-dvh overflow-hidden' : 'min-h-screen',
      )}
    >
      {/* Mobile Sticky Top Icon Bar (< lg) */}
      <header className="sticky top-0 z-40 flex h-14 shrink-0 items-center justify-between gap-1 border-b border-slate-200/80 bg-white/85 px-2.5 backdrop-blur-md dark:border-slate-800 dark:bg-[#121826]/85 lg:hidden">
        <Link
          to="/"
          title={t('nav.back_to_feed')}
          aria-label={t('nav.back_to_feed')}
          className={cn(
            'shrink-0 rounded-xl transition-all',
            isFeedActive
              ? 'ring-2 ring-sky-500 ring-offset-2 ring-offset-white dark:ring-offset-[#121826]'
              : 'opacity-90 hover:opacity-100',
          )}
        >
          <CominkLogo size="md" />
        </Link>

        <nav className="flex items-center gap-1" aria-label={t('app.title')}>
          {mobileActionItems.map((item) => {
            const Icon = item.icon
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                title={item.label}
                aria-label={item.label}
                className={({ isActive }) =>
                  cn(
                    'relative flex h-9 w-9 shrink-0 items-center justify-center rounded-xl transition-colors',
                    isActive
                      ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
                  )
                }
              >
                <Icon className="h-4 w-4" />
                {item.badge > 0 && (
                  <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[16px] items-center justify-center rounded-full bg-sky-600 px-1 text-[10px] font-bold text-white dark:bg-sky-500">
                    {item.badge > 99 ? '99+' : item.badge}
                  </span>
                )}
              </NavLink>
            )
          })}
          <NotificationMenu placement="mobile-top" />
        </nav>

        <div className="flex shrink-0 items-center border-l border-slate-200 pl-1.5 dark:border-slate-800">
          <SettingsMenu placement="mobile-top" />
        </div>
      </header>

      {/* Desktop Left Sidebar (>= lg) */}
      <aside
        className={cn(
          'hidden shrink-0 flex-col justify-between border-r border-slate-200/80 bg-white p-3 transition-all dark:border-slate-800 dark:bg-[#121826] lg:sticky lg:top-0 lg:z-30 lg:flex lg:h-screen',
          isMessagesRoute ? 'w-[76px]' : 'w-[244px]',
        )}
      >
        <div className="space-y-4">
          <div className={cn('flex items-center py-1', isMessagesRoute ? 'justify-center' : 'px-2')}>
            <Link to="/" title={t('nav.back_to_feed')} aria-label={t('nav.back_to_feed')}>
              <CominkLogo size="md" showWordmark={!isMessagesRoute} />
            </Link>
          </div>

          <nav className="space-y-1">
            {navItems.map((item) => {
              const Icon = item.icon
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  title={item.label}
                  className={({ isActive }) =>
                    cn(
                      'relative flex items-center rounded-xl py-2.5 text-sm font-semibold transition-colors',
                      isMessagesRoute ? 'justify-center px-2' : 'justify-between px-3',
                      isActive
                        ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300'
                        : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800/80',
                    )
                  }
                >
                  <span className="flex items-center gap-3">
                    <Icon className="h-4 w-4 shrink-0" />
                    {!isMessagesRoute && <span>{item.label}</span>}
                  </span>
                  {!isMessagesRoute && item.badge > 0 && (
                    <span className="rounded-full bg-sky-600 px-2 py-0.5 text-xs font-bold text-white dark:bg-sky-500">
                      {item.badge > 99 ? '99+' : item.badge}
                    </span>
                  )}
                  {isMessagesRoute && item.badge > 0 && (
                    <span className="absolute right-2.5 top-2 h-2.5 w-2.5 rounded-full bg-sky-600 ring-2 ring-white dark:bg-sky-400 dark:ring-[#121826]" />
                  )}
                </NavLink>
              )
            })}
            <NotificationMenu placement="sidebar" compact={isMessagesRoute} />
          </nav>
        </div>

        <div className="space-y-3">
          <ApkDownloadBanner compact={isMessagesRoute} />

          <div className="space-y-2 border-t border-slate-200/80 pt-3 dark:border-slate-800">
            <SettingsMenu placement="sidebar-bottom" compact={isMessagesRoute} />

            <Link
              to="/profile"
              title={user.displayName}
              className={cn(
                'flex items-center rounded-xl transition-colors hover:bg-slate-100 dark:hover:bg-slate-800/80',
                isMessagesRoute ? 'justify-center p-1.5' : 'gap-2.5 p-2',
              )}
            >
              <Avatar name={user.displayName} url={user.avatarUrl} size="sm" />
              {!isMessagesRoute && (
                <div className="min-w-0 flex-1">
                  <p className="truncate text-xs font-bold text-slate-800 dark:text-slate-100">
                    {user.displayName}
                  </p>
                  <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                    @{user.userName}
                    {isAdmin ? ' · Admin' : ''}
                  </p>
                </div>
              )}
            </Link>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div
        className={cn(
          'flex min-w-0 flex-1 flex-col',
          isMessagesRoute && 'min-h-0 overflow-hidden',
        )}
      >
        {showOfflineBanner && (
          <div
            role="status"
            className="shrink-0 border-b border-amber-300 bg-amber-100 px-4 py-2 text-center text-xs font-medium text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200"
          >
            {t('chat.connection_lost')}
          </div>
        )}

        <main
          className={cn(
            'min-w-0 flex-1',
            isMessagesRoute
              ? 'min-h-0 overflow-hidden'
              : 'mx-auto w-full max-w-6xl px-4 py-6 lg:px-8',
          )}
        >
          <Outlet />
        </main>
      </div>

      <IncomingCallModal />
      <ToastContainer />
    </div>
  )
}
