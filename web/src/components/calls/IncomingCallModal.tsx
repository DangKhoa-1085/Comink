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

  // Đếm ngược 30 giây rồi tự từ chối (giống điện thoại thật).
  useEffect(() => {
    const timer = setInterval(() => setSecondsLeft((value) => value - 1), 1000)
    return () => clearInterval(timer)
  }, [])

  useEffect(() => {
    if (secondsLeft <= 0 && !declineMutation.isPending) {
      declineMutation.mutate()
    }
  }, [secondsLeft, declineMutation])

  const caller = call.participants.find((participant) => participant.userId === call.initiatedBy)
  const isVideo = call.type === 'Video'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-sm rounded-2xl bg-white p-6 text-center shadow-2xl dark:bg-slate-900"
      >
        <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-indigo-100 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-300">
          {isVideo ? <Video className="h-7 w-7" /> : <Phone className="h-7 w-7" />}
        </div>

        <p className="text-xs uppercase tracking-wide text-slate-500">
          {isVideo ? t('call.incoming_video') : t('call.incoming_audio')}
        </p>
        <p className="mt-1 text-lg font-bold">{caller?.displayName ?? t('call.unknown_caller')}</p>
        <p className="mt-1 text-xs text-slate-500">
          {t('call.ringing_seconds', { seconds: Math.max(secondsLeft, 0) })}
        </p>

        <div className="mt-6 flex items-center justify-center gap-3">
          <Button variant="outline" onClick={() => declineMutation.mutate()} disabled={declineMutation.isPending}>
            <PhoneOff className="h-4 w-4" />
            {t('call.decline')}
          </Button>
          <Button onClick={() => answerMutation.mutate()} disabled={answerMutation.isPending}>
            <Phone className="h-4 w-4" />
            {t('call.answer')}
          </Button>
        </div>
      </div>
    </div>
  )
}
