import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Mic, MicOff, PhoneOff, Video, VideoOff } from 'lucide-react'
import { RoomEvent, Track, type Room, type RemoteTrack } from 'livekit-client'
import { callsApi } from '../api/calls'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { RELAY_ONLY, createCallRoom, normalizeLiveKitUrl } from '../lib/livekitRoom'
import { ApiError } from '../lib/http'
import { translateErrorCode } from '../lib/errorMessages'
import { useAuthStore } from '../stores/authStore'

const TERMINAL_STATUSES = ['Completed', 'Declined', 'Cancelled', 'Missed', 'Failed']

/** Màn hình gọi: kết nối room LiveKit, hiện video, bật/tắt mic–camera, kết thúc. */
export function CallPage() {
  const { t } = useTranslation()
  const { callId } = useParams<{ callId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const me = useAuthStore((state) => state.user)

  const [connectionState, setConnectionState] = useState<'connecting' | 'connected' | 'error'>('connecting')
  const [errorCode, setErrorCode] = useState<string | null>(null)
  const [errorDetail, setErrorDetail] = useState<string | null>(null)
  const [micOn, setMicOn] = useState(true)
  const [camOn, setCamOn] = useState(true)
  const [durationSeconds, setDurationSeconds] = useState(0)

  const roomRef = useRef<Room | null>(null)
  const remoteMediaRef = useRef<HTMLDivElement | null>(null)
  const localVideoRef = useRef<HTMLVideoElement | null>(null)

  // Poll nhẹ để biết cuộc gọi đã kết thúc (webhook hoặc phía bên kia bấm kết thúc).
  const callQuery = useQuery({
    queryKey: ['call', callId],
    queryFn: () => callsApi.get(callId!),
    enabled: Boolean(callId),
    refetchInterval: 3000
  })

  const isTerminal = callQuery.data ? TERMINAL_STATUSES.includes(callQuery.data.status) : false

  // Kết nối room một lần cho mỗi cuộc gọi.
  useEffect(() => {
    if (!callId || !me?.id) {
      return
    }

    let disposed = false
    const room = createCallRoom()
    roomRef.current = room

    const attachRemote = (track: RemoteTrack) => {
      const element = track.attach()
      element.style.width = '100%'
      element.style.maxHeight = '60vh'
      element.style.borderRadius = '0.75rem'
      element.style.objectFit = 'cover'
      remoteMediaRef.current?.appendChild(element)
    }

    room.on(RoomEvent.TrackSubscribed, attachRemote)
    room.on(RoomEvent.TrackUnsubscribed, (track) => track.detach().forEach((element) => element.remove()))

    void (async () => {
      try {
        const details = await callsApi.get(callId)

        // Người nhận phải "trả lời" trước khi vào room; người gọi thì không.
        if (details.status === 'Ringing' && details.initiatedBy !== me.id) {
          await callsApi.answer(callId)
        }

        // Token ngắn hạn (10 phút) — xin ngay trước khi kết nối.
        const token = await callsApi.token(callId)
        await room.connect(normalizeLiveKitUrl(token.url), token.token)

        if (disposed) {
          await room.disconnect()
          return
        }

        await room.localParticipant.setMicrophoneEnabled(true)
        if (details.type === 'Video') {
          await room.localParticipant.setCameraEnabled(true)
          const publication = room.localParticipant.getTrackPublication(Track.Source.Camera)
          if (publication?.videoTrack && localVideoRef.current) {
            publication.videoTrack.attach(localVideoRef.current)
          }
        } else {
          setCamOn(false)
        }

        setConnectionState('connected')
      } catch (error) {
        // Lỗi kết nối LiveKit không phải ApiError → nếu chỉ hiện "Máy chủ gặp lỗi" thì không
        // biết sửa ở đâu. Giữ mã dịch cho người dùng + chi tiết kỹ thuật để chẩn đoán.
        const message = error instanceof Error ? error.message : String(error)

        // Lỗi hay gặp nhất (2026-10-23): LiveKit báo "could not establish pc connection" khi
        // KHÔNG có đường tới cổng media (UDP 7882/TCP 7881) — thường do LiveKit đang quảng bá
        // một IP mà máy này không tới được (sai mạng / Tailscale offline). Phải báo đúng bản
        // chất thay vì "Máy chủ gặp lỗi".
        const code = error instanceof ApiError
          ? error.code
          : /pc connection|failed to connect|ice/i.test(message)
            ? 'CALL_MEDIA_FAILED'
            : 'INTERNAL_ERROR'

        const detail = error instanceof ApiError ? `${error.code} (HTTP ${error.status})` : message
        console.error('[call] Không kết nối được phòng LiveKit', { callId, error })
        if (!disposed) {
          setErrorCode(code)
          setErrorDetail(detail)
          setConnectionState('error')
        }
      }
    })()

    return () => {
      disposed = true
      void room.disconnect()
      roomRef.current = null
    }
  }, [callId, me?.id])

  // Đồng hồ thời lượng tính từ lúc có người trả lời.
  useEffect(() => {
    const answeredAt = callQuery.data?.answeredAt
    if (!answeredAt) {
      return
    }

    const update = () => {
      setDurationSeconds(Math.max(0, Math.round((Date.now() - new Date(answeredAt).getTime()) / 1000)))
    }

    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [callQuery.data?.answeredAt])

  const endCall = useCallback(async () => {
    await roomRef.current?.disconnect().catch(() => undefined)
    await callsApi.end(callId!).catch(() => undefined)
    queryClient.invalidateQueries({ queryKey: ['call', callId] })
    queryClient.invalidateQueries({ queryKey: ['calls'] })
    navigate(-1)
  }, [callId, navigate, queryClient])

  const toggleMic = async () => {
    const room = roomRef.current
    if (!room) {
      return
    }

    const next = !micOn
    await room.localParticipant.setMicrophoneEnabled(next)
    setMicOn(next)
  }

  const toggleCamera = async () => {
    const room = roomRef.current
    if (!room) {
      return
    }

    const next = !camOn
    await room.localParticipant.setCameraEnabled(next)
    setCamOn(next)

    if (next) {
      const publication = room.localParticipant.getTrackPublication(Track.Source.Camera)
      if (publication?.videoTrack && localVideoRef.current) {
        publication.videoTrack.attach(localVideoRef.current)
      }
    }
  }

  if (callQuery.isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
        <Spinner /> {t('social.loading')}
      </div>
    )
  }

  if (callQuery.isError || !callQuery.data) {
    return <Alert variant="error">{t('call.not_found')}</Alert>
  }

  // Camera/micro chỉ chạy trên secure context (HTTPS hoặc localhost). Mở bằng
  // `http://<ip-mạng>:5173` thì `navigator.mediaDevices` là `undefined` và LiveKit sẽ ném
  // "Cannot read properties of undefined (reading 'getUserMedia')" — báo lỗi này thay vì
  // để người dùng đoán.
  if (typeof window !== 'undefined' && !window.isSecureContext) {
    return (
      <div className="mx-auto max-w-xl space-y-3">
        <Alert variant="error">{t('call.insecure_context')}</Alert>
        <p className="text-xs text-slate-500">{t('call.insecure_context_hint')}</p>
        <Button onClick={() => navigate(-1)}>{t('call.back')}</Button>
      </div>
    )
  }

  if (isTerminal) {
    return (
      <div className="mx-auto max-w-md space-y-4 rounded-2xl border border-slate-200 p-6 text-center dark:border-slate-800">
        <p className="text-lg font-semibold">{t('call.ended')}</p>
        <p className="text-sm text-slate-500">
          {t('call.ended_detail', {
            duration: callQuery.data.durationSeconds ?? 0,
            reason: callQuery.data.endReason ?? 'Completed'
          })}
        </p>
        <Button onClick={() => navigate(-1)}>{t('call.back')}</Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-3 flex items-center justify-between">
        <div>
          <h1 className="text-lg font-bold">
            {callQuery.data.type === 'Video' ? t('call.video_title') : t('call.audio_title')}
          </h1>
          <p className="text-xs text-slate-500">
            {connectionState === 'connecting'
              ? t('call.connecting')
              : t('call.duration', { time: formatDuration(durationSeconds) })}
          </p>
        </div>
        {RELAY_ONLY && (
          <span className="rounded-full bg-amber-100 px-2 py-1 text-[11px] text-amber-900 dark:bg-amber-950 dark:text-amber-200">
            {t('call.relay_mode')}
          </span>
        )}
      </div>

      {connectionState === 'error' ? (
        <Alert variant="error">
          <p>{errorCode ? translateErrorCode(t, errorCode) : t('call.error')}</p>
          {errorDetail && <p className="mt-1 text-xs opacity-80">{errorDetail}</p>}
        </Alert>
      ) : (
        <>
          <div
            ref={remoteMediaRef}
            className="mb-3 flex min-h-[240px] flex-col items-center justify-center gap-2 rounded-2xl bg-slate-900 p-3"
          >
            {connectionState === 'connecting' && (
              <span className="flex items-center gap-2 text-sm text-slate-300">
                <Spinner /> {t('call.connecting')}
              </span>
            )}
            <span className="text-xs text-slate-500">{t('call.remote_hint')}</span>
          </div>

          <div className={camOn ? 'mb-3' : 'mb-3 hidden'}>
            <video
              ref={localVideoRef}
              autoPlay
              muted
              playsInline
              className="w-full max-h-56 rounded-2xl bg-slate-800 object-cover"
            />
          </div>
        </>
      )}

      <div className="flex items-center justify-center gap-2">
        <Button variant="outline" onClick={() => void toggleMic()} disabled={connectionState !== 'connected'}>
          {micOn ? <Mic className="h-4 w-4" /> : <MicOff className="h-4 w-4" />}
          {micOn ? t('call.mic_on') : t('call.mic_off')}
        </Button>
        <Button variant="outline" onClick={() => void toggleCamera()} disabled={connectionState !== 'connected'}>
          {camOn ? <Video className="h-4 w-4" /> : <VideoOff className="h-4 w-4" />}
          {camOn ? t('call.camera_on') : t('call.camera_off')}
        </Button>
        <Button onClick={() => void endCall()}>
          <PhoneOff className="h-4 w-4" />
          {t('call.hangup')}
        </Button>
      </div>
    </div>
  )
}

/** mm:ss */
function formatDuration(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}
