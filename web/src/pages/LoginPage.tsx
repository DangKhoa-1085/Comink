import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Eye, EyeOff, LogIn } from 'lucide-react'
import { loginSchema, type LoginFormValues } from '../lib/validation'
import { useAuthStore } from '../stores/authStore'
import { toast } from '../stores/toastStore'
import { translateErrorCode } from '../lib/errorMessages'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Alert, Card, FieldError, Label } from '../components/ui/Feedback'

/** Trang đăng nhập. */
export function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const user = useAuthStore((state) => state.user)
  const login = useAuthStore((state) => state.login)
  const loading = useAuthStore((state) => state.loading)
  const errorCode = useAuthStore((state) => state.errorCode)
  const fieldErrors = useAuthStore((state) => state.fieldErrors)
  const clearError = useAuthStore((state) => state.clearError)
  const [showPassword, setShowPassword] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { emailOrUserName: '', password: '' },
  })

  // Đã đăng nhập thì không cần ở lại trang này.
  useEffect(() => {
    if (user) {
      navigate('/', { replace: true })
    }
  }, [user, navigate])

  const onSubmit = async (values: LoginFormValues) => {
    clearError()
    const success = await login(values)
    if (success) {
      toast.success(t('auth.login_success'))
      const from = (location.state as { from?: string } | null)?.from
      navigate(from ?? '/', { replace: true })
    }
  }

  return (
    <div className="mx-auto grid max-w-4xl grid-cols-1 items-center gap-8 py-6 lg:grid-cols-2">
      <div className="hidden h-[460px] flex-col justify-between rounded-3xl bg-gradient-to-br from-sky-700 via-sky-600 to-cyan-500 p-8 text-white shadow-xl lg:flex">
        <div className="space-y-2">
          <span className="inline-block rounded-full bg-white/15 px-3 py-1 text-xs font-semibold">
            {t('app.title')}
          </span>
          <h2 className="text-3xl font-bold leading-snug">{t('app.welcome')}</h2>
          <p className="text-sm text-white/85">{t('app.subtitle')}</p>
        </div>
        <div className="space-y-2.5 text-xs text-white/90">
          <p>&#10003; {t('app.feature_social_desc')}</p>
          <p>&#10003; {t('app.feature_messaging_desc')}</p>
          <p>&#10003; {t('app.feature_calls_desc')}</p>
        </div>
      </div>

      <Card className="p-6 sm:p-8">
        <h1 className="mb-1 text-2xl font-bold text-slate-900 dark:text-slate-100">
          {t('auth.login_title')}
        </h1>
        <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">
          {t('auth.login_subtitle')}
        </p>

        {errorCode && (
          <div className="mb-4">
            <Alert variant="error">{translateErrorCode(t, errorCode)}</Alert>
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
          <div>
            <Label htmlFor="emailOrUserName">{t('auth.email_or_username')}</Label>
            <Input
              id="emailOrUserName"
              autoComplete="username"
              invalid={Boolean(errors.emailOrUserName || fieldErrors.emailOrUserName)}
              {...register('emailOrUserName')}
            />
            <FieldError>
              {errors.emailOrUserName ? t(errors.emailOrUserName.message ?? '') : null}
            </FieldError>
          </div>

          <div>
            <Label htmlFor="password">{t('auth.password')}</Label>
            <div className="relative">
              <Input
                id="password"
                type={showPassword ? 'text' : 'password'}
                autoComplete="current-password"
                className="pr-10"
                invalid={Boolean(errors.password || fieldErrors.password)}
                {...register('password')}
              />
              <button
                type="button"
                onClick={() => setShowPassword((prev) => !prev)}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-lg p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                aria-label={t('auth.password')}
              >
                {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
            <FieldError>{errors.password ? t(errors.password.message ?? '') : null}</FieldError>
          </div>

          <Button type="submit" className="w-full" disabled={loading}>
            <LogIn className="h-4 w-4" />
            {loading ? t('auth.logging_in') : t('auth.login_button')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
          {t('auth.no_account')}{' '}
          <Link
            to="/register"
            className="font-semibold text-sky-600 hover:underline dark:text-sky-400"
          >
            {t('auth.register_button')}
          </Link>
        </p>
      </Card>
    </div>
  )
}
