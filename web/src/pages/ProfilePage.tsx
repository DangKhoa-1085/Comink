import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { LogOut, Moon, Save, Sun } from 'lucide-react'
import { usersApi } from '../api/users'
import type { MyProfile } from '../api/types'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { profileSchema, type ProfileFormValues } from '../lib/validation'
import { useAuthStore } from '../stores/authStore'
import { toast } from '../stores/toastStore'
import { useLanguage, type SupportedLanguage } from '../hooks/useLanguage'
import { useTheme } from '../hooks/useTheme'
import { Avatar } from '../components/social/Avatar'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Alert, Card, FieldError, Label, Spinner } from '../components/ui/Feedback'
import { cn } from '../lib/utils'

type ProfileTab = 'edit' | 'account' | 'settings'

/** Trang hồ sơ cá nhân: ảnh bìa thương hiệu, thống kê, sửa hồ sơ và cài đặt tài khoản. */
export function ProfilePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const currentUserId = useAuthStore((state) => state.user?.id)
  const setUser = useAuthStore((state) => state.setUser)
  const logout = useAuthStore((state) => state.logout)
  const { current, changeLanguage } = useLanguage()
  const { isDark, toggleTheme } = useTheme()
  const [tab, setTab] = useState<ProfileTab>('edit')

  const { data: profile, isLoading, isError, error, refetch } = useQuery<MyProfile>({
    // Gắn userId vào queryKey để tách biệt cache hồ sơ giữa các tài khoản.
    queryKey: ['profile', 'me', currentUserId],
    queryFn: usersApi.getMe,
    enabled: Boolean(currentUserId),
  })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { displayName: '', bio: '', location: '', dateOfBirth: '' },
  })

  // Đổ dữ liệu hồ sơ vào form sau khi tải xong.
  useEffect(() => {
    if (profile) {
      reset({
        displayName: profile.displayName,
        bio: profile.bio ?? '',
        location: profile.location ?? '',
        dateOfBirth: profile.dateOfBirth ?? '',
      })
    }
  }, [profile, reset])

  const updateMutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      usersApi.updateProfile({
        displayName: values.displayName,
        bio: values.bio ?? '',
        location: values.location ?? '',
        dateOfBirth: values.dateOfBirth || undefined,
      }),
    onSuccess: (updated: MyProfile) => {
      // Giữ tiêu đề/nav hiển thị đúng tên mới ngay lập tức.
      const user = useAuthStore.getState().user
      if (user) {
        setUser({ ...user, displayName: updated.displayName })
      }
      void queryClient.invalidateQueries({ queryKey: ['profile', 'me'] })
      reset({
        displayName: updated.displayName,
        bio: updated.bio ?? '',
        location: updated.location ?? '',
        dateOfBirth: updated.dateOfBirth ?? '',
      })
      toast.success(t('profile.update_success'))
    },
  })

  const handleLogout = async () => {
    await logout()
    toast.success(t('auth.logout_success'))
    navigate('/login')
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-16 text-slate-500">
        <Spinner className="h-5 w-5 text-sky-600 dark:text-sky-400" />
        {t('common.loading')}
      </div>
    )
  }

  if (isError || !profile) {
    const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
    return (
      <div className="mx-auto max-w-lg py-10">
        <Alert variant="error">
          <p className="mb-2 font-medium">{t('common.error_title')}</p>
          <p className="mb-3">{translateErrorCode(t, code)}</p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            {t('common.retry')}
          </Button>
        </Alert>
      </div>
    )
  }

  const tabs: { id: ProfileTab; label: string }[] = [
    { id: 'edit', label: t('profile.tab_edit') },
    { id: 'account', label: t('profile.tab_account') },
    { id: 'settings', label: t('profile.tab_settings') },
  ]

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="overflow-hidden rounded-2xl border border-slate-200/90 bg-white shadow-xs dark:border-slate-800 dark:bg-[#121826]">
        <div className="relative h-28 bg-gradient-to-r from-sky-700 via-sky-500 to-cyan-400">
          <span className="absolute bottom-3 right-4 rounded-full bg-black/25 px-3 py-1 text-xs font-semibold text-white backdrop-blur-xs">
            {t('profile.joined_at')}: {new Date(profile.createdAt).toLocaleDateString()}
          </span>
        </div>

        <div className="px-5 pb-5">
          <div className="relative z-10 -mt-10 flex flex-wrap items-end justify-between gap-3">
            <div className="flex min-w-0 items-end gap-3.5">
              <div className="shrink-0 rounded-full bg-white p-1 ring-2 ring-white dark:bg-[#121826] dark:ring-[#121826]">
                <Avatar name={profile.displayName} url={profile.avatarUrl} size="lg" />
              </div>
              <div className="min-w-0 pt-11">
                <h1 className="truncate text-xl font-bold text-slate-900 dark:text-slate-100">
                  {profile.displayName}
                </h1>
                <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                  @{profile.userName}
                  {profile.location ? ` · ${profile.location}` : ''}
                </p>
              </div>
            </div>
          </div>

          {profile.bio && (
            <p className="mt-3 text-sm leading-relaxed text-slate-700 dark:text-slate-200">
              {profile.bio}
            </p>
          )}

          <div className="mt-4 grid grid-cols-3 gap-2.5 border-t border-slate-100 pt-4 text-center dark:border-slate-800">
            <Stat value={profile.friendCount} label={t('profile.friends')} />
            <Stat value={profile.followerCount} label={t('profile.followers')} />
            <Stat value={profile.followingCount} label={t('profile.following')} />
          </div>
        </div>
      </div>

      <div className="flex items-center gap-1.5 overflow-x-auto">
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={cn(
              'shrink-0 whitespace-nowrap rounded-xl px-3.5 py-2 text-xs font-semibold transition-colors',
              tab === item.id
                ? 'bg-sky-600 text-white shadow-xs dark:bg-sky-500'
                : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-slate-800 dark:bg-[#121826] dark:text-slate-300 dark:hover:bg-slate-800',
            )}
          >
            {item.label}
          </button>
        ))}
      </div>

      {tab === 'edit' && (
        <Card>
          <h2 className="mb-4 text-base font-bold">{t('profile.edit_title')}</h2>

          {updateMutation.isError && (
            <div className="mb-4">
              <Alert variant="error">
                {translateErrorCode(
                  t,
                  updateMutation.error instanceof ApiError
                    ? updateMutation.error.code
                    : 'INTERNAL_ERROR',
                )}
              </Alert>
            </div>
          )}

          <form
            onSubmit={handleSubmit((values) => updateMutation.mutate(values))}
            noValidate
            className="space-y-4"
          >
            <div>
              <Label htmlFor="displayName">{t('profile.display_name')}</Label>
              <Input
                id="displayName"
                invalid={Boolean(errors.displayName)}
                {...register('displayName')}
              />
              <FieldError>{errors.displayName ? t(errors.displayName.message ?? '') : null}</FieldError>
            </div>

            <div>
              <Label htmlFor="bio">{t('profile.bio')}</Label>
              <textarea
                id="bio"
                rows={3}
                maxLength={300}
                className="w-full rounded-xl border border-slate-200 bg-slate-50/70 px-3.5 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-transparent focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500 dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-100 dark:focus:bg-[#121826]"
                {...register('bio')}
              />
              <FieldError>{errors.bio ? t(errors.bio.message ?? '') : null}</FieldError>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <Label htmlFor="location">{t('profile.location')}</Label>
                <Input id="location" invalid={Boolean(errors.location)} {...register('location')} />
                <FieldError>{errors.location ? t(errors.location.message ?? '') : null}</FieldError>
              </div>
              <div>
                <Label htmlFor="dateOfBirth">{t('profile.date_of_birth')}</Label>
                <Input
                  id="dateOfBirth"
                  type="date"
                  invalid={Boolean(errors.dateOfBirth)}
                  {...register('dateOfBirth')}
                />
                <FieldError>
                  {errors.dateOfBirth ? t(errors.dateOfBirth.message ?? '') : null}
                </FieldError>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Button type="submit" disabled={updateMutation.isPending || !isDirty}>
                <Save className="h-4 w-4" />
                {updateMutation.isPending ? t('common.saving') : t('common.save')}
              </Button>
              <Button
                variant="ghost"
                onClick={() =>
                  reset({
                    displayName: profile.displayName,
                    bio: profile.bio ?? '',
                    location: profile.location ?? '',
                    dateOfBirth: profile.dateOfBirth ?? '',
                  })
                }
                disabled={!isDirty}
              >
                {t('common.cancel')}
              </Button>
            </div>
          </form>
        </Card>
      )}

      {tab === 'account' && (
        <Card>
          <h2 className="mb-4 text-base font-bold">{t('profile.account_info')}</h2>
          <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
            <InfoRow label={t('auth.email')} value={profile.email} />
            <InfoRow label={t('auth.username')} value={profile.userName} />
            <InfoRow label={t('profile.roles')} value={profile.roles.join(', ')} />
            <InfoRow
              label={t('profile.joined_at')}
              value={new Date(profile.createdAt).toLocaleDateString()}
            />
          </dl>
          <p className="mt-3 text-xs text-slate-400">{t('profile.email_readonly')}</p>
        </Card>
      )}

      {tab === 'settings' && (
        <Card className="space-y-5">
          <div>
            <h2 className="mb-3 text-sm font-bold">{t('profile.language')}</h2>
            <div className="flex gap-2">
              {(['vi', 'en'] as SupportedLanguage[]).map((language) => (
                <Button
                  key={language}
                  variant={current === language ? 'primary' : 'outline'}
                  size="sm"
                  onClick={() => changeLanguage(language)}
                >
                  {language === 'vi' ? 'Tiếng Việt' : 'English'}
                </Button>
              ))}
            </div>
          </div>

          <div className="border-t border-slate-100 pt-4 dark:border-slate-800">
            <h2 className="mb-3 text-sm font-bold">{t('nav.theme')}</h2>
            <Button variant="outline" size="sm" onClick={toggleTheme}>
              {isDark ? <Sun className="h-4 w-4 text-amber-400" /> : <Moon className="h-4 w-4 text-sky-600" />}
              <span>{isDark ? t('nav.theme_dark') : t('nav.theme_light')}</span>
            </Button>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4 dark:border-slate-800">
            <div>
              <h2 className="text-sm font-bold text-rose-600 dark:text-rose-400">
                {t('profile.session_title')}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {t('profile.session_desc')}
              </p>
            </div>
            <Button variant="danger" size="sm" onClick={() => void handleLogout()}>
              <LogOut className="h-4 w-4" />
              {t('nav.logout')}
            </Button>
          </div>
        </Card>
      )}
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-slate-50 p-3 dark:bg-[#1A2234]">
      <dt className="text-xs uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="mt-0.5 font-medium text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  )
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <div className="rounded-xl bg-slate-50 px-2 py-2.5 dark:bg-[#1A2234]">
      <p className="text-lg font-bold text-sky-600 dark:text-sky-400">{value}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{label}</p>
    </div>
  )
}
