import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Languages, Moon, Sun } from 'lucide-react'
import { useLanguage } from '../../hooks/useLanguage'
import { useTheme } from '../../hooks/useTheme'
import { CominkLogo } from '../brand/CominkLogo'
import { Button } from '../ui/Button'

/** Thanh điều hướng tối giản cho khách chưa đăng nhập (Trang giới thiệu, Đăng nhập, Đăng ký). */
export function AppHeader() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { isDark, toggleTheme } = useTheme()
  const { current, toggleLanguage } = useLanguage()

  return (
    <header className="sticky top-0 z-40 border-b border-slate-200/80 bg-white/85 backdrop-blur-md dark:border-slate-800 dark:bg-[#121826]/85">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-3 px-4">
        <Link to="/" aria-label={t('app.title')}>
          <CominkLogo size="md" showWordmark />
        </Link>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={toggleLanguage} title={t('app.switch_lang')}>
            <Languages className="h-4 w-4 text-sky-600 dark:text-sky-400" />
            <span className="uppercase">{current}</span>
          </Button>

          <Button variant="outline" size="sm" onClick={toggleTheme} title={t('app.toggle_theme')}>
            {isDark ? (
              <Sun className="h-4 w-4 text-amber-400" />
            ) : (
              <Moon className="h-4 w-4 text-slate-600" />
            )}
          </Button>

          <Button variant="ghost" size="sm" onClick={() => navigate('/login')}>
            {t('nav.login')}
          </Button>
          <Button size="sm" onClick={() => navigate('/register')}>
            {t('nav.register')}
          </Button>
        </div>
      </div>
    </header>
  )
}
