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
        'h-10 w-full rounded-xl border bg-slate-50/70 px-3.5 text-sm text-slate-900 placeholder:text-slate-400 transition-colors',
        'focus:bg-white focus:outline-none focus:ring-2 focus:ring-sky-500 focus:border-transparent',
        'disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500',
        'dark:bg-[#1A2234] dark:text-slate-100 dark:placeholder:text-slate-500 dark:focus:bg-[#121826]',
        invalid
          ? 'border-rose-400 dark:border-rose-500'
          : 'border-slate-200 dark:border-slate-700',
        className,
      )}
      {...props}
    />
  )
}
