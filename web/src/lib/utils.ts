import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

/**
 * Kết hợp các class CSS với Tailwind Merge để tránh xung đột
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
