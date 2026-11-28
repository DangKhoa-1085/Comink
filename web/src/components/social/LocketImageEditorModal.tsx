import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  Camera,
  Check,
  FlipHorizontal,
  ImagePlus,
  Loader2,
  RotateCcw,
  RotateCw,
  Send,
  Sparkles,
  Users,
  X,
} from 'lucide-react'
import { friendshipsApi } from '../../api/friendships'
import { locketsApi, type LocketAudienceType } from '../../api/lockets'
import { mediaApi } from '../../api/media'
import { Avatar } from './Avatar'
import { Button } from '../ui/Button'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'
import { toast } from '../../stores/toastStore'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const EXPORT_SIZE = 1080
const PREVIEW_SIZE = 320

type FilterPresetId = 'none' | 'warm' | 'vintage' | 'bw' | 'vivid' | 'cool'

const FILTER_PRESETS: { id: FilterPresetId; css: string }[] = [
  { id: 'none', css: 'none' },
  { id: 'warm', css: 'sepia(0.28) saturate(1.22) brightness(1.04)' },
  { id: 'vintage', css: 'sepia(0.45) contrast(1.08) brightness(0.95)' },
  { id: 'bw', css: 'grayscale(1) contrast(1.12)' },
  { id: 'vivid', css: 'saturate(1.42) contrast(1.08)' },
  { id: 'cool', css: 'hue-rotate(15deg) saturate(1.1) brightness(1.02)' },
]

interface Props {
  open: boolean
  onClose: () => void
  onCreated: () => void
}

/**
 * Modal chọn ảnh hoặc chụp webcam + chỉnh sửa ảnh vuông 1:1 (kéo cắt, zoom, xoay, lật, lọc màu)
 * rồi tải lên Cloudinary và đăng Comink Locket (tự xóa sau 24 giờ).
 */
export function LocketImageEditorModal({ open, onClose, onCreated }: Props) {
  const { t } = useTranslation()

  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [loadedImage, setLoadedImage] = useState<HTMLImageElement | null>(null)
  const [cameraActive, setCameraActive] = useState(false)
  const [cameraError, setCameraError] = useState<string | null>(null)

  const [zoom, setZoom] = useState(1)
  const [rotation, setRotation] = useState(0)
  const [flippedX, setFlippedX] = useState(false)
  const [offset, setOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 })
  const [filterId, setFilterId] = useState<FilterPresetId>('none')

  const [caption, setCaption] = useState('')
  const [audienceType, setAudienceType] = useState<LocketAudienceType>('AllFriends')
  const [selectedFriends, setSelectedFriends] = useState<string[]>([])

  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const videoRef = useRef<HTMLVideoElement | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const dragRef = useRef<{ startX: number; startY: number; origX: number; origY: number } | null>(null)

  const friendsQuery = useQuery({
    queryKey: ['friendships', 'list', 'locket-picker'],
    queryFn: () => friendshipsApi.list(),
    enabled: open && audienceType === 'SelectedFriends',
    staleTime: 60_000,
  })

  const stopCameraTracks = useCallback(() => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop())
      streamRef.current = null
    }
  }, [])

  const stopCamera = useCallback(() => {
    stopCameraTracks()
    setCameraActive(false)
  }, [stopCameraTracks])

  const handleClose = useCallback(() => {
    stopCamera()
    onClose()
  }, [onClose, stopCamera])

  const resetTransforms = useCallback(() => {
    setZoom(1)
    setRotation(0)
    setFlippedX(false)
    setOffset({ x: 0, y: 0 })
    setFilterId('none')
  }, [])

  useEffect(() => {
    if (!open) {
      return
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        handleClose()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      stopCameraTracks()
    }
  }, [open, handleClose, stopCameraTracks])

  useEffect(() => {
    return () => {
      if (imageUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(imageUrl)
      }
    }
  }, [imageUrl])

  useEffect(() => {
    if (!imageUrl) {
      return
    }
    let cancelled = false
    const img = new Image()
    img.onload = () => {
      if (!cancelled) {
        setLoadedImage(img)
      }
    }
    img.src = imageUrl
    return () => {
      cancelled = true
    }
  }, [imageUrl])

  const activeImage = imageUrl ? loadedImage : null

  const drawFrame = useCallback(
    (ctx: CanvasRenderingContext2D, size: number) => {
      if (!activeImage) {
        return
      }
      const preset = FILTER_PRESETS.find((item) => item.id === filterId)?.css ?? 'none'
      ctx.save()
      ctx.clearRect(0, 0, size, size)
      ctx.fillStyle = '#080B14'
      ctx.fillRect(0, 0, size, size)
      ctx.filter = preset

      const isQuarterRotated = rotation % 180 !== 0
      const baseWidth = isQuarterRotated ? activeImage.height : activeImage.width
      const baseHeight = isQuarterRotated ? activeImage.width : activeImage.height
      const coverScale = Math.max(size / baseWidth, size / baseHeight) * zoom
      const ratio = size / PREVIEW_SIZE

      ctx.translate(size / 2 + offset.x * ratio, size / 2 + offset.y * ratio)
      ctx.rotate((rotation * Math.PI) / 180)
      ctx.scale(flippedX ? -coverScale : coverScale, coverScale)
      ctx.drawImage(activeImage, -activeImage.width / 2, -activeImage.height / 2)
      ctx.restore()
    },
    [activeImage, filterId, rotation, zoom, offset, flippedX],
  )

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas || !activeImage) {
      return
    }
    const ctx = canvas.getContext('2d')
    if (!ctx) {
      return
    }
    drawFrame(ctx, PREVIEW_SIZE)
  }, [drawFrame, activeImage])

  const openCamera = async () => {
    setCameraError(null)
    if (!navigator.mediaDevices?.getUserMedia) {
      setCameraError(t('locket.camera_error'))
      return
    }
    try {
      stopCamera()
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 1080 }, height: { ideal: 1080 } },
        audio: false,
      })
      streamRef.current = stream
      setCameraActive(true)
      requestAnimationFrame(() => {
        if (videoRef.current) {
          videoRef.current.srcObject = stream
        }
      })
    } catch {
      setCameraError(t('locket.camera_error'))
    }
  }

  const snapFromCamera = () => {
    const video = videoRef.current
    if (!video || video.videoWidth === 0) {
      return
    }
    const snapCanvas = document.createElement('canvas')
    snapCanvas.width = video.videoWidth
    snapCanvas.height = video.videoHeight
    const ctx = snapCanvas.getContext('2d')
    if (!ctx) {
      return
    }
    ctx.drawImage(video, 0, 0)
    const dataUrl = snapCanvas.toDataURL('image/jpeg', 0.92)
    stopCamera()
    resetTransforms()
    setImageUrl(dataUrl)
  }

  const handleFileChange = (files: FileList | null) => {
    const file = files?.[0]
    if (!file) {
      return
    }
    if (!ALLOWED_TYPES.includes(file.type)) {
      toast.error(t('social.bad_type'))
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      toast.error(t('social.too_large_image'))
      return
    }
    stopCamera()
    resetTransforms()
    setCameraError(null)
    setImageUrl(URL.createObjectURL(file))
  }

  const publishMutation = useMutation({
    mutationFn: async () => {
      if (!activeImage) {
        throw new Error('NO_IMAGE')
      }
      if (audienceType === 'SelectedFriends' && selectedFriends.length === 0) {
        throw new Error('NO_RECIPIENTS')
      }

      const exportCanvas = document.createElement('canvas')
      exportCanvas.width = EXPORT_SIZE
      exportCanvas.height = EXPORT_SIZE
      const ctx = exportCanvas.getContext('2d')
      if (!ctx) {
        throw new Error('CANVAS_ERROR')
      }
      drawFrame(ctx, EXPORT_SIZE)

      const blob = await new Promise<Blob>((resolve, reject) => {
        exportCanvas.toBlob(
          (result) => (result ? resolve(result) : reject(new Error('BLOB_FAILED'))),
          'image/jpeg',
          0.9,
        )
      })
      const editedFile = new File([blob], `locket-${Date.now()}.jpg`, { type: 'image/jpeg' })

      const signature = await mediaApi.getSignature('Locket')
      const uploaded = await mediaApi.upload(signature, editedFile)
      const asset = await mediaApi.register({
        providerPublicId: uploaded.providerPublicId,
        url: uploaded.url,
        thumbnailUrl: uploaded.url,
        mimeType: uploaded.mimeType,
        sizeBytes: uploaded.sizeBytes,
        width: EXPORT_SIZE,
        height: EXPORT_SIZE,
        context: 'Locket',
      })

      return locketsApi.create({
        mediaAssetId: asset.id,
        caption: caption.trim() || null,
        audienceType,
        recipientIds: audienceType === 'SelectedFriends' ? selectedFriends : undefined,
      })
    },
    onSuccess: () => {
      toast.success(t('locket.posted'))
      setCaption('')
      setSelectedFriends([])
      setImageUrl(null)
      setLoadedImage(null)
      resetTransforms()
      onCreated()
      handleClose()
    },
    onError: (error) => {
      if (error instanceof Error && error.message === 'NO_RECIPIENTS') {
        toast.error(t('locket.pick_at_least_one'))
        return
      }
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    },
  })

  if (!open) {
    return null
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-3 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
    >
      <div className="flex max-h-[92vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-[#121826]">
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <div>
            <h2 className="text-sm font-bold text-slate-900 dark:text-slate-100">
              {t('locket.editor_title')}
            </h2>
            <p className="text-[11px] text-slate-500 dark:text-slate-400">
              {t('locket.subtitle_24h')}
            </p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
            aria-label={t('common.cancel')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="flex-1 space-y-3 overflow-y-auto p-4">
          {/* Nút chọn ảnh hoặc chụp webcam */}
          <div className="flex flex-wrap items-center gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={(event) => {
                handleFileChange(event.target.files)
                event.target.value = ''
              }}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
            >
              <ImagePlus className="h-4 w-4" />
              {t('locket.pick_photo')}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => void openCamera()}>
              <Camera className="h-4 w-4" />
              {cameraActive ? t('locket.retake_photo') : t('locket.open_camera')}
            </Button>
          </div>

          {cameraError && (
            <p className="rounded-xl bg-rose-50 px-3 py-2 text-xs text-rose-600 dark:bg-rose-500/10 dark:text-rose-300">
              {cameraError}
            </p>
          )}

          {/* Khung xem webcam hoặc khung cắt vuông 1:1 */}
          <div className="mx-auto w-full max-w-[320px]">
            {cameraActive ? (
              <div className="relative aspect-square w-full overflow-hidden rounded-2xl border border-slate-700 bg-black">
                <video
                  ref={videoRef}
                  autoPlay
                  playsInline
                  muted
                  className="h-full w-full scale-x-[-1] object-cover"
                />
                <div className="absolute inset-x-0 bottom-3 flex justify-center">
                  <button
                    type="button"
                    onClick={snapFromCamera}
                    className="inline-flex items-center gap-1.5 rounded-full bg-white px-4 py-2 text-xs font-bold text-slate-900 shadow-lg hover:bg-sky-50"
                  >
                    <Camera className="h-4 w-4 text-sky-600" />
                    {t('locket.snap_now')}
                  </button>
                </div>
              </div>
            ) : activeImage ? (
              <div className="space-y-2">
                <div className="relative aspect-square w-full touch-none select-none overflow-hidden rounded-2xl border border-slate-300 bg-[#080B14] dark:border-slate-700">
                  <canvas
                    ref={canvasRef}
                    width={PREVIEW_SIZE}
                    height={PREVIEW_SIZE}
                    className="h-full w-full cursor-grab active:cursor-grabbing"
                    onPointerDown={(event) => {
                      event.currentTarget.setPointerCapture(event.pointerId)
                      dragRef.current = {
                        startX: event.clientX,
                        startY: event.clientY,
                        origX: offset.x,
                        origY: offset.y,
                      }
                    }}
                    onPointerMove={(event) => {
                      if (!dragRef.current) {
                        return
                      }
                      const dx = event.clientX - dragRef.current.startX
                      const dy = event.clientY - dragRef.current.startY
                      setOffset({
                        x: dragRef.current.origX + dx,
                        y: dragRef.current.origY + dy,
                      })
                    }}
                    onPointerUp={() => {
                      dragRef.current = null
                    }}
                  />
                  {/* Lưới chia 3×3 giúp người dùng căn khung vuông dễ dàng */}
                  <div className="pointer-events-none absolute inset-0 grid grid-cols-3 grid-rows-3 opacity-30">
                    {Array.from({ length: 9 }).map((_, idx) => (
                      <div key={idx} className="border border-white/25" />
                    ))}
                  </div>
                </div>
                <p className="text-center text-[11px] text-slate-400">
                  {t('locket.drag_to_crop')}
                </p>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                className="flex aspect-square w-full flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-slate-300 bg-slate-50 p-6 text-center transition hover:border-sky-400 dark:border-slate-700 dark:bg-[#1A2234]"
              >
                <span className="flex h-12 w-12 items-center justify-center rounded-full bg-sky-500/15 text-sky-500">
                  <ImagePlus className="h-6 w-6" />
                </span>
                <span className="text-xs font-medium text-slate-600 dark:text-slate-300">
                  {t('locket.editor_empty_hint')}
                </span>
              </button>
            )}
          </div>

          {/* Bộ công cụ chỉnh ảnh: zoom, xoay, lật, reset và bộ lọc màu */}
          {activeImage && !cameraActive && (
            <div className="space-y-2.5 rounded-xl border border-slate-200 bg-slate-50/70 p-3 dark:border-slate-800 dark:bg-[#1A2234]">
              <div className="flex items-center gap-2">
                <span className="w-16 text-[11px] font-medium text-slate-600 dark:text-slate-300">
                  {t('locket.zoom')} ({zoom.toFixed(1)}x)
                </span>
                <input
                  type="range"
                  min={1}
                  max={3}
                  step={0.05}
                  value={zoom}
                  onChange={(event) => setZoom(Number(event.target.value))}
                  className="h-1.5 flex-1 accent-sky-500"
                />
              </div>

              <div className="flex flex-wrap items-center gap-1.5">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setRotation((value) => (value + 90) % 360)}
                >
                  <RotateCw className="h-3.5 w-3.5" />
                  {t('locket.rotate')}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setFlippedX((value) => !value)}
                >
                  <FlipHorizontal className="h-3.5 w-3.5" />
                  {t('locket.flip')}
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={resetTransforms}>
                  <RotateCcw className="h-3.5 w-3.5" />
                  {t('locket.reset')}
                </Button>
              </div>

              <div>
                <p className="mb-1.5 flex items-center gap-1 text-[11px] font-semibold text-slate-600 dark:text-slate-300">
                  <Sparkles className="h-3 w-3 text-amber-500" />
                  {t('locket.filter_label')}
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {FILTER_PRESETS.map((preset) => (
                    <button
                      key={preset.id}
                      type="button"
                      onClick={() => setFilterId(preset.id)}
                      className={`rounded-full px-2.5 py-1 text-[11px] font-semibold transition ${
                        filterId === preset.id
                          ? 'bg-sky-600 text-white'
                          : 'border border-slate-200 bg-white text-slate-600 hover:border-sky-400 dark:border-slate-700 dark:bg-[#121826] dark:text-slate-300'
                      }`}
                    >
                      {t(`locket.filter_${preset.id}`)}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* Chú thích ngắn + Phạm vi người nhận */}
          <div className="space-y-2">
            <div>
              <input
                type="text"
                maxLength={100}
                value={caption}
                onChange={(event) => setCaption(event.target.value)}
                placeholder={t('locket.caption_placeholder')}
                className="h-9 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 text-xs outline-none focus:border-sky-400 dark:border-slate-700 dark:bg-[#1A2234] dark:text-slate-100"
              />
              <div className="mt-1 text-right text-[11px] text-slate-400 tabular-nums">
                {caption.length}/100
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center gap-2 text-xs">
                <span className="font-semibold text-slate-600 dark:text-slate-300">
                  {t('locket.audience_label')}:
                </span>
                <button
                  type="button"
                  onClick={() => setAudienceType('AllFriends')}
                  className={`rounded-full px-2.5 py-1 font-medium transition ${
                    audienceType === 'AllFriends'
                      ? 'bg-sky-600 text-white'
                      : 'border border-slate-200 text-slate-600 dark:border-slate-700 dark:text-slate-300'
                  }`}
                >
                  {t('locket.audience_all')}
                </button>
                <button
                  type="button"
                  onClick={() => setAudienceType('SelectedFriends')}
                  className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 font-medium transition ${
                    audienceType === 'SelectedFriends'
                      ? 'bg-sky-600 text-white'
                      : 'border border-slate-200 text-slate-600 dark:border-slate-700 dark:text-slate-300'
                  }`}
                >
                  <Users className="h-3 w-3" />
                  {t('locket.audience_selected')}
                </button>
              </div>

              {audienceType === 'SelectedFriends' && (
                <div className="max-h-32 space-y-1 overflow-y-auto rounded-xl border border-slate-200 p-2 dark:border-slate-700">
                  {friendsQuery.isLoading ? (
                    <div className="flex items-center gap-2 py-2 text-xs text-slate-400">
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      {t('social.loading')}
                    </div>
                  ) : (friendsQuery.data?.items.length ?? 0) === 0 ? (
                    <p className="py-1 text-xs text-slate-400">{t('locket.no_friends_available')}</p>
                  ) : (
                    friendsQuery.data?.items.map((friendship) => {
                      const friend = friendship.user
                      const checked = selectedFriends.includes(friend.id)
                      return (
                        <button
                          key={friend.id}
                          type="button"
                          onClick={() =>
                            setSelectedFriends((current) =>
                              checked
                                ? current.filter((id) => id !== friend.id)
                                : [...current, friend.id],
                            )
                          }
                          className="flex w-full items-center gap-2 rounded-lg px-2 py-1 text-left text-xs hover:bg-slate-100 dark:hover:bg-slate-800"
                        >
                          <Avatar name={friend.displayName} url={friend.avatarUrl} size="sm" />
                          <span className="min-w-0 flex-1 truncate font-medium text-slate-800 dark:text-slate-100">
                            {friend.displayName}
                          </span>
                          <span
                            className={`flex h-4 w-4 items-center justify-center rounded border ${
                              checked
                                ? 'border-sky-600 bg-sky-600 text-white'
                                : 'border-slate-300 dark:border-slate-600'
                            }`}
                          >
                            {checked && <Check className="h-3 w-3" />}
                          </span>
                        </button>
                      )
                    })
                  )}
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center justify-end gap-2 border-t border-slate-200 px-4 py-3 dark:border-slate-800">
          <Button type="button" variant="ghost" size="sm" onClick={handleClose}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            size="sm"
            disabled={!activeImage || publishMutation.isPending}
            onClick={() => publishMutation.mutate()}
          >
            {publishMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Send className="h-4 w-4" />
            )}
            {publishMutation.isPending ? t('locket.publishing') : t('locket.publish')}
          </Button>
        </div>
      </div>
    </div>
  )
}
