import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import {
  AlertCircle,
  CheckCircle2,
  Globe2,
  MessageSquare,
  RefreshCw,
  ShieldCheck,
  Users,
  Video,
} from 'lucide-react'
import { systemApi } from '../api/system'
import { Card, Spinner } from '../components/ui/Feedback'
import { Button } from '../components/ui/Button'
import { useAuthStore } from '../stores/authStore'

interface Feature {
  icon: typeof Users
  titleKey: string
  descriptionKey: string
  color: string
}

const features: Feature[] = [
  {
    icon: ShieldCheck,
    titleKey: 'app.feature_identity',
    descriptionKey: 'app.feature_identity_desc',
    color: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-950/60 dark:text-indigo-400',
  },
  {
    icon: Users,
    titleKey: 'app.feature_social',
    descriptionKey: 'app.feature_social_desc',
    color: 'bg-purple-50 text-purple-600 dark:bg-purple-950/60 dark:text-purple-400',
  },
  {
    icon: MessageSquare,
    titleKey: 'app.feature_messaging',
    descriptionKey: 'app.feature_messaging_desc',
    color: 'bg-pink-50 text-pink-600 dark:bg-pink-950/60 dark:text-pink-400',
  },
  {
    icon: Video,
    titleKey: 'app.feature_calls',
    descriptionKey: 'app.feature_calls_desc',
    color: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400',
  },
]

/** Trang chủ: giới thiệu dự án, trạng thái backend và các khối tính năng. */
export function HomePage() {
  const { t } = useTranslation()
  const user = useAuthStore((state) => state.user)

  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['health'],
    queryFn: systemApi.health,
    retry: 1,
  })

  return (
    <div>
      <section className="mb-12 text-center">
        <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
          <span className="h-2 w-2 animate-pulse rounded-full bg-emerald-500" />
          {t('app.status')}
        </div>
        <h1 className="mb-4 text-4xl font-black tracking-tight sm:text-5xl">{t('app.welcome')}</h1>
        <p className="mx-auto max-w-3xl text-lg leading-relaxed text-slate-600 dark:text-slate-400">
          {t('app.description')}
        </p>
        {user && (
          <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
            {t('app.signed_in_as', { name: user.displayName })}
          </p>
        )}
      </section>

      {/* Trạng thái backend: có đủ ba trạng thái tải / lỗi / thành công */}
      <Card className="mx-auto mb-14 max-w-xl">
        <div className="mb-3 flex items-center justify-between">
          <span className="flex items-center gap-2 text-sm font-bold text-slate-700 dark:text-slate-300">
            <Globe2 className="h-4 w-4 text-indigo-500" />
            {t('app.backend_status')}
          </span>
          <Button variant="outline" size="sm" onClick={() => void refetch()} disabled={isFetching}>
            <RefreshCw className={`h-3 w-3 ${isFetching ? 'animate-spin' : ''}`} />
            {t('app.check_health')}
          </Button>
        </div>

        <div className="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-3.5 text-sm dark:border-slate-800 dark:bg-slate-950/70">
          {isLoading ? (
            <span className="flex items-center gap-2 text-slate-500">
              <Spinner />
              {t('app.backend_connecting')}
            </span>
          ) : isError ? (
            <span className="flex items-center gap-2 text-rose-600 dark:text-rose-400">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span className="text-xs font-medium">{t('app.backend_offline')}</span>
            </span>
          ) : (
            <span className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-4 w-4 shrink-0" />
              <span className="font-semibold">{t('app.backend_online')}</span>
              <span className="text-xs text-slate-400">
                ({data?.status} @ {data?.timestamp ? new Date(data.timestamp).toLocaleTimeString() : ''})
              </span>
            </span>
          )}
        </div>
      </Card>

      <section>
        <h2 className="mb-6 text-center text-xl font-bold">{t('app.features_title')}</h2>
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <Card key={feature.titleKey} className="transition hover:shadow-md">
              <div className={`mb-4 flex h-12 w-12 items-center justify-center rounded-xl ${feature.color}`}>
                <feature.icon className="h-6 w-6" />
              </div>
              <h3 className="mb-2 text-base font-bold">{t(feature.titleKey)}</h3>
              <p className="text-xs leading-relaxed text-slate-500 dark:text-slate-400">
                {t(feature.descriptionKey)}
              </p>
            </Card>
          ))}
        </div>
      </section>
    </div>
  )
}
