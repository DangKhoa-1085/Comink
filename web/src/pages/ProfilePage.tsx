import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Save, UserCircle2 } from 'lucide-react'
import { usersApi } from '../api/users'
import type { MyProfile } from '../api/types'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { profileSchema, type ProfileFormValues } from '../lib/validation'
import { useAuthStore } from '../stores/authStore'
import { toast } from '../stores/toastStore'
import { useLanguage, type SupportedLanguage } from '../hooks/useLanguage'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { Alert, Card, FieldError, Label, Spinner } from '../components/ui/Feedback'

/** Trang hồ sơ cá nhân: xem thông tin, sửa tên hiển thị/tiểu sử/địa điểm/ngày sinh, đổi ngôn ngữ. */
export function ProfilePage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const setUser = useAuthStore((state) => state.setUser)
  const { current, changeLanguage } = useLanguage()

  const { data: profile, isLoading, isError, error, refetch } = useQuery<MyProfile>({
    queryKey: ['profile', 'me'],
    queryFn: usersApi.getMe,
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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-16 text-slate-500">
        <Spinner className="h-5 w-5" />
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

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center gap-3">
        <UserCircle2 className="h-8 w-8 text-indigo-500" />
        <div>
          <h1 className="text-2xl font-bold">{t('profile.title')}</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">@{profile.userName}</p>
        </div>
      </div>

      {/* Thông tin chỉ đọc */}
      <Card>
        <h2 className="mb-4 text-base font-bold">{t('profile.account_info')}</h2>
        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
          <InfoRow label={t('auth.email')} value={profile.email} />
          <InfoRow label={t('auth.username')} value={profile.userName} />
          <InfoRow label={t('profile.roles')} value={profile.roles.join(', ')} />
          <InfoRow label={t('profile.joined_at')} value={new Date(profile.createdAt).toLocaleDateString()} />
        </dl>
        <p className="mt-3 text-xs text-slate-400">{t('profile.email_readonly')}</p>
      </Card>

      {/* Thống kê */}
      <Card>
        <h2 className="mb-4 text-base font-bold">{t('profile.stats')}</h2>
        <div className="grid grid-cols-3 gap-4 text-center">
          <Stat value={profile.friendCount} label={t('profile.friends')} />
          <Stat value={profile.followerCount} label={t('profile.followers')} />
          <Stat value={profile.followingCount} label={t('profile.following')} />
        </div>
      </Card>

      {/* Sửa hồ sơ */}
      <Card>
        <h2 className="mb-4 text-base font-bold">{t('profile.edit_title')}</h2>

        {updateMutation.isError && (
          <div className="mb-4">
            <Alert variant="error">
              {translateErrorCode(
                t,
                updateMutation.error instanceof ApiError ? updateMutation.error.code : 'INTERNAL_ERROR',
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
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-transparent focus:outline-none focus:ring-2 focus:ring-indigo-500 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
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
              <FieldError>{errors.dateOfBirth ? t(errors.dateOfBirth.message ?? '') : null}</FieldError>
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

      {/* Cài đặt: ngôn ngữ giao diện */}
      <Card>
        <h2 className="mb-4 text-base font-bold">{t('profile.language')}</h2>
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
      </Card>
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  )
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <div>
      <p className="text-2xl font-bold text-indigo-600 dark:text-indigo-400">{value}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{label}</p>
    </div>
  )
}
