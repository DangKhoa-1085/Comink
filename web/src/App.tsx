import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { 
  Users, 
  MessageSquare, 
  Video, 
  ShieldCheck, 
  CheckCircle2, 
  AlertCircle, 
  RefreshCw, 
  Moon, 
  Sun, 
  Languages, 
  Globe2 
} from 'lucide-react'

interface HealthResponse {
  status: string
  timestamp: string
}

export function App() {
  const { t, i18n } = useTranslation()
  const [isDark, setIsDark] = useState(() => {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  })

  useEffect(() => {
    if (isDark) {
      document.documentElement.classList.add('dark')
    } else {
      document.documentElement.classList.remove('dark')
    }
  }, [isDark])

  const toggleLanguage = () => {
    const nextLang = i18n.language.startsWith('vi') ? 'en' : 'vi'
    i18n.changeLanguage(nextLang)
  }

  const { data: health, isLoading, isError, refetch, isFetching } = useQuery<HealthResponse>({
    queryKey: ['health'],
    queryFn: async () => {
      const res = await fetch('/health')
      if (!res.ok) throw new Error('Network response was not ok')
      return res.json()
    },
    retry: 1,
  })

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 transition-colors duration-200">
      {/* Header bar */}
      <header className="border-b border-slate-200 dark:border-slate-800 bg-white/70 dark:bg-slate-900/70 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 flex items-center justify-center shadow-lg shadow-indigo-500/20 text-white font-black text-xl">
              C
            </div>
            <div>
              <span className="font-extrabold text-xl tracking-tight bg-gradient-to-r from-indigo-600 via-purple-600 to-pink-600 bg-clip-text text-transparent">
                Comink
              </span>
              <span className="ml-2 text-xs font-semibold px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 border border-indigo-200/50 dark:border-indigo-800/50">
                v0.1
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={toggleLanguage}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-100 dark:hover:bg-slate-800 text-sm font-medium transition cursor-pointer"
              title={t('app.switch_lang')}
            >
              <Languages className="w-4 h-4 text-indigo-500" />
              <span className="uppercase">{i18n.language.startsWith('vi') ? 'VI' : 'EN'}</span>
            </button>

            <button
              onClick={() => setIsDark(!isDark)}
              className="p-2 rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
              title={t('app.toggle_theme')}
            >
              {isDark ? <Sun className="w-4 h-4 text-amber-400" /> : <Moon className="w-4 h-4 text-slate-600" />}
            </button>
          </div>
        </div>
      </header>

      {/* Main hero section */}
      <main className="max-w-6xl mx-auto px-4 py-12">
        <div className="text-center max-w-3xl mx-auto mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200/60 dark:border-emerald-800/60 text-emerald-700 dark:text-emerald-300 text-xs font-semibold mb-4">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            {t('app.status')}
          </div>
          <h1 className="text-4xl sm:text-5xl font-black tracking-tight mb-4">
            {t('app.welcome')}
          </h1>
          <p className="text-lg text-slate-600 dark:text-slate-400 leading-relaxed">
            {t('app.description')}
          </p>
        </div>

        {/* Backend health check card */}
        <div className="max-w-xl mx-auto mb-16 p-5 rounded-2xl border border-slate-200/80 dark:border-slate-800 bg-white/60 dark:bg-slate-900/60 backdrop-blur-sm shadow-xl shadow-slate-200/30 dark:shadow-none">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <Globe2 className="w-4 h-4 text-indigo-500" />
              <span className="text-sm font-bold text-slate-700 dark:text-slate-300">
                {t('app.backend_status')} (ASP.NET Core .NET 10)
              </span>
            </div>
            <button
              onClick={() => refetch()}
              disabled={isFetching}
              className="inline-flex items-center gap-1 text-xs px-2.5 py-1 rounded-md bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 font-medium transition cursor-pointer disabled:opacity-50"
            >
              <RefreshCw className={`w-3 h-3 ${isFetching ? 'animate-spin' : ''}`} />
              {t('app.check_health')}
            </button>
          </div>

          <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-950/70 border border-slate-100 dark:border-slate-800 flex items-center justify-between text-sm">
            {isLoading ? (
              <span className="text-slate-500 flex items-center gap-2">
                <RefreshCw className="w-4 h-4 animate-spin" />
                {t('app.backend_connecting')}
              </span>
            ) : isError ? (
              <div className="flex items-center gap-2 text-rose-600 dark:text-rose-400">
                <AlertCircle className="w-4 h-4 flex-shrink-0" />
                <span className="text-xs font-medium">{t('app.backend_offline')}</span>
              </div>
            ) : (
              <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
                <CheckCircle2 className="w-4 h-4 flex-shrink-0" />
                <span className="font-semibold">{t('app.backend_online')}</span>
                <span className="text-xs text-slate-400">({health?.status} @ {health?.timestamp ? new Date(health.timestamp).toLocaleTimeString() : ''})</span>
              </div>
            )}
          </div>
        </div>

        {/* Feature Cards Grid */}
        <div className="mb-12">
          <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100 mb-6 text-center">
            {t('app.features_title')}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5">
            <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md transition">
              <div className="w-12 h-12 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center mb-4">
                <ShieldCheck className="w-6 h-6" />
              </div>
              <h3 className="font-bold text-base mb-2">{t('app.feature_identity')}</h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                {t('app.feature_identity_desc')}
              </p>
            </div>

            <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md transition">
              <div className="w-12 h-12 rounded-xl bg-purple-50 dark:bg-purple-950/60 text-purple-600 dark:text-purple-400 flex items-center justify-center mb-4">
                <Users className="w-6 h-6" />
              </div>
              <h3 className="font-bold text-base mb-2">{t('app.feature_social')}</h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                {t('app.feature_social_desc')}
              </p>
            </div>

            <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md transition">
              <div className="w-12 h-12 rounded-xl bg-pink-50 dark:bg-pink-950/60 text-pink-600 dark:text-pink-400 flex items-center justify-center mb-4">
                <MessageSquare className="w-6 h-6" />
              </div>
              <h3 className="font-bold text-base mb-2">{t('app.feature_messaging')}</h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                {t('app.feature_messaging_desc')}
              </p>
            </div>

            <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md transition">
              <div className="w-12 h-12 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mb-4">
                <Video className="w-6 h-6" />
              </div>
              <h3 className="font-bold text-base mb-2">{t('app.feature_calls')}</h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                {t('app.feature_calls_desc')}
              </p>
            </div>
          </div>
        </div>
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-200 dark:border-slate-800 py-6 text-center text-xs text-slate-500">
        <p>Comink &bull; Đồ án chuyên ngành công nghệ phần mềm &bull; 2026</p>
      </footer>
    </div>
  )
}

export default App
