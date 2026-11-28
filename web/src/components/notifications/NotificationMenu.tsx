import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, CheckCheck, MoreHorizontal, RefreshCw } from 'lucide-react'
import { notificationsApi, type NotificationItem } from '../../api/notifications'
import { cn } from '../../lib/utils'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Feedback'
import { NotificationItemRow } from './NotificationItemRow'

type FilterMode = 'all' | 'unread'

interface NotificationMenuProps {
  /** Vị trí đặt trong layout: thanh trên Mobile hoặc Sidebar Desktop. */
  placement: 'mobile-top' | 'sidebar'
  /** Thu gọn thành icon trên Desktop (khi ở màn hình Tin nhắn). */
  compact?: boolean
}

const RECENT_MS = 24 * 60 * 60 * 1000

/** Tính tọa độ cố định trên viewport để bảng thông báo không bị kẹt dưới stacking context khác. */
function computePanelStyle(
  trigger: HTMLElement | null,
  placement: NotificationMenuProps['placement'],
): CSSProperties | null {
  const rect = trigger?.getBoundingClientRect()
  if (!rect || typeof window === 'undefined') {
    return null
  }

  const padding = 12
  const width = Math.min(384, window.innerWidth - padding * 2)
  const maxHeight = Math.min(Math.floor(window.innerHeight * 0.8), 560)

  if (placement === 'sidebar') {
    const left = Math.max(padding, Math.min(rect.right + 8, window.innerWidth - width - padding))
    const top = Math.max(padding, Math.min(rect.top, window.innerHeight - maxHeight - padding))
    return { top, left, width, maxHeight }
  }

  if (window.innerWidth < 640) {
    return {
      top: Math.max(rect.bottom + 8, 64),
      left: padding,
      right: padding,
      maxHeight,
    }
  }

  const left = Math.max(padding, Math.min(rect.right - width, window.innerWidth - width - padding))
  return {
    top: rect.bottom + 8,
    left,
    width,
    maxHeight,
  }
}

/**
 * Nút chuông và bảng thả Thông báo kiểu Facebook (Tất cả / Chưa đọc, Mới / Trước đó, Xem tất cả).
 */
export function NotificationMenu({ placement, compact = false }: NotificationMenuProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [optionsOpen, setOptionsOpen] = useState(false)
  const [filter, setFilter] = useState<FilterMode>('all')
  const [nowMs, setNowMs] = useState(() => Date.now())
  const [panelStyle, setPanelStyle] = useState<CSSProperties | null>(null)
  const triggerRef = useRef<HTMLButtonElement | null>(null)
  const panelRef = useRef<HTMLDivElement | null>(null)

  const query = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationsApi.list(null, 30),
    staleTime: 20_000,
  })

  const items = query.data?.items ?? []
  const unreadCount = items.filter((item) => !item.isRead).length

  const markAllMutation = useMutation({
    mutationFn: async (unreadItems: NotificationItem[]) => {
      await Promise.all(unreadItems.map((item) => notificationsApi.markRead(item.id)))
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
      setOptionsOpen(false)
    },
  })

  useEffect(() => {
    if (!open) {
      return
    }

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      const target = event.target as Node
      if (!triggerRef.current?.contains(target) && !panelRef.current?.contains(target)) {
        setOpen(false)
        setOptionsOpen(false)
      }
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        setOptionsOpen(false)
      }
    }
    const updatePosition = () => {
      setPanelStyle(computePanelStyle(triggerRef.current, placement))
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('touchstart', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    window.addEventListener('resize', updatePosition)
    window.addEventListener('scroll', updatePosition, true)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('touchstart', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('resize', updatePosition)
      window.removeEventListener('scroll', updatePosition, true)
    }
  }, [open, placement])

  const toggleMenu = () => {
    setOpen((prev) => {
      const next = !prev
      if (!next) {
        setOptionsOpen(false)
      } else {
        setNowMs(Date.now())
        setPanelStyle(computePanelStyle(triggerRef.current, placement))
        void query.refetch()
      }
      return next
    })
  }

  const filtered = filter === 'unread' ? items.filter((item) => !item.isRead) : items
  const recentItems: NotificationItem[] = []
  const earlierItems: NotificationItem[] = []
  for (const item of filtered) {
    const isRecent = !item.isRead || nowMs - new Date(item.createdAt).getTime() < RECENT_MS
    if (isRecent) {
      recentItems.push(item)
    } else {
      earlierItems.push(item)
    }
  }

  return (
    <div className="relative">
      {placement === 'mobile-top' ? (
        <button
          ref={triggerRef}
          type="button"
          onClick={toggleMenu}
          title={t('notifications.title')}
          aria-label={t('notifications.title')}
          aria-expanded={open}
          className={cn(
            'relative flex h-9 w-9 shrink-0 items-center justify-center rounded-xl transition-colors',
            open
              ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
              : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
          )}
        >
          <Bell className="h-4 w-4" />
          {unreadCount > 0 && (
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[16px] items-center justify-center rounded-full bg-sky-600 px-1 text-[10px] font-bold text-white dark:bg-sky-500">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </button>
      ) : (
        <button
          ref={triggerRef}
          type="button"
          onClick={toggleMenu}
          title={t('notifications.title')}
          aria-label={t('notifications.title')}
          aria-expanded={open}
          className={cn(
            'relative flex w-full items-center rounded-xl py-2.5 text-sm font-semibold transition-colors',
            compact ? 'justify-center px-2' : 'justify-between px-3',
            open
              ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300'
              : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800/80',
          )}
        >
          <span className="flex items-center gap-3">
            <Bell className="h-4 w-4 shrink-0" />
            {!compact && <span>{t('notifications.title')}</span>}
          </span>
          {!compact && unreadCount > 0 && (
            <span className="rounded-full bg-sky-600 px-2 py-0.5 text-xs font-bold text-white dark:bg-sky-500">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
          {compact && unreadCount > 0 && (
            <span className="absolute right-2.5 top-2 h-2.5 w-2.5 rounded-full bg-sky-600 ring-2 ring-white dark:bg-sky-400 dark:ring-[#121826]" />
          )}
        </button>
      )}

      {open &&
        createPortal(
          <div
            ref={panelRef}
            role="dialog"
            aria-label={t('notifications.title')}
            style={panelStyle ?? undefined}
            className="fixed z-[100] flex flex-col rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-[#121826]"
          >
          {/* Đầu bảng thông báo */}
          <div className="flex items-center justify-between px-4 pt-3.5 pb-2">
            <h2 className="text-xl font-extrabold text-slate-900 dark:text-slate-50">
              {t('notifications.title')}
            </h2>

            <div className="relative">
              <button
                type="button"
                onClick={() => setOptionsOpen((prev) => !prev)}
                title={t('notifications.more_options')}
                aria-label={t('notifications.more_options')}
                className="flex h-8 w-8 items-center justify-center rounded-full text-slate-500 transition-colors hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
              >
                <MoreHorizontal className="h-4 w-4" />
              </button>

              {optionsOpen && (
                <div className="absolute right-0 top-full z-10 mt-1 w-56 space-y-1 rounded-xl border border-slate-200 bg-white p-1.5 text-xs shadow-xl dark:border-slate-700 dark:bg-[#1A2234]">
                  <button
                    type="button"
                    disabled={unreadCount === 0 || markAllMutation.isPending}
                    onClick={() => markAllMutation.mutate(items.filter((i) => !i.isRead))}
                    className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-50 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    <CheckCheck className="h-4 w-4 text-sky-600 dark:text-sky-400" />
                    <span>{t('notifications.mark_all_read')}</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setOptionsOpen(false)
                      void query.refetch()
                    }}
                    className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 font-semibold text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    <RefreshCw className="h-4 w-4 text-slate-500 dark:text-slate-400" />
                    <span>{t('notifications.refresh')}</span>
                  </button>
                </div>
              )}
            </div>
          </div>

          {/* Hai nút lọc Tất cả / Chưa đọc */}
          <div className="flex items-center gap-2 px-4 pb-3">
            <button
              type="button"
              onClick={() => setFilter('all')}
              className={cn(
                'rounded-full px-3.5 py-1.5 text-xs font-bold transition-colors',
                filter === 'all'
                  ? 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
                  : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
              )}
            >
              {t('notifications.filter_all')}
            </button>
            <button
              type="button"
              onClick={() => setFilter('unread')}
              className={cn(
                'rounded-full px-3.5 py-1.5 text-xs font-bold transition-colors',
                filter === 'unread'
                  ? 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
                  : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
              )}
            >
              {t('notifications.filter_unread')}
            </button>
          </div>

          {/* Danh sách thông báo cuộn bên trong */}
          <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
            {query.isLoading ? (
              <div className="space-y-3 p-2">
                {[1, 2, 3].map((row) => (
                  <div key={row} className="flex items-center gap-3">
                    <Skeleton className="h-12 w-12 rounded-full" />
                    <div className="flex-1 space-y-2">
                      <Skeleton className="h-3.5 w-4/5" />
                      <Skeleton className="h-3 w-20" />
                    </div>
                  </div>
                ))}
              </div>
            ) : query.isError ? (
              <div className="space-y-2 p-4 text-center text-xs text-slate-500">
                <p>{t('notifications.error')}</p>
                <Button variant="outline" size="sm" onClick={() => void query.refetch()}>
                  {t('common.retry')}
                </Button>
              </div>
            ) : filtered.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-2 py-10 text-center text-sm text-slate-500 dark:text-slate-400">
                <Bell className="h-8 w-8 opacity-40" />
                <p>
                  {filter === 'unread' ? t('notifications.empty_unread') : t('notifications.empty')}
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {recentItems.length > 0 && (
                  <section className="space-y-1">
                    <div className="flex items-center justify-between px-2 pt-1">
                      <h3 className="text-sm font-bold text-slate-800 dark:text-slate-100">
                        {t('notifications.section_new')}
                      </h3>
                      <Link
                        to="/notifications"
                        onClick={() => setOpen(false)}
                        className="text-xs font-semibold text-sky-600 hover:underline dark:text-sky-400"
                      >
                        {t('notifications.view_all')}
                      </Link>
                    </div>
                    {recentItems.map((item) => (
                      <NotificationItemRow
                        key={item.id}
                        item={item}
                        onNavigate={() => setOpen(false)}
                      />
                    ))}
                  </section>
                )}

                {earlierItems.length > 0 && (
                  <section className="space-y-1">
                    <div className="flex items-center justify-between px-2 pt-1">
                      <h3 className="text-sm font-bold text-slate-800 dark:text-slate-100">
                        {t('notifications.section_earlier')}
                      </h3>
                      {recentItems.length === 0 && (
                        <Link
                          to="/notifications"
                          onClick={() => setOpen(false)}
                          className="text-xs font-semibold text-sky-600 hover:underline dark:text-sky-400"
                        >
                          {t('notifications.view_all')}
                        </Link>
                      )}
                    </div>
                    {earlierItems.map((item) => (
                      <NotificationItemRow
                        key={item.id}
                        item={item}
                        onNavigate={() => setOpen(false)}
                      />
                    ))}
                  </section>
                )}
              </div>
            )}
          </div>
        </div>,
          document.body,
        )}
    </div>
  )
}
