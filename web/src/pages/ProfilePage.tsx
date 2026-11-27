import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Camera, Loader2, LogOut, Moon, Save, Sun, Trash2 } from 'lucide-react'
import { mediaApi } from '../api/media'
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
type ImageTarget = 'avatar' | 'cover'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Trang hồ sơ cá nhân: đổi ảnh bìa, ảnh đại diện, thống kê, sửa hồ sơ và cài đặt tài khoản. */
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
  const [uploadingTarget, setUploadingTarget] = useState<ImageTarget | null>(null)
  const avatarInputRef = useRef<HTMLInputElement | null>(null)
  const coverInputRef = useRef<HTMLInputElement | null>(null)

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

  const syncProfileUpdate = (updated: MyProfile, messageKey: string) => {
    const user = useAuthStore.getState().user
    if (user) {
      setUser({
        ...user,
        displayName: updated.displayName,
        avatarUrl: updated.avatarUrl,
      })
    }
    queryClient.setQueryData(['profile', 'me', currentUserId], updated)
    void queryClient.invalidateQueries({ queryKey: ['profile', 'me'] })
    void queryClient.invalidateQueries({ queryKey: ['public-profile'] })
    void queryClient.invalidateQueries({ queryKey: ['feed'] })
    void queryClient.invalidateQueries({ queryKey: ['conversations'] })
    toast.success(t(messageKey))
  }

  const handleImageUpload = async (file: File | undefined, target: ImageTarget) => {
    if (!file || uploadingTarget !== null) {
      return
    }

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.error(t('social.bad_type'))
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      toast.error(t('social.too_large_image'))
      return
    }

    setUploadingTarget(target)
    try {
      const context = target === 'avatar' ? 'Avatar' : 'Cover'
      const signature = await mediaApi.getSignature(context)
      const uploaded = await mediaApi.upload(signature, file)
      const asset = await mediaApi.register({
        providerPublicId: uploaded.providerPublicId,
        url: uploaded.url,
        thumbnailUrl: uploaded.url,
        mimeType: uploaded.mimeType,
        sizeBytes: uploaded.sizeBytes,
        context,
      })
      const updated = await usersApi.updateProfile(
        target === 'avatar' ? { avatarUrl: asset.url } : { coverUrl: asset.url },
      )
      syncProfileUpdate(
        updated,
        target === 'avatar' ? 'profile.avatar_updated' : 'profile.cover_updated',
      )
    } catch (uploadError) {
      const code = uploadError instanceof ApiError ? uploadError.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    } finally {
      setUploadingTarget(null)
      if (target === 'avatar' && avatarInputRef.current) {
        avatarInputRef.current.value = ''
      }
      if (target === 'cover' && coverInputRef.current) {
        coverInputRef.current.value = ''
      }
    }
  }

  const handleRemoveImage = async (target: ImageTarget) => {
    if (uploadingTarget !== null) {
      return
    }
    setUploadingTarget(target)
    try {
      const updated = await usersApi.updateProfile(
        target === 'avatar' ? { avatarUrl: '' } : { coverUrl: '' },
      )
      syncProfileUpdate(
        updated,
        target === 'avatar' ? 'profile.avatar_updated' : 'profile.cover_updated',
      )
    } catch (removeError) {
      const code = removeError instanceof ApiError ? removeError.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    } finally {
      setUploadingTarget(null)
    }
  }

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
        <input
          ref={coverInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={(event) => void handleImageUpload(event.target.files?.[0], 'cover')}
        />
        <input
          ref={avatarInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={(event) => void handleImageUpload(event.target.files?.[0], 'avatar')}
        />

        <div className="relative h-36 bg-gradient-to-r from-sky-700 via-sky-500 to-cyan-400 sm:h-44">
          {profile.coverUrl && (
            <img
              src={profile.coverUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          )}
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/45 via-transparent to-black/15" />

          <div className="absolute right-3 top-3 flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => coverInputRef.current?.click()}
              disabled={uploadingTarget !== null}
              className="inline-flex items-center gap-1.5 rounded-xl bg-black/45 px-3 py-1.5 text-xs font-semibold text-white backdrop-blur-xs transition-colors hover:bg-black/65 disabled:opacity-60"
            >
              {uploadingTarget === 'cover' ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Camera className="h-3.5 w-3.5" />
              )}
              <span>
                {uploadingTarget === 'cover'
                  ? t('profile.uploading_image')
                  : t('profile.change_cover')}
              </span>
            </button>
            {profile.coverUrl && (
              <button
                type="button"
                onClick={() => void handleRemoveImage('cover')}
                disabled={uploadingTarget !== null}
                title={t('profile.remove_cover')}
                aria-label={t('profile.remove_cover')}
                className="inline-flex h-7 w-7 items-center justify-center rounded-xl bg-black/45 text-white backdrop-blur-xs transition-colors hover:bg-rose-600/80 disabled:opacity-60"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            )}
          </div>

          <span className="absolute bottom-3 right-4 rounded-full bg-black/35 px-3 py-1 text-xs font-semibold text-white backdrop-blur-xs">
            {t('profile.joined_at')}: {new Date(profile.createdAt).toLocaleDateString()}
          </span>
        </div>

        <div className="px-5 pb-5">
          <div className="relative z-10 -mt-10 flex flex-wrap items-end justify-between gap-3">
            <div className="flex min-w-0 items-end gap-3.5">
              <div className="relative shrink-0 rounded-full bg-white p-1 ring-2 ring-white dark:bg-[#121826] dark:ring-[#121826]">
                <Avatar name={profile.displayName} url={profile.avatarUrl} size="lg" />
                <button
                  type="button"
                  onClick={() => avatarInputRef.current?.click()}
                  disabled={uploadingTarget !== null}
                  title={t('profile.change_avatar')}
                  aria-label={t('profile.change_avatar')}
                  className="absolute bottom-0 right-0 flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-sky-600 text-white shadow-sm transition-colors hover:bg-sky-700 disabled:opacity-60 dark:border-[#121826] dark:bg-sky-500 dark:hover:bg-sky-400"
                >
                  {uploadingTarget === 'avatar' ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Camera className="h-3.5 w-3.5" />
                  )}
                </button>
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

            {profile.avatarUrl && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => void handleRemoveImage('avatar')}
                disabled={uploadingTarget !== null}
                className="text-xs text-slate-500 hover:text-rose-600 dark:text-slate-400 dark:hover:text-rose-400"
              >
                <Trash2 className="h-3.5 w-3.5" />
                {t('profile.remove_avatar')}
              </Button>
            )}
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
          <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-3">
            <InfoRow label={t('auth.email')} value={profile.email} />
            <InfoRow label={t('auth.username')} value={profile.userName} />
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
