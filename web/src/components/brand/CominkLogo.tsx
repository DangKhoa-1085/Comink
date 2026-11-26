import { useId } from 'react'
import { cn } from '../../lib/utils'

type LogoSize = 'sm' | 'md' | 'lg' | 'xl'

const sizeMap: Record<LogoSize, string> = {
  sm: 'h-8 w-8',
  md: 'h-9 w-9',
  lg: 'h-12 w-12',
  xl: 'h-16 w-16',
}

interface CominkLogoProps {
  size?: LogoSize
  showWordmark?: boolean
  className?: string
  wordmarkClassName?: string
}

/**
 * Biểu tượng thương hiệu Comink (Phương án A — Chat-Link C):
 * chữ C bong bóng chat trắng + chấm trực tuyến xanh ngọc trên nền Xanh nước biển.
 */
export function CominkLogo({
  size = 'md',
  showWordmark = false,
  className,
  wordmarkClassName,
}: CominkLogoProps) {
  const gradientId = useId().replace(/:/g, '')

  const mark = (
    <svg
      className={cn('shrink-0', sizeMap[size], className)}
      viewBox="0 0 100 100"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <defs>
        <linearGradient
          id={gradientId}
          x1="8"
          y1="6"
          x2="94"
          y2="96"
          gradientUnits="userSpaceOnUse"
        >
          <stop offset="0%" stopColor="#0369A1" />
          <stop offset="52%" stopColor="#0284C7" />
          <stop offset="100%" stopColor="#06B6D4" />
        </linearGradient>
      </defs>
      <rect x="2" y="2" width="96" height="96" rx="24" fill={`url(#${gradientId})`} />
      <path
        d="M71.5 34.5C66.6 27.6 58.6 23.5 49.5 23.5C34.3 23.5 22 35.6 22 50.5C22 57.6 24.8 64.1 29.4 68.9L23.8 77.8C23.1 78.9 24.1 80.3 25.4 80.0L36.8 77.2C40.7 78.7 45.0 79.5 49.5 79.5C58.8 79.5 66.9 75.2 71.8 68.1"
        stroke="#FFFFFF"
        strokeWidth="11.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="66" cy="51" r="8.2" fill="#34D399" />
    </svg>
  )

  if (!showWordmark) {
    return mark
  }

  return (
    <span className="inline-flex items-center gap-2.5">
      {mark}
      <span
        className={cn(
          'bg-gradient-to-r from-sky-700 via-sky-600 to-cyan-500 bg-clip-text text-lg font-bold tracking-normal text-transparent dark:from-sky-400 dark:via-sky-300 dark:to-cyan-300',
          wordmarkClassName,
        )}
      >
        Comink
      </span>
    </span>
  )
}
