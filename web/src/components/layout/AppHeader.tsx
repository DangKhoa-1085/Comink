import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Languages, LogOut, Moon, ShieldCheck, Sun, User } from 'lucide-react'
import { useAuthStore } from '../../stores/authStore'
import { useLanguage } from '../../hooks/useLanguage'
import { useTheme } from '../../hooks/useTheme'
import { toast } from '../../stores/toastStore'
import { Button } from '../ui/Button'
import { cn } from '../../lib/utils'

/** Thanh điều hướng trên cùng: logo, menu, đổi ngôn ngữ, đổi giao diện, đăng nhập/đăng xuất. */
export function AppHeader() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isDark, toggleTheme } = useTheme()
  const { current, toggleLanguage } = useLanguage()
  const user = useAuthStore((state) => state.user)
  const logout = useAuthStore((state) => state.logout)

  const isAdmin = user?.roles.includes('Admin') ?? false

  const handleLogout = async () => {
    await logout()
    toast.success(t('auth.logout_success'))
    navigate('/login')
  }

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
      isActive
        ? 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300'
        : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
    )

  return (
    <header className="sticky top-0 z-40 border-b border-slate-200 bg-white/80 backdrop-blur-md dark:border-slate-800 dark:bg-slate-950/80">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
        <div className="flex items-center gap-6">
          <Link to="/" className="flex items-center gap-2.5">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 text-lg font-black text-white">
              C
            </span>
            <span className="bg-gradient-to-r from-indigo-600 via-purple-600 to-pink-600 bg-clip-text text-lg font-extrabold tracking-tight text-transparent">
              {t('app.title')}
            </span>
          </Link>

          <nav className="hidden items-center gap-1 sm:flex">
            <NavLink to="/" className={navLinkClass} end>
              {t('nav.home')}
            </NavLink>
            {user && (
              <NavLink to="/profile" className={navLinkClass}>
                {t('nav.profile')}
              </NavLink>
            )}
            {user && (
              <NavLink to="/search" className={navLinkClass}>
                {t('social.search')}
              </NavLink>
            )}
            {user && (
              <NavLink to="/friends" className={navLinkClass}>
                {t('social.friends')}
              </NavLink>
            )}
            {isAdmin && (
              <NavLink to="/admin/users" className={navLinkClass}>
                {t('nav.admin')}
              </NavLink>
            )}
          </nav>
        </div>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={toggleLanguage} title={t('app.switch_lang')}>
            <Languages className="h-4 w-4 text-indigo-500" />
            <span className="uppercase">{current}</span>
          </Button>

          <Button variant="outline" size="sm" onClick={toggleTheme} title={t('app.toggle_theme')}>
            {isDark ? <Sun className="h-4 w-4 text-amber-400" /> : <Moon className="h-4 w-4 text-slate-600" />}
          </Button>

          {user ? (
            <div className="flex items-center gap-2">
              <span className="hidden items-center gap-1.5 text-sm text-slate-600 dark:text-slate-300 md:flex">
                {isAdmin ? <ShieldCheck className="h-4 w-4 text-indigo-500" /> : <User className="h-4 w-4" />}
                {user.displayName}
              </span>
              <Button variant="ghost" size="sm" onClick={handleLogout}>
                <LogOut className="h-4 w-4" />
                <span className="hidden sm:inline">{t('nav.logout')}</span>
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <Button variant="ghost" size="sm" onClick={() => navigate('/login')}>
                {t('nav.login')}
              </Button>
              <Button size="sm" onClick={() => navigate('/register')}>
                {t('nav.register')}
              </Button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
