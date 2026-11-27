import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Download, QrCode, Smartphone, X } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import { APK_DOWNLOAD_URL } from '../../config/env'
import { cn } from '../../lib/utils'
import { toast } from '../../stores/toastStore'

interface ApkDownloadBannerProps {
  /** Thu gọn thành nút icon vuông khi sidebar chuyển sang Icon Rail (`76px`) ở `/messages`. */
  compact?: boolean
}

/**
 * Banner tải app Android (.apk) theo Kiểu 1:
 * thẻ Ocean Blue Gradient nổi bật + nút tải trực tiếp + nút mở popup mã QR.
 */
export function ApkDownloadBanner({ compact = false }: ApkDownloadBannerProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement | null>(null)
  const hasDownloadUrl = APK_DOWNLOAD_URL.length > 0

  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('touchstart', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('touchstart', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const notifyNotConfigured = () => {
    toast.info(t('apk.not_configured'))
  }

  return (
    <div ref={rootRef} className="relative">
      {compact ? (
        <button
          type="button"
          onClick={() => setOpen((prev) => !prev)}
          title={t('apk.compact_label')}
          aria-label={t('apk.compact_label')}
          aria-expanded={open}
          className="mx-auto flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-sky-600 via-sky-500 to-cyan-500 text-white shadow-md transition-transform hover:scale-105"
        >
          <QrCode className="h-4 w-4" />
        </button>
      ) : (
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-sky-600 via-sky-500 to-cyan-500 p-3 text-white shadow-md">
          {/* Điểm nhấn ánh sáng nhẹ ở góc trên phải */}
          <div className="pointer-events-none absolute -right-4 -top-4 h-16 w-16 rounded-full bg-white/15" />

          <div className="relative flex items-start justify-between gap-2">
            <div className="flex min-w-0 items-center gap-2">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-white/20">
                <Smartphone className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <p className="truncate text-xs font-bold leading-tight">{t('apk.title')}</p>
                <p className="truncate text-[11px] leading-tight text-white/90">
                  {t('apk.subtitle')}
                </p>
              </div>
            </div>
            <span className="shrink-0 rounded-full bg-white/20 px-1.5 py-0.5 text-[10px] font-bold tracking-wide">
              {t('apk.badge')}
            </span>
          </div>

          <div className="relative mt-2.5 flex items-center gap-1.5">
            {hasDownloadUrl ? (
              <a
                href={APK_DOWNLOAD_URL}
                target="_blank"
                rel="noopener noreferrer"
                download
                className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-white py-1.5 text-xs font-bold text-sky-700 shadow-sm transition-colors hover:bg-sky-50"
              >
                <Download className="h-3.5 w-3.5 shrink-0" />
                <span>{t('apk.download_cta')}</span>
              </a>
            ) : (
              <button
                type="button"
                onClick={notifyNotConfigured}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-white py-1.5 text-xs font-bold text-sky-700 shadow-sm transition-colors hover:bg-sky-50"
              >
                <Download className="h-3.5 w-3.5 shrink-0" />
                <span>{t('apk.download_cta')}</span>
              </button>
            )}

            <button
              type="button"
              onClick={() => setOpen((prev) => !prev)}
              title={t('apk.qr_button_label')}
              aria-label={t('apk.qr_button_label')}
              aria-expanded={open}
              className={cn(
                'flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border border-white/30 text-white transition-colors',
                open ? 'bg-white/30' : 'bg-white/20 hover:bg-white/30',
              )}
            >
              <QrCode className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}

      {open && (
        <div
          role="dialog"
          aria-label={t('apk.qr_title')}
          className={cn(
            'absolute z-50 w-60 rounded-2xl border border-slate-200 bg-white p-3.5 text-xs text-slate-900 shadow-2xl',
            'dark:border-slate-800 dark:bg-[#121826] dark:text-slate-100',
            compact ? 'bottom-0 left-full ml-3' : 'bottom-full left-0 mb-2',
          )}
        >
          <div className="mb-2.5 flex items-start justify-between gap-2">
            <div>
              <p className="text-xs font-bold leading-tight">{t('apk.qr_title')}</p>
              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                {t('apk.qr_subtitle')}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t('common.cancel')}
              className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>

          {hasDownloadUrl ? (
            <>
              <div className="mx-auto flex w-fit items-center justify-center rounded-xl border border-slate-200 bg-white p-2.5 shadow-inner">
                <QRCodeSVG
                  value={APK_DOWNLOAD_URL}
                  size={148}
                  level="M"
                  fgColor="#0F172A"
                  bgColor="#FFFFFF"
                />
              </div>
              <p className="mt-2.5 text-center text-[11px] text-slate-500 dark:text-slate-400">
                {t('apk.qr_hint')}
              </p>
              <a
                href={APK_DOWNLOAD_URL}
                target="_blank"
                rel="noopener noreferrer"
                download
                onClick={() => setOpen(false)}
                className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-sky-600 to-sky-500 py-1.5 text-xs font-bold text-white transition-opacity hover:opacity-95"
              >
                <Download className="h-3.5 w-3.5 shrink-0" />
                <span>{t('apk.download_direct')}</span>
              </a>
            </>
          ) : (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-center text-[11px] text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
              {t('apk.qr_not_configured')}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
