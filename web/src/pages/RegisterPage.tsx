import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Eye, EyeOff, UserPlus } from 'lucide-react'
import { registerSchema, type RegisterFormValues } from '../lib/validation'
import { useAuthStore } from '../stores/authStore'
import { toast } from '../stores/toastStore'
import { translateErrorCode } from '../lib/errorMessages'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Alert, Card, FieldError, Label } from '../components/ui/Feedback'

/** Trang đăng ký tài khoản. */
export function RegisterPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const registerUser = useAuthStore((state) => state.register)
  const loading = useAuthStore((state) => state.loading)
  const errorCode = useAuthStore((state) => state.errorCode)
  const fieldErrors = useAuthStore((state) => state.fieldErrors)
  const clearError = useAuthStore((state) => state.clearError)
  const [showPassword, setShowPassword] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { userName: '', email: '', displayName: '', password: '' },
  })

  const onSubmit = async (values: RegisterFormValues) => {
    clearError()
    const success = await registerUser(values)
    if (success) {
      toast.success(t('auth.register_success'))
      navigate('/', { replace: true })
    }
  }

  return (
    <div className="mx-auto grid max-w-4xl grid-cols-1 items-center gap-8 py-6 lg:grid-cols-2">
      <div className="hidden h-[520px] flex-col justify-between rounded-3xl bg-gradient-to-br from-sky-700 via-sky-600 to-cyan-500 p-8 text-white shadow-xl lg:flex">
        <div className="space-y-2">
          <span className="inline-block rounded-full bg-white/15 px-3 py-1 text-xs font-semibold">
            {t('app.title')}
          </span>
          <h2 className="text-3xl font-bold leading-snug">{t('auth.register_title')}</h2>
          <p className="text-sm text-white/85">{t('app.description')}</p>
        </div>
        <div className="space-y-2.5 text-xs text-white/90">
          <p>&#10003; {t('app.feature_identity_desc')}</p>
          <p>&#10003; {t('app.feature_messaging_desc')}</p>
          <p>&#10003; {t('app.feature_calls_desc')}</p>
        </div>
      </div>

      <Card className="p-6 sm:p-8">
        <h1 className="mb-1 text-2xl font-bold text-slate-900 dark:text-slate-100">
          {t('auth.register_title')}
        </h1>
        <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">
          {t('auth.register_subtitle')}
        </p>

        {/* Lỗi từ backend (trùng email, trùng tên đăng nhập, validate) hiển thị ở đây */}
        {errorCode && (
          <div className="mb-4">
            <Alert variant="error">{translateErrorCode(t, errorCode)}</Alert>
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
          <div>
            <Label htmlFor="userName">{t('auth.username')}</Label>
            <Input
              id="userName"
              autoComplete="username"
              invalid={Boolean(errors.userName || fieldErrors.userName)}
              {...register('userName')}
            />
            <FieldError>
              {errors.userName ? t(errors.userName.message ?? '') : fieldErrors.userName?.[0]}
            </FieldError>
            {!errors.userName && (
              <p className="mt-1 text-xs text-slate-400">{t('auth.username_hint')}</p>
            )}
          </div>

          <div>
            <Label htmlFor="email">{t('auth.email')}</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              invalid={Boolean(errors.email || fieldErrors.email)}
              {...register('email')}
            />
            <FieldError>
              {errors.email ? t(errors.email.message ?? '') : fieldErrors.email?.[0]}
            </FieldError>
          </div>

          <div>
            <Label htmlFor="displayName">{t('auth.display_name')}</Label>
            <Input
              id="displayName"
              autoComplete="name"
              invalid={Boolean(errors.displayName || fieldErrors.displayName)}
              {...register('displayName')}
            />
            <FieldError>
              {errors.displayName ? t(errors.displayName.message ?? '') : fieldErrors.displayName?.[0]}
            </FieldError>
          </div>

          <div>
            <Label htmlFor="password">{t('auth.password')}</Label>
            <div className="relative">
              <Input
                id="password"
                type={showPassword ? 'text' : 'password'}
                autoComplete="new-password"
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
            <FieldError>
              {errors.password ? t(errors.password.message ?? '') : fieldErrors.password?.[0]}
            </FieldError>
            {!errors.password && (
              <p className="mt-1 text-xs text-slate-400">{t('auth.password_hint')}</p>
            )}
          </div>

          <Button type="submit" className="w-full" disabled={loading}>
            <UserPlus className="h-4 w-4" />
            {loading ? t('auth.registering') : t('auth.register_button')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
          {t('auth.has_account')}{' '}
          <Link
            to="/login"
            className="font-semibold text-sky-600 hover:underline dark:text-sky-400"
          >
            {t('auth.login_button')}
          </Link>
        </p>
      </Card>
    </div>
  )
}
