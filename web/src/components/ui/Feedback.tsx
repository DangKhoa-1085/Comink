import type { HTMLAttributes, LabelHTMLAttributes, ReactNode } from 'react'
import { Loader2, AlertCircle, CheckCircle2, Info } from 'lucide-react'
import { cn } from '../../lib/utils'

/** Thẻ bao nội dung. */
export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-slate-200 bg-white p-6 shadow-sm',
        'dark:border-slate-800 dark:bg-slate-900',
        className,
      )}
      {...props}
    />
  )
}

/** Nhãn của trường nhập liệu. */
export function Label({ className, ...props }: LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label
      className={cn('mb-1.5 block text-sm font-medium text-slate-700 dark:text-slate-300', className)}
      {...props}
    />
  )
}

/** Dòng thông báo lỗi nhỏ dưới một trường. */
export function FieldError({ children }: { children?: ReactNode }) {
  if (!children) {
    return null
  }
  return <p className="mt-1 text-xs text-rose-600 dark:text-rose-400">{children}</p>
}

type AlertVariant = 'error' | 'success' | 'info'

const alertStyles: Record<AlertVariant, { container: string; icon: ReactNode }> = {
  error: {
    container:
      'border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-900 dark:bg-rose-950/50 dark:text-rose-300',
    icon: <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />,
  },
  success: {
    container:
      'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-300',
    icon: <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />,
  },
  info: {
    container:
      'border-sky-200 bg-sky-50 text-sky-800 dark:border-sky-900 dark:bg-sky-950/50 dark:text-sky-300',
    icon: <Info className="mt-0.5 h-4 w-4 shrink-0" />,
  },
}

/** Khối thông báo (lỗi / thành công / thông tin). */
export function Alert({ variant = 'info', children }: { variant?: AlertVariant; children: ReactNode }) {
  const style = alertStyles[variant]
  return (
    <div className={cn('flex items-start gap-2 rounded-lg border p-3 text-sm', style.container)}>
      {style.icon}
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  )
}

/** Vòng xoay báo đang tải. */
export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('h-4 w-4 animate-spin', className)} />
}

/** Trạng thái đang tải toàn màn hình (dùng khi khôi phục phiên). */
export function FullPageSpinner({ label }: { label: string }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 text-slate-500">
      <Spinner className="h-6 w-6" />
      <span className="text-sm">{label}</span>
    </div>
  )
}
