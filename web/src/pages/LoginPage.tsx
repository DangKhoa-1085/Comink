import { useEffect } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { LogIn } from 'lucide-react'
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
    <div className="mx-auto max-w-md py-6">
      <Card>
        <h1 className="mb-1 text-2xl font-bold">{t('auth.login_title')}</h1>
        <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">{t('auth.login_subtitle')}</p>

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
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              invalid={Boolean(errors.password || fieldErrors.password)}
              {...register('password')}
            />
            <FieldError>{errors.password ? t(errors.password.message ?? '') : null}</FieldError>
          </div>

          <Button type="submit" className="w-full" disabled={loading}>
            <LogIn className="h-4 w-4" />
            {loading ? t('auth.logging_in') : t('auth.login_button')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
          {t('auth.no_account')}{' '}
          <Link to="/register" className="font-medium text-indigo-600 hover:underline dark:text-indigo-400">
            {t('auth.register_button')}
          </Link>
        </p>
      </Card>
    </div>
  )
}
