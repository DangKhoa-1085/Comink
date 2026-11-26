import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
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
import { Button } from '../components/ui/Button'
import { Card, Spinner } from '../components/ui/Feedback'
import { useAuthStore } from '../stores/authStore'
import { FeedPage } from './FeedPage'

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
    color: 'bg-sky-50 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300',
  },
  {
    icon: Users,
    titleKey: 'app.feature_social',
    descriptionKey: 'app.feature_social_desc',
    color: 'bg-cyan-50 text-cyan-600 dark:bg-cyan-500/15 dark:text-cyan-300',
  },
  {
    icon: MessageSquare,
    titleKey: 'app.feature_messaging',
    descriptionKey: 'app.feature_messaging_desc',
    color: 'bg-blue-50 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300',
  },
  {
    icon: Video,
    titleKey: 'app.feature_calls',
    descriptionKey: 'app.feature_calls_desc',
    color: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
]

/** Trang chủ: giới thiệu dự án, trạng thái backend và các khối tính năng. */
export function HomePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)

  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['health'],
    queryFn: systemApi.health,
    retry: 1,
  })

  // Người đã đăng nhập thấy bảng tin; khách thấy trang giới thiệu.
  if (user) {
    return <FeedPage />
  }

  return (
    <div className="space-y-12 py-4">
      <section className="grid grid-cols-1 items-center gap-8 lg:grid-cols-2">
        <div className="space-y-5">
          <div className="inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700 dark:border-emerald-800/70 dark:bg-emerald-950/60 dark:text-emerald-300">
            <span className="h-2 w-2 animate-pulse rounded-full bg-emerald-500" />
            {t('app.subtitle')}
          </div>

          <h1 className="text-3xl font-bold leading-tight text-slate-900 dark:text-slate-100 sm:text-4xl">
            {t('app.welcome')}
          </h1>

          <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-400 sm:text-base">
            {t('app.description')}
          </p>

          <div className="flex flex-wrap items-center gap-3">
            <Button size="lg" onClick={() => navigate('/register')}>
              {t('nav.register')}
            </Button>
            <Button variant="outline" size="lg" onClick={() => navigate('/login')}>
              {t('nav.login')}
            </Button>
          </div>
        </div>

        {/* Thẻ trạng thái backend + xem nhanh giao diện chat thời gian thực */}
        <Card className="space-y-4 shadow-md">
          <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-3 dark:border-slate-800">
            <span className="flex items-center gap-2 text-sm font-bold text-slate-800 dark:text-slate-100">
              <Globe2 className="h-4 w-4 text-sky-600 dark:text-sky-400" />
              {t('app.backend_status')}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => void refetch()}
              disabled={isFetching}
            >
              <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
              {t('app.check_health')}
            </Button>
          </div>

          <div className="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50/90 p-3.5 text-sm dark:border-slate-800 dark:bg-[#0B0F1A]">
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
                  ({data?.status} @{' '}
                  {data?.timestamp ? new Date(data.timestamp).toLocaleTimeString() : ''})
                </span>
              </span>
            )}
          </div>

          <div className="space-y-2 rounded-xl bg-slate-50 p-3.5 text-xs dark:bg-[#1A2234]">
            <div className="flex items-center justify-between text-slate-500 dark:text-slate-400">
              <span className="font-semibold text-slate-700 dark:text-slate-200">
                SignalR + LiveKit WebRTC
              </span>
              <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 font-semibold text-emerald-600 dark:text-emerald-300">
                Real-time
              </span>
            </div>
            <p className="leading-relaxed text-slate-500 dark:text-slate-400">
              {t('app.feature_messaging_desc')}
            </p>
          </div>
        </Card>
      </section>

      <section>
        <h2 className="mb-6 text-center text-xl font-bold text-slate-900 dark:text-slate-100">
          {t('app.features_title')}
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <Card key={feature.titleKey} className="transition hover:shadow-md">
              <div
                className={`mb-4 flex h-11 w-11 items-center justify-center rounded-xl ${feature.color}`}
              >
                <feature.icon className="h-5 w-5" />
              </div>
              <h3 className="mb-1.5 text-sm font-bold text-slate-900 dark:text-slate-100">
                {t(feature.titleKey)}
              </h3>
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
