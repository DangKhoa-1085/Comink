import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Compass } from 'lucide-react'
import { Button } from '../components/ui/Button'

/** Trang 404. */
export function NotFoundPage() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
      <Compass className="h-10 w-10 text-slate-300" />
      <h1 className="text-3xl font-bold">404</h1>
      <p className="text-slate-500 dark:text-slate-400">{t('errors.NOT_FOUND')}</p>
      <Link to="/">
        <Button variant="outline">{t('common.back_home')}</Button>
      </Link>
    </div>
  )
}
