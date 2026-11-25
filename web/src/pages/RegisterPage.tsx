import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { UserPlus } from 'lucide-react'
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
    <div className="mx-auto max-w-md py-6">
      <Card>
        <h1 className="mb-1 text-2xl font-bold">{t('auth.register_title')}</h1>
        <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">{t('auth.register_subtitle')}</p>

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
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              invalid={Boolean(errors.password || fieldErrors.password)}
              {...register('password')}
            />
            <FieldError>
              {errors.password ? t(errors.password.message ?? '') : fieldErrors.password?.[0]}
            </FieldError>
            {!errors.password && <p className="mt-1 text-xs text-slate-400">{t('auth.password_hint')}</p>}
          </div>

          <Button type="submit" className="w-full" disabled={loading}>
            <UserPlus className="h-4 w-4" />
            {loading ? t('auth.registering') : t('auth.register_button')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
          {t('auth.has_account')}{' '}
          <Link to="/login" className="font-medium text-indigo-600 hover:underline dark:text-indigo-400">
            {t('auth.login_button')}
          </Link>
        </p>
      </Card>
    </div>
  )
}
