import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImagePlus, Loader2, Send, X } from 'lucide-react'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Visibility } from '../../api/types'
import { Button } from '../ui/Button'
import { Avatar } from './Avatar'
import { useAuthStore } from '../../stores/authStore'
import { toast } from '../../stores/toastStore'
import { ApiError } from '../../lib/http'
import { translateErrorCode } from '../../lib/errorMessages'

const MAX_IMAGES = 10
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

interface UploadedImage {
  id: string
  url: string
}

/** Hộp soạn bài: nội dung, phạm vi hiển thị và ảnh (tải lên cloud/đĩa trước khi đăng). */
export function PostComposer({ onPosted }: { onPosted: () => void }) {
  const { t } = useTranslation()
  const user = useAuthStore((state) => state.user)

  const [content, setContent] = useState('')
  const [visibility, setVisibility] = useState<Visibility>('Friends')
  const [images, setImages] = useState<UploadedImage[]>([])
  const [uploading, setUploading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  if (!user) {
    return null
  }

  const handleFiles = async (files: FileList | null) => {
    if (!files || files.length === 0) {
      return
    }

    const room = MAX_IMAGES - images.length
    const selected = Array.from(files).slice(0, Math.max(room, 0))

    if (files.length > room) {
      toast.error(t('social.too_many_images', { max: MAX_IMAGES }))
    }

    setUploading(true)
    try {
      for (const file of selected) {
        if (!ALLOWED_TYPES.includes(file.type)) {
          toast.error(t('social.bad_type'))
          continue
        }
        if (file.size > MAX_IMAGE_BYTES) {
          toast.error(t('social.too_large_image'))
          continue
        }

        const signature = await mediaApi.getSignature('Post')
        const uploaded = await mediaApi.upload(signature, file)
        const asset = await mediaApi.register({
          providerPublicId: uploaded.providerPublicId,
          url: uploaded.url,
          thumbnailUrl: uploaded.url,
          mimeType: uploaded.mimeType,
          sizeBytes: uploaded.sizeBytes,
          context: 'Post',
        })

        setImages((current) => [...current, { id: asset.id, url: asset.url }])
      }
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    } finally {
      setUploading(false)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }
    }
  }

  const submit = async () => {
    const trimmed = content.trim()
    if (trimmed.length === 0 && images.length === 0) {
      toast.error(t('social.content_required'))
      return
    }

    setSubmitting(true)
    try {
      await postsApi.create({
        content: trimmed.length > 0 ? trimmed : null,
        visibility,
        mediaIds: images.map((image) => image.id),
      })
      setContent('')
      setImages([])
      setVisibility('Friends')
      toast.success(t('social.posted'))
      onPosted()
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
      toast.error(translateErrorCode(t, code))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex gap-3">
        <Avatar name={user.displayName} size="md" />
        <div className="min-w-0 flex-1">
          <textarea
            value={content}
            onChange={(event) => setContent(event.target.value)}
            placeholder={t('social.composer_placeholder')}
            rows={3}
            maxLength={5000}
            className="w-full resize-none rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm outline-none focus:border-indigo-400 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
          />

          {images.length > 0 && (
            <div className="mt-2 grid grid-cols-3 gap-2 sm:grid-cols-4">
              {images.map((image) => (
                <div key={image.id} className="relative">
                  <img
                    src={image.url}
                    alt=""
                    className="h-20 w-full rounded-lg object-cover"
                  />
                  <button
                    type="button"
                    onClick={() => setImages((current) => current.filter((item) => item.id !== image.id))}
                    className="absolute -right-1 -top-1 rounded-full bg-slate-900/80 p-1 text-white hover:bg-rose-600"
                    aria-label={t('social.remove_image')}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          )}

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept={ALLOWED_TYPES.join(',')}
              multiple
              hidden
              onChange={(event) => void handleFiles(event.target.files)}
            />
            <Button
              variant="outline"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
              disabled={uploading || submitting || images.length >= MAX_IMAGES}
            >
              {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ImagePlus className="h-4 w-4" />}
              {t('social.add_images')}
            </Button>

            <select
              value={visibility}
              onChange={(event) => setVisibility(event.target.value as Visibility)}
              className="h-8 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
              aria-label={t('social.visibility')}
            >
              <option value="Public">{t('social.visibility_public')}</option>
              <option value="Friends">{t('social.visibility_friends')}</option>
              <option value="OnlyMe">{t('social.visibility_onlyme')}</option>
            </select>

            <span className="ml-auto text-xs text-slate-400">{content.length}/5000</span>

            <Button size="sm" onClick={() => void submit()} disabled={submitting || uploading}>
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              {submitting ? t('social.composer_posting') : t('social.composer_submit')}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
