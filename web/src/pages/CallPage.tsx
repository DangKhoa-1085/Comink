import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Mic, MicOff, PhoneOff, RefreshCw, Users, Video, VideoOff } from 'lucide-react'
import {
  RoomEvent,
  Track,
  type Participant,
  type RemoteTrack,
  type Room,
} from 'livekit-client'
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

interface ParticipantSnapshot {
  userId: string
  videoTrack: Track | null
  isMicMuted: boolean
  isSpeaking: boolean
}

function getActiveCameraTrack(participant: Participant): Track | null {
  for (const publication of participant.videoTrackPublications.values()) {
    if (
      publication.track &&
      publication.track.kind === Track.Kind.Video &&
      !publication.isMuted
    ) {
      return publication.track
    }
  }
  return null
}

/** Màn hình gọi: kết nối room LiveKit, chia lưới người tham gia vừa khung hình, ghim thanh điều khiển ở đáy. */
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
  const [remoteSnapshots, setRemoteSnapshots] = useState<ParticipantSnapshot[]>([])
  const [localVideoTrack, setLocalVideoTrack] = useState<Track | null>(null)
  const [localSpeaking, setLocalSpeaking] = useState(false)

  const roomRef = useRef<Room | null>(null)
  const remoteAudioRef = useRef<HTMLDivElement | null>(null)

  // Poll nhẹ để biết cuộc gọi đã kết thúc (webhook hoặc phía bên kia bấm kết thúc).
  const callQuery = useQuery({
    queryKey: ['call', callId],
    queryFn: () => callsApi.get(callId!),
    enabled: Boolean(callId),
    refetchInterval: 3000,
  })

  const isTerminal = callQuery.data ? TERMINAL_STATUSES.includes(callQuery.data.status) : false

  const remoteTiles = useMemo(
    () =>
      remoteSnapshots.map((snapshot) => ({
        ...snapshot,
        displayName:
          callQuery.data?.participants.find((participant) => participant.userId === snapshot.userId)
            ?.displayName ?? t('call.unknown_caller'),
      })),
    [callQuery.data?.participants, remoteSnapshots, t],
  )

  const isWaiting = remoteTiles.length === 0
  const myDisplayName =
    me?.displayName ||
    callQuery.data?.participants.find((participant) => participant.userId === me?.id)?.displayName ||
    t('call.you')

  // Kết nối room một lần cho mỗi cuộc gọi.
  useEffect(() => {
    if (!callId || !me?.id) {
      return
    }

    let disposed = false
    const room = createCallRoom()
    roomRef.current = room
    const audioContainer = remoteAudioRef.current

    const syncRoomParticipants = () => {
      if (disposed) {
        return
      }
      const nextRemotes: ParticipantSnapshot[] = [...room.remoteParticipants.values()].map(
        (participant) => ({
          userId: participant.identity,
          videoTrack: getActiveCameraTrack(participant),
          isMicMuted: !participant.isMicrophoneEnabled,
          isSpeaking: participant.isSpeaking,
        }),
      )
      setRemoteSnapshots(nextRemotes)
      setLocalVideoTrack(getActiveCameraTrack(room.localParticipant))
      setLocalSpeaking(room.localParticipant.isSpeaking)
    }

    const handleTrackSubscribed = (track: RemoteTrack) => {
      if (disposed) {
        return
      }
      if (track.kind === Track.Kind.Audio) {
        const element = track.attach()
        audioContainer?.appendChild(element)
      }
      syncRoomParticipants()
    }

    const handleTrackUnsubscribed = (track: RemoteTrack) => {
      if (track.kind === Track.Kind.Audio) {
        track.detach().forEach((element) => element.remove())
      }
      syncRoomParticipants()
    }

    room.on(RoomEvent.TrackSubscribed, handleTrackSubscribed)
    room.on(RoomEvent.TrackUnsubscribed, handleTrackUnsubscribed)
    room.on(RoomEvent.TrackMuted, syncRoomParticipants)
    room.on(RoomEvent.TrackUnmuted, syncRoomParticipants)
    room.on(RoomEvent.LocalTrackPublished, syncRoomParticipants)
    room.on(RoomEvent.LocalTrackUnpublished, syncRoomParticipants)
    room.on(RoomEvent.ParticipantConnected, syncRoomParticipants)
    room.on(RoomEvent.ParticipantDisconnected, syncRoomParticipants)
    room.on(RoomEvent.ActiveSpeakersChanged, syncRoomParticipants)

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

        await room.localParticipant.setMicrophoneEnabled(true)
        if (details.type === 'Video') {
          await room.localParticipant.setCameraEnabled(true)
        } else {
          setCamOn(false)
        }

        syncRoomParticipants()
        setConnectionState('connected')
      } catch (error) {
        // Lỗi kết nối LiveKit không phải ApiError → giữ mã dịch + chi tiết kỹ thuật để chẩn đoán.
        const message = error instanceof Error ? error.message : String(error)
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
      room.off(RoomEvent.TrackSubscribed, handleTrackSubscribed)
      room.off(RoomEvent.TrackUnsubscribed, handleTrackUnsubscribed)
      room.off(RoomEvent.TrackMuted, syncRoomParticipants)
      room.off(RoomEvent.TrackUnmuted, syncRoomParticipants)
      room.off(RoomEvent.LocalTrackPublished, syncRoomParticipants)
      room.off(RoomEvent.LocalTrackUnpublished, syncRoomParticipants)
      room.off(RoomEvent.ParticipantConnected, syncRoomParticipants)
      room.off(RoomEvent.ParticipantDisconnected, syncRoomParticipants)
      room.off(RoomEvent.ActiveSpeakersChanged, syncRoomParticipants)
      if (audioContainer) {
        audioContainer.replaceChildren()
      }
      void room.disconnect()
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
    setLocalVideoTrack(next ? getActiveCameraTrack(room.localParticipant) : null)
  }

  if (callQuery.isLoading) {
    return (
      <div className="flex h-full items-center justify-center gap-2 text-sm text-slate-300">
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

  // Camera/micro chỉ chạy trên secure context (HTTPS hoặc localhost).
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
      <div className="flex h-full items-center justify-center p-4">
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

  const totalCount = 1 + remoteTiles.length
  const isOneToOneVideo = remoteTiles.length === 1 && callQuery.data.type === 'Video'

  // Tất cả thành viên khi gọi nhóm (>= 3 người) hoặc gọi thoại (2+ người) đều nằm trong cùng grid.
  const gridParticipants = [
    ...remoteTiles.map((person) => ({
      key: person.userId,
      displayName: person.displayName,
      isLocal: false,
      videoTrack: person.videoTrack,
      isMicMuted: person.isMicMuted,
      isSpeaking: person.isSpeaking,
    })),
    {
      key: me?.id ?? 'local-self',
      displayName: myDisplayName,
      isLocal: true,
      videoTrack: camOn ? localVideoTrack : null,
      isMicMuted: !micOn,
      isSpeaking: localSpeaking,
    },
  ]

  const gridClassName = resolveGridClassName(gridParticipants.length)

  return (
    <div className="mx-auto flex h-full w-full max-w-6xl flex-col overflow-hidden p-3 sm:p-5">
      {/* Thẻ <audio> của remote gắn ẩn để không phá vỡ bố cục lưới video/avatar. */}
      <div ref={remoteAudioRef} className="hidden" aria-hidden="true" />

      <div className="shrink-0 flex flex-wrap items-center justify-between gap-2 pb-2">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-base font-bold text-white sm:text-lg">
              {callQuery.data.type === 'Video' ? t('call.video_title') : t('call.audio_title')}
            </h1>
            <span className="rounded-full bg-emerald-500/20 px-2.5 py-0.5 text-xs font-semibold text-emerald-300">
              {connectionState === 'connecting' ? t('call.connecting') : t('call.connected')}
            </span>
            <span className="inline-flex items-center gap-1 rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-200">
              <Users className="h-3.5 w-3.5 text-sky-300" />
              {t('call.participants_count', { count: totalCount })}
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
        <div className="my-auto flex-1 overflow-y-auto py-4">
          <Alert variant="error">
            <p>{errorCode ? translateErrorCode(t, errorCode) : t('call.error')}</p>
            {errorDetail && <p className="mt-1 text-xs opacity-80">{errorDetail}</p>}
          </Alert>
          <Button
            variant="outline"
            className="mt-3"
            onClick={() => {
              setConnectionState('connecting')
              setErrorCode(null)
              setErrorDetail(null)
              setRemoteSnapshots([])
              setLocalVideoTrack(null)
              setConnectionAttempt((value) => value + 1)
            }}
          >
            <RefreshCw className="h-4 w-4" />
            {t('call.retry')}
          </Button>
        </div>
      ) : (
        <div className="relative my-1 min-h-0 flex-1 overflow-hidden sm:my-2">
          {isWaiting ? (
            <div className="relative h-full w-full">
              <ParticipantTile
                displayName={myDisplayName}
                isLocal
                videoTrack={camOn ? localVideoTrack : null}
                isMicMuted={!micOn}
                isSpeaking={localSpeaking}
              />
              <div className="pointer-events-none absolute inset-x-0 bottom-4 flex justify-center px-3">
                <div className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-slate-950/75 px-4 py-1.5 text-xs text-slate-200 backdrop-blur-md">
                  {connectionState === 'connecting' && <Spinner className="h-3.5 w-3.5 text-sky-400" />}
                  <span>
                    {connectionState === 'connecting' ? t('call.connecting') : t('call.remote_hint')}
                  </span>
                </div>
              </div>
            </div>
          ) : isOneToOneVideo ? (
            <div className="relative h-full w-full">
              <ParticipantTile
                displayName={remoteTiles[0].displayName}
                isLocal={false}
                videoTrack={remoteTiles[0].videoTrack}
                isMicMuted={remoteTiles[0].isMicMuted}
                isSpeaking={remoteTiles[0].isSpeaking}
              />
              {camOn && (
                <div className="absolute bottom-3 right-3 z-10 h-28 w-40 shadow-2xl sm:h-36 sm:w-52">
                  <ParticipantTile
                    displayName={myDisplayName}
                    isLocal
                    videoTrack={localVideoTrack}
                    isMicMuted={!micOn}
                    isSpeaking={localSpeaking}
                    compact
                  />
                </div>
              )}
            </div>
          ) : (
            <div className={`grid h-full w-full min-h-0 gap-2 sm:gap-3 ${gridClassName}`}>
              {gridParticipants.map((person, index) => {
                // Nhóm 3 người: ô thứ 3 nằm cân giữa hàng dưới (2 cột) để không thừa khoảng lệch.
                const threeMemberLastTile =
                  gridParticipants.length === 3 && index === 2
                    ? 'col-span-2 mx-auto w-full sm:w-1/2'
                    : ''
                return (
                  <ParticipantTile
                    key={person.key}
                    displayName={person.displayName}
                    isLocal={person.isLocal}
                    videoTrack={person.videoTrack}
                    isMicMuted={person.isMicMuted}
                    isSpeaking={person.isSpeaking}
                    compact={gridParticipants.length >= 5}
                    className={threeMemberLastTile}
                  />
                )
              })}
            </div>
          )}
        </div>
      )}

      <div className="shrink-0 flex justify-center pt-2">
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

function resolveGridClassName(count: number): string {
  if (count <= 2) {
    return 'grid-cols-1 grid-rows-2 sm:grid-cols-2 sm:grid-rows-1'
  }
  if (count <= 4) {
    return 'grid-cols-2 grid-rows-2'
  }
  if (count <= 6) {
    return 'grid-cols-2 grid-rows-3 sm:grid-cols-3 sm:grid-rows-2'
  }
  if (count <= 9) {
    return 'grid-cols-2 grid-rows-4 sm:grid-cols-3 sm:grid-rows-3'
  }
  return 'grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 auto-rows-[minmax(140px,1fr)] overflow-y-auto pr-1'
}

interface ParticipantTileProps {
  displayName: string
  isLocal: boolean
  videoTrack: Track | null
  isMicMuted: boolean
  isSpeaking: boolean
  compact?: boolean
  className?: string
}

/** Một ô thành viên trong phòng gọi: gắn track video khi mở camera hoặc hiện avatar khi tắt camera. */
function ParticipantTile({
  displayName,
  isLocal,
  videoTrack,
  isMicMuted,
  isSpeaking,
  compact = false,
  className = '',
}: ParticipantTileProps) {
  const { t } = useTranslation()
  const videoRef = useRef<HTMLVideoElement | null>(null)

  useEffect(() => {
    const element = videoRef.current
    if (!element || !videoTrack) {
      return
    }
    videoTrack.attach(element)
    return () => {
      videoTrack.detach(element)
    }
  }, [videoTrack])

  return (
    <div
      className={`relative flex h-full w-full min-h-0 items-center justify-center overflow-hidden rounded-2xl border bg-[#121826] transition ${
        isSpeaking
          ? 'border-sky-400 ring-2 ring-sky-400/50'
          : 'border-white/10'
      } ${className}`}
    >
      {videoTrack ? (
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted={isLocal}
          className={`h-full w-full object-cover ${isLocal ? 'scale-x-[-1]' : ''}`}
        />
      ) : (
        <div className="flex flex-col items-center justify-center gap-2 p-3">
          <span
            className={`flex items-center justify-center rounded-full border border-sky-400/30 bg-sky-500/20 font-bold text-sky-200 ${
              compact ? 'h-14 w-14 text-lg' : 'h-20 w-20 text-2xl'
            }`}
          >
            {initialsOf(displayName)}
          </span>
        </div>
      )}

      <div className="absolute bottom-2 left-2 flex max-w-[calc(100%-1rem)] items-center gap-1.5 rounded-lg bg-slate-950/70 px-2.5 py-1 text-xs font-medium text-white backdrop-blur-sm">
        <span className="truncate">
          {isLocal && displayName !== t('call.you')
            ? `${displayName} (${t('call.you')})`
            : displayName}
        </span>
        {isMicMuted && <MicOff className="h-3.5 w-3.5 shrink-0 text-rose-400" />}
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
