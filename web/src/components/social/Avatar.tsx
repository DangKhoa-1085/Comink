import { cn } from '../../lib/utils'
import { initialsOf } from '../../lib/format'

type AvatarSize = 'sm' | 'md' | 'lg'

const sizes: Record<AvatarSize, string> = {
  sm: 'h-8 w-8 text-xs',
  md: 'h-10 w-10 text-sm',
  lg: 'h-20 w-20 text-2xl',
}

/** Ảnh đại diện: dùng URL nếu có, không thì hiện chữ viết tắt. */
export function Avatar({
  name,
  url,
  size = 'md',
  className,
}: {
  name: string
  url?: string | null
  size?: AvatarSize
  className?: string
}) {
  if (url) {
    return (
      <img
        src={url}
        alt={name}
        className={cn('shrink-0 rounded-full object-cover', sizes[size], className)}
      />
    )
  }

  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex shrink-0 items-center justify-center rounded-full bg-indigo-100 font-bold text-indigo-700',
        'dark:bg-indigo-950 dark:text-indigo-300',
        sizes[size],
        className,
      )}
    >
      {initialsOf(name)}
    </span>
  )
}
