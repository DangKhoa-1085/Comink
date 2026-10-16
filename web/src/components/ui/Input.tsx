import type { InputHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Có lỗi thì viền đỏ để người dùng thấy ngay. */
  invalid?: boolean
}

/** Ô nhập liệu dùng chung. */
export function Input({ className, invalid, ...props }: InputProps) {
  return (
    <input
      className={cn(
        'h-10 w-full rounded-lg border bg-white px-3 text-sm text-slate-900 placeholder:text-slate-400',
        'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent',
        'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500',
        'dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500',
        invalid
          ? 'border-rose-400 dark:border-rose-500'
          : 'border-slate-300 dark:border-slate-700',
        className,
      )}
      {...props}
    />
  )
}
