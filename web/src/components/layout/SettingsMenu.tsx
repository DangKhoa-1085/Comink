import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Download, Globe, LogOut, Moon, Settings, Smartphone, Sun, UserCog, X } from 'lucide-react'
import { APK_DOWNLOAD_URL } from '../../config/env'
import { useAuthStore } from '../../stores/authStore'
import { useTheme } from '../../hooks/useTheme'
import { useLanguage, type SupportedLanguage } from '../../hooks/useLanguage'
import { toast } from '../../stores/toastStore'
import { cn } from '../../lib/utils'

interface SettingsMenuProps {
  /** Hướng mở menu: trên Mobile mở xuống phải; trên Sidebar Desktop mở lên trên trái. */
  placement: 'mobile-top' | 'sidebar-bottom'
  /** Thu gọn thành nút icon (khi sidebar thu gọn ở màn hình Tin nhắn). */
  compact?: boolean
}

/**
 * Nút Cài đặt tập trung: đổi giao diện Sáng/Tối, đổi ngôn ngữ VI/EN,
 * lối tắt cài đặt tài khoản và Đăng xuất.
 */
export function SettingsMenu({ placement, compact = false }: SettingsMenuProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isDark, toggleTheme } = useTheme()
  const { current, changeLanguage } = useLanguage()
  const logout = useAuthStore((state) => state.logout)
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('touchstart', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('touchstart', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const handleLogout = async () => {
    setOpen(false)
    await logout()
    toast.success(t('auth.logout_success'))
    navigate('/login')
  }

  const popoverPosition =
    placement === 'mobile-top' ? 'right-0 top-full mt-2' : 'left-0 bottom-full mb-2'

  return (
    <div ref={rootRef} className="relative">
      {placement === 'mobile-top' || compact ? (
        <button
          type="button"
          onClick={() => setOpen((prev) => !prev)}
          title={t('nav.settings')}
          aria-label={t('nav.settings')}
          aria-expanded={open}
          className={cn(
            'flex h-9 w-9 items-center justify-center rounded-xl transition-colors',
            open
              ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200/80 dark:bg-[#1A2234] dark:text-slate-300 dark:hover:bg-slate-800',
          )}
        >
          <Settings className="h-4 w-4" />
        </button>
      ) : (
        <button
          type="button"
          onClick={() => setOpen((prev) => !prev)}
          title={t('nav.settings')}
          aria-expanded={open}
          className={cn(
            'flex w-full items-center justify-between rounded-xl border px-3 py-2 text-xs font-semibold transition-colors',
            open
              ? 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-500/30 dark:bg-sky-500/15 dark:text-sky-300'
              : 'border-slate-200 bg-slate-50 text-slate-600 hover:bg-slate-100 dark:border-slate-800 dark:bg-[#1A2234] dark:text-slate-300 dark:hover:bg-slate-800',
          )}
        >
          <span className="flex items-center gap-2">
            <Settings className="h-4 w-4" />
            <span>{t('nav.settings')}</span>
          </span>
          <span className="text-[11px] uppercase opacity-75">
            {current} · {isDark ? t('nav.theme_dark') : t('nav.theme_light')}
          </span>
        </button>
      )}

      {open && (
        <div
          role="menu"
          className={cn(
            'absolute z-50 w-64 space-y-1.5 rounded-2xl border border-slate-200 bg-white p-2 text-xs shadow-2xl',
            'dark:border-slate-800 dark:bg-[#121826]',
            popoverPosition,
          )}
        >
          <div className="flex items-center justify-between border-b border-slate-100 px-2.5 py-1.5 dark:border-slate-800">
            <span className="text-sm font-bold text-slate-900 dark:text-slate-100">
              {t('nav.settings')}
            </span>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
              aria-label={t('common.cancel')}
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>

          <button
            type="button"
            onClick={toggleTheme}
            className="flex w-full items-center justify-between rounded-xl px-2.5 py-2 font-semibold text-slate-700 transition-colors hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            <span className="flex items-center gap-2">
              {isDark ? (
                <Sun className="h-4 w-4 text-amber-400" />
              ) : (
                <Moon className="h-4 w-4 text-sky-600" />
              )}
              <span>{t('nav.theme')}</span>
            </span>
            <span className="rounded-lg bg-slate-100 px-2 py-0.5 font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
              {isDark ? t('nav.theme_dark') : t('nav.theme_light')}
            </span>
          </button>

          <div className="flex items-center justify-between rounded-xl px-2.5 py-1.5 font-semibold text-slate-700 dark:text-slate-200">
            <span className="flex items-center gap-2">
              <Globe className="h-4 w-4 text-sky-600 dark:text-sky-400" />
              <span>{t('nav.language')}</span>
            </span>
            <div className="inline-flex rounded-lg border border-slate-200 bg-slate-100 p-0.5 dark:border-slate-700 dark:bg-slate-900">
              {(['vi', 'en'] as SupportedLanguage[]).map((lang) => (
                <button
                  key={lang}
                  type="button"
                  onClick={() => changeLanguage(lang)}
                  className={cn(
                    'rounded-md px-2 py-0.5 text-xs font-bold uppercase transition-colors',
                    current === lang
                      ? 'bg-sky-600 text-white dark:bg-sky-500'
                      : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200',
                  )}
                >
                  {lang}
                </button>
              ))}
            </div>
          </div>

          <button
            type="button"
            onClick={() => {
              setOpen(false)
              navigate('/profile')
            }}
            className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 font-semibold text-slate-700 transition-colors hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            <UserCog className="h-4 w-4 text-slate-500 dark:text-slate-400" />
            <span>{t('nav.account_settings')}</span>
          </button>

          {placement === 'mobile-top' &&
            (APK_DOWNLOAD_URL ? (
              <a
                href={APK_DOWNLOAD_URL}
                target="_blank"
                rel="noopener noreferrer"
                download
                onClick={() => setOpen(false)}
                className="flex w-full items-center justify-between rounded-xl px-2.5 py-2 font-semibold text-sky-700 transition-colors hover:bg-sky-50 dark:text-sky-300 dark:hover:bg-sky-500/15"
              >
                <span className="flex items-center gap-2">
                  <Smartphone className="h-4 w-4 shrink-0" />
                  <span>{t('apk.mobile_menu_item')}</span>
                </span>
                <Download className="h-3.5 w-3.5 shrink-0" />
              </a>
            ) : (
              <button
                type="button"
                onClick={() => {
                  setOpen(false)
                  toast.info(t('apk.not_configured'))
                }}
                className="flex w-full items-center justify-between rounded-xl px-2.5 py-2 font-semibold text-sky-700 transition-colors hover:bg-sky-50 dark:text-sky-300 dark:hover:bg-sky-500/15"
              >
                <span className="flex items-center gap-2">
                  <Smartphone className="h-4 w-4 shrink-0" />
                  <span>{t('apk.mobile_menu_item')}</span>
                </span>
                <Download className="h-3.5 w-3.5 shrink-0" />
              </button>
            ))}

          <div className="border-t border-slate-100 pt-1 dark:border-slate-800">
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 font-bold text-rose-600 transition-colors hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950/40"
            >
              <LogOut className="h-4 w-4" />
              <span>{t('nav.logout')}</span>
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
