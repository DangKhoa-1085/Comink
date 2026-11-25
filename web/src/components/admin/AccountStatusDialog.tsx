import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Lock, Unlock, X } from 'lucide-react'
import { adminApi } from '../../api/admin'
import type { AdminUserSummary } from '../../api/types'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Alert, Label, Spinner } from '../ui/Feedback'
import { toast } from '../../stores/toastStore'

/**
 * Hộp thoại xác nhận khóa / mở khóa tài khoản (Giai đoạn 5).
 * Chế độ suy ra từ trạng thái hiện tại của `user`: `Locked` → mở khóa, còn lại → khóa.
 */
export function AccountStatusDialog({
  user,
  onClose,
}: {
  user: AdminUserSummary
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isLocked = user.accountStatus === 'Locked'
  const [reason, setReason] = useState('')
  const [errorCode, setErrorCode] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: () => (isLocked ? adminApi.unlockUser(user.id) : adminApi.lockUser(user.id, reason)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] })
      toast.success(isLocked ? t('admin.unlocked_success') : t('admin.locked_success'))
      onClose()
    },
    onError: (error) => {
      setErrorCode(error instanceof ApiError ? error.code : 'INTERNAL_ERROR')
    },
  })

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/60 p-4">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-md rounded-2xl bg-white p-5 shadow-2xl dark:bg-slate-900"
      >
        <div className="mb-3 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-base font-bold">
            {isLocked ? <Unlock className="h-4 w-4" /> : <Lock className="h-4 w-4" />}
            {isLocked ? t('admin.unlock_title') : t('admin.lock_title')}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1 hover:bg-slate-100 dark:hover:bg-slate-800"
            aria-label={t('common.cancel')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <p className="text-sm text-slate-600 dark:text-slate-300">
          {isLocked
            ? t('admin.unlock_confirm', { userName: user.userName })
            : t('admin.lock_confirm', { userName: user.userName })}
        </p>

        {!isLocked && (
          <div className="mt-4">
            <Label htmlFor="lock-reason">{t('admin.lock_reason')}</Label>
            <Input
              id="lock-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder={t('admin.lock_reason_placeholder')}
              maxLength={500}
            />
            <p className="mt-1 text-xs text-slate-500">{t('admin.lock_reason_hint')}</p>
          </div>
        )}

        {errorCode && (
          <div className="mt-4">
            <Alert variant="error">{translateErrorCode(t, errorCode)}</Alert>
          </div>
        )}

        <div className="mt-5 flex items-center justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant={isLocked ? 'primary' : 'danger'}
            onClick={() => {
              setErrorCode(null)
              mutation.mutate()
            }}
            disabled={mutation.isPending}
          >
            {mutation.isPending ? (
              <>
                <Spinner />
                {isLocked ? t('admin.unlocking') : t('admin.locking')}
              </>
            ) : (
              t('admin.confirm')
            )}
          </Button>
        </div>
      </div>
    </div>
  )
}
