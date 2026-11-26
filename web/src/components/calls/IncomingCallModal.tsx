import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Phone, PhoneOff, Video } from 'lucide-react'
import { callsApi, type CallDto } from '../../api/calls'
import { Button } from '../ui/Button'
import { useCallStore } from '../../stores/callStore'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'

/** Đổ chuông tối đa 30 giây — khớp `CallRules.RingTimeoutSeconds` ở backend. */
const RING_TIMEOUT_SECONDS = 30

/**
 * Modal toàn cục hiện khi có `CallIncoming` (docs/05_api_contract.md mục 9.6).
 * Tách component con để mỗi cuộc gọi đến là một lần mount mới (đồng hồ đếm ngược
 * khởi tạo đúng, không phải setState trong effect).
 */
export function IncomingCallModal() {
  const incoming = useCallStore((state) => state.incoming)

  if (!incoming) {
    return null
  }

  return <IncomingCallDialog key={incoming.id} call={incoming} />
}

function IncomingCallDialog({ call }: { call: CallDto }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const clearIncoming = useCallStore((state) => state.clearIncoming)
  const [secondsLeft, setSecondsLeft] = useState(RING_TIMEOUT_SECONDS)

  const answerMutation = useMutation({
    mutationFn: () => callsApi.answer(call.id),
    onSuccess: (answered) => {
      clearIncoming()
      queryClient.invalidateQueries({ queryKey: ['call', answered.id] })
      navigate(`/calls/${answered.id}`)
    },
    onError: (error) => {
      clearIncoming()
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    }
  })

  const declineMutation = useMutation({
    mutationFn: () => callsApi.decline(call.id),
    onSettled: () => clearIncoming()
  })

  // Đếm ngược 30 giây. Quyết định D8 (Ưu tiên 3): KHÔNG tự gọi `decline` khi hết giờ —
  // nếu không cuộc gọi bị ghi "Declined" thay vì "Missed" và người bị gỡ không nhận được
  // thông báo nhỡ. Server (`CallRingTimeoutService`) sẽ đóng cuộc gọi và phát `CallEnded`.
  useEffect(() => {
    const timer = setInterval(() => setSecondsLeft((value) => (value <= 0 ? 0 : value - 1)), 1000)
    return () => clearInterval(timer)
  }, [])

  const expired = secondsLeft <= 0

  // Dự phòng khi sự kiện `CallEnded` bị mất: đóng UI sau vài giây, không đổi trạng thái server.
  useEffect(() => {
    if (!expired) {
      return
    }

    const timer = setTimeout(() => clearIncoming(), 8000)
    return () => clearTimeout(timer)
  }, [expired, clearIncoming])

  const caller = call.participants.find((participant) => participant.userId === call.initiatedBy)
  const isVideo = call.type === 'Video'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-xs">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-sm rounded-3xl border border-slate-200 bg-white p-6 text-center shadow-2xl dark:border-slate-800 dark:bg-[#121826]"
      >
        <div className="relative mx-auto mb-4 flex h-16 w-16 items-center justify-center">
          <span className="absolute inset-0 animate-ping rounded-full bg-emerald-500/25" />
          <div className="relative flex h-14 w-14 items-center justify-center rounded-full bg-emerald-500 text-white shadow-lg">
            {isVideo ? <Video className="h-7 w-7" /> : <Phone className="h-7 w-7" />}
          </div>
        </div>

        <p className="text-xs font-bold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
          {isVideo ? t('call.incoming_video') : t('call.incoming_audio')}
        </p>
        <p className="mt-1 text-lg font-bold text-slate-900 dark:text-slate-100">
          {caller?.displayName ?? t('call.unknown_caller')}
        </p>
        <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
          {expired
            ? t('call.ring_timeout')
            : t('call.ringing_seconds', { seconds: Math.max(secondsLeft, 0) })}
        </p>

        <div className="mt-6 grid grid-cols-2 gap-3">
          <Button
            variant="danger"
            onClick={() => declineMutation.mutate()}
            disabled={declineMutation.isPending || expired}
          >
            <PhoneOff className="h-4 w-4" />
            {t('call.decline')}
          </Button>
          <Button
            className="bg-emerald-600 text-white hover:bg-emerald-500 dark:bg-emerald-600 dark:hover:bg-emerald-500"
            onClick={() => answerMutation.mutate()}
            disabled={answerMutation.isPending || expired}
          >
            <Phone className="h-4 w-4" />
            {t('call.answer')}
          </Button>
        </div>
      </div>
    </div>
  )
}
