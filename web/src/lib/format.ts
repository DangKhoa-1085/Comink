/** Tiện ích hiển thị thời gian và chữ viết tắt tên. */

/** Chữ viết tắt từ tên hiển thị (tối đa 2 chữ). */
export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) {
    return '?'
  }
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}

/** Thời gian tương đối: "vừa xong", "5 phút trước", "3 ngày trước"… */
export function timeAgo(iso: string, locale: string): string {
  const created = new Date(iso).getTime()
  const diffSeconds = Math.round((Date.now() - created) / 1000)
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })

  if (Math.abs(diffSeconds) < 60) {
    return rtf.format(-diffSeconds, 'second')
  }

  const diffMinutes = Math.round(diffSeconds / 60)
  if (Math.abs(diffMinutes) < 60) {
    return rtf.format(-diffMinutes, 'minute')
  }

  const diffHours = Math.round(diffMinutes / 60)
  if (Math.abs(diffHours) < 24) {
    return rtf.format(-diffHours, 'hour')
  }

  const diffDays = Math.round(diffHours / 24)
  if (Math.abs(diffDays) < 30) {
    return rtf.format(-diffDays, 'day')
  }

  return new Date(iso).toLocaleDateString(locale)
}
