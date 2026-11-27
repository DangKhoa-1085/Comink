import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Mic, MicOff, PhoneOff, RefreshCw, Video, VideoOff } from 'lucide-react'
import { RoomEvent, Track, type Room, type RemoteTrack } from 'livekit-client'
import { callsApi } from '../api/calls'
import { Button } from '../components/ui/Button'
import { Alert, Spinner } from '../components/ui/Feedback'
import { formatCallDuration } from '../lib/callFormat'
import { initialsOf } from '../lib/format'
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
  /** Tăng lên khi người dùng bấm "Thử lại" — chạy lại effect kết nối. */
  const [connectionAttempt, setConnectionAttempt] = useState(0)
  /** Những người khác đã thực sự vào phòng — dùng để bỏ dòng "đang chờ". */
  const [remoteUserIds, setRemoteUserIds] = useState<string[]>([])
  /** Đã có video của người khác chưa; nếu chưa thì hiện avatar thay vì ô trống. */
  const [hasRemoteVideo, setHasRemoteVideo] = useState(false)

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
  const remotePeople = remoteUserIds.map((userId) => ({
    userId,
    displayName:
      callQuery.data?.participants.find((participant) => participant.userId === userId)
        ?.displayName ?? t('call.unknown_caller')
  }))
  const isWaiting = remotePeople.length === 0

  // Kết nối room một lần cho mỗi cuộc gọi.
  useEffect(() => {
    if (!callId || !me?.id) {
      return
    }

    let disposed = false
    const room = createCallRoom()
    roomRef.current = room

    const hasSubscribedVideo = () =>
      [...room.remoteParticipants.values()].some((participant) =>
        [...participant.videoTrackPublications.values()].some((publication) => Boolean(publication.track)),
      )

    const syncRemoteParticipants = () => {
      if (disposed) {
        return
      }
      const ids = [...room.remoteParticipants.keys()]
      setRemoteUserIds(ids)
      if (ids.length === 0) {
        setHasRemoteVideo(false)
      }
    }

    const attachRemote = (track: RemoteTrack) => {
      if (disposed) {
        return
      }
      if (track.kind === Track.Kind.Video) {
        setHasRemoteVideo(true)
      }
      const element = track.attach()
      element.style.width = '100%'
      element.style.maxHeight = '60vh'
      element.style.borderRadius = '0.75rem'
      element.style.objectFit = 'cover'
      remoteMediaRef.current?.appendChild(element)
    }

    const detachRemote = (track: RemoteTrack) => {
      track.detach().forEach((element) => element.remove())
      if (track.kind === Track.Kind.Video) {
        setHasRemoteVideo(hasSubscribedVideo())
      }
    }

    room.on(RoomEvent.TrackSubscribed, attachRemote)
    room.on(RoomEvent.TrackUnsubscribed, detachRemote)
    room.on(RoomEvent.ParticipantConnected, syncRemoteParticipants)
    room.on(RoomEvent.ParticipantDisconnected, syncRemoteParticipants)

    void (async () => {
      try {
        const details = await callsApi.get(callId)

        // React StrictMode chạy effect hai lần ở dev: nếu cleanup đã chạy trong lúc chờ API
        // thì dừng ngay, tránh mở hai kết nối LiveKit cùng identity (DUPLICATE_IDENTITY).
        if (disposed) {
          return
        }

        // Người nhận phải "trả lời" trước khi vào room; người gọi thì không.
        if (details.status === 'Ringing' && details.initiatedBy !== me.id) {
          await callsApi.answer(callId)
          if (disposed) {
            return
          }
        }

        // Token ngắn hạn (10 phút) — xin ngay trước khi kết nối.
        const token = await callsApi.token(callId)
        if (disposed) {
          return
        }

        await room.connect(normalizeLiveKitUrl(token.url), token.token)

        if (disposed) {
          await room.disconnect()
          return
        }

        syncRemoteParticipants()

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
      room.off(RoomEvent.TrackSubscribed, attachRemote)
      room.off(RoomEvent.TrackUnsubscribed, detachRemote)
      room.off(RoomEvent.ParticipantConnected, syncRemoteParticipants)
      room.off(RoomEvent.ParticipantDisconnected, syncRemoteParticipants)
      void room.disconnect()
      // Chỉ xoá nếu vẫn là room của effect này — tránh cleanup cũ xoá room mới khi StrictMode
      // hoặc khi bấm "Thử lại" tạo effect mới.
      if (roomRef.current === room) {
        roomRef.current = null
      }
    }
  }, [callId, me?.id, connectionAttempt])

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

  const invalidateAfterCallEnd = useCallback(
    (conversationId?: string) => {
      void queryClient.invalidateQueries({ queryKey: ['call', callId] })
      void queryClient.invalidateQueries({ queryKey: ['calls'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['conversations'], refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: ['notifications'], refetchType: 'all' })
      void queryClient.invalidateQueries({
        queryKey: conversationId ? ['messages', conversationId] : ['messages'],
        refetchType: 'all',
      })
    },
    [callId, queryClient],
  )

  useEffect(() => {
    if (isTerminal) {
      invalidateAfterCallEnd(callQuery.data?.conversationId)
    }
  }, [isTerminal, callQuery.data?.conversationId, invalidateAfterCallEnd])

  const endCall = useCallback(async () => {
    await roomRef.current?.disconnect().catch(() => undefined)
    const ended = await callsApi.end(callId!).catch(() => undefined)
    invalidateAfterCallEnd(ended?.conversationId ?? callQuery.data?.conversationId)
    navigate(-1)
  }, [callId, callQuery.data?.conversationId, invalidateAfterCallEnd, navigate])

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
      <div className="flex min-h-screen items-center justify-center gap-2 text-sm text-slate-300">
        <Spinner className="text-sky-400" /> {t('social.loading')}
      </div>
    )
  }

  if (callQuery.isError || !callQuery.data) {
    return (
      <div className="mx-auto max-w-md px-4 py-16">
        <Alert variant="error">{t('call.not_found')}</Alert>
      </div>
    )
  }

  // Camera/micro chỉ chạy trên secure context (HTTPS hoặc localhost). Mở bằng
  // `http://<ip-mạng>:5173` thì `navigator.mediaDevices` là `undefined` và LiveKit sẽ ném
  // "Cannot read properties of undefined (reading 'getUserMedia')" — báo lỗi này thay vì
  // để người dùng đoán.
  if (typeof window !== 'undefined' && !window.isSecureContext) {
    return (
      <div className="mx-auto max-w-xl space-y-3 px-4 py-16">
        <Alert variant="error">{t('call.insecure_context')}</Alert>
        <p className="text-xs text-slate-400">{t('call.insecure_context_hint')}</p>
        <Button onClick={() => navigate(-1)}>{t('call.back')}</Button>
      </div>
    )
  }

  if (isTerminal) {
    const endReason = callQuery.data.endReason ?? 'Completed'
    return (
      <div className="flex min-h-screen items-center justify-center p-4">
        <div className="w-full max-w-md space-y-4 rounded-2xl border border-white/10 bg-[#121826] p-6 text-center shadow-2xl">
          <p className="text-lg font-bold text-white">{t('call.ended')}</p>
          <p className="text-sm text-slate-400">
            {t('call.ended_detail', {
              duration: formatCallDuration(callQuery.data.durationSeconds ?? 0, t),
              reason: t(`call.reason_${endReason.toLowerCase()}`, { defaultValue: endReason }),
            })}
          </p>
          <Button
            onClick={() => {
              invalidateAfterCallEnd(callQuery.data?.conversationId)
              navigate(-1)
            }}
          >
            {t('call.back')}
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="relative mx-auto flex min-h-screen max-w-5xl flex-col justify-between p-4 sm:p-6">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-bold text-white">
              {callQuery.data.type === 'Video' ? t('call.video_title') : t('call.audio_title')}
            </h1>
            <span className="rounded-full bg-emerald-500/20 px-2.5 py-0.5 text-xs font-semibold text-emerald-300">
              {connectionState === 'connecting' ? t('call.connecting') : t('call.connected')}
            </span>
          </div>
          <p className="text-xs text-slate-400">
            {connectionState === 'connecting'
              ? t('call.connecting')
              : t('call.duration', { time: formatDuration(durationSeconds) })}
          </p>
        </div>
        {RELAY_ONLY && (
          <span className="rounded-full bg-amber-500/20 px-3 py-1 text-xs font-semibold text-amber-300">
            {t('call.relay_mode')}
          </span>
        )}
      </div>

      {connectionState === 'error' ? (
        <div className="my-auto">
          <Alert variant="error">
            <p>{errorCode ? translateErrorCode(t, errorCode) : t('call.error')}</p>
            {errorDetail && <p className="mt-1 text-xs opacity-80">{errorDetail}</p>}
          </Alert>
          <Button
            variant="outline"
            className="mt-3"
            onClick={() => {
              // Đặt lại trạng thái trong sự kiện (không setState đồng bộ trong effect).
              setConnectionState('connecting')
              setErrorCode(null)
              setErrorDetail(null)
              setRemoteUserIds([])
              setHasRemoteVideo(false)
              setConnectionAttempt((value) => value + 1)
            }}
          >
            <RefreshCw className="h-4 w-4" />
            {t('call.retry')}
          </Button>
        </div>
      ) : (
        <div className="relative my-3 flex flex-1 flex-col justify-center">
          <div
            ref={remoteMediaRef}
            className="flex min-h-[320px] flex-1 flex-col items-center justify-center gap-3 rounded-2xl border border-white/10 bg-[#121826] p-4"
          >
            {connectionState === 'connecting' && isWaiting && (
              <span className="flex items-center gap-2 text-sm text-slate-300">
                <Spinner className="text-sky-400" /> {t('call.connecting')}
              </span>
            )}
            {isWaiting ? (
              <span className="text-xs text-slate-400">{t('call.remote_hint')}</span>
            ) : !hasRemoteVideo ? (
              <div className="flex flex-wrap items-center justify-center gap-6">
                {remotePeople.map((person) => (
                  <div key={person.userId} className="flex flex-col items-center gap-2">
                    <span className="flex h-20 w-20 items-center justify-center rounded-full border border-sky-400/30 bg-sky-500/20 text-2xl font-bold text-sky-200">
                      {initialsOf(person.displayName)}
                    </span>
                    <span className="max-w-40 truncate text-sm font-medium text-slate-200">
                      {person.displayName}
                    </span>
                  </div>
                ))}
              </div>
            ) : null}
          </div>

          <div
            className={
              camOn
                ? 'mt-3 md:absolute md:bottom-4 md:right-4 md:mt-0 md:w-52'
                : 'hidden'
            }
          >
            <video
              ref={localVideoRef}
              autoPlay
              muted
              playsInline
              className="max-h-48 w-full rounded-2xl border border-white/20 bg-slate-800 object-cover shadow-2xl md:h-32"
            />
          </div>
        </div>
      )}

      <div className="mt-3 flex justify-center">
        <div className="inline-flex flex-wrap items-center justify-center gap-2 rounded-2xl border border-white/15 bg-white/10 px-4 py-2.5 backdrop-blur-md">
          <button
            type="button"
            onClick={() => void toggleMic()}
            disabled={connectionState !== 'connected'}
            className={`inline-flex items-center gap-2 whitespace-nowrap rounded-xl px-4 py-2 text-xs font-semibold transition disabled:opacity-50 ${
              micOn
                ? 'bg-white/15 text-white hover:bg-white/20'
                : 'border border-rose-400/40 bg-rose-500/25 text-rose-200'
            }`}
          >
            {micOn ? <Mic className="h-4 w-4" /> : <MicOff className="h-4 w-4" />}
            <span>{micOn ? t('call.mic_on') : t('call.mic_off')}</span>
          </button>
          <button
            type="button"
            onClick={() => void toggleCamera()}
            disabled={connectionState !== 'connected'}
            className={`inline-flex items-center gap-2 whitespace-nowrap rounded-xl px-4 py-2 text-xs font-semibold transition disabled:opacity-50 ${
              camOn
                ? 'bg-white/15 text-white hover:bg-white/20'
                : 'border border-rose-400/40 bg-rose-500/25 text-rose-200'
            }`}
          >
            {camOn ? <Video className="h-4 w-4" /> : <VideoOff className="h-4 w-4" />}
            <span>{camOn ? t('call.camera_on') : t('call.camera_off')}</span>
          </button>
          <button
            type="button"
            onClick={() => void endCall()}
            className="inline-flex items-center gap-2 whitespace-nowrap rounded-xl bg-rose-600 px-5 py-2 text-xs font-bold text-white shadow-lg transition hover:bg-rose-500"
          >
            <PhoneOff className="h-4 w-4" />
            <span>{t('call.hangup')}</span>
          </button>
        </div>
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
