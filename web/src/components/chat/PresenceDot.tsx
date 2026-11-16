/**
 * Chấm trạng thái online (NC GĐ 7). Màu + nhãn đều có ý nghĩa nên phải có `label` cho trình đọc màn hình.
 */
interface PresenceDotProps {
  online: boolean
  label: string
  className?: string
}

export function PresenceDot({ online, label, className = '' }: PresenceDotProps) {
  return (
    <span
      role="img"
      aria-label={label}
      title={label}
      className={`inline-block h-2.5 w-2.5 shrink-0 rounded-full ring-2 ring-white dark:ring-slate-900 ${
        online ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600'
      } ${className}`}
    />
  )
}
