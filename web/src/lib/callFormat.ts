/** Định dạng thời lượng và nội dung tin nhắn cuộc gọi (NC Ưu tiên 3). */
import type { TFunction } from 'i18next'

/** "39 giây", "2 phút", "2 phút 5 giây" — theo ngôn ngữ đang chọn. */
export function formatCallDuration(totalSeconds: number, t: TFunction): string {
  const safe = Math.max(0, Math.round(totalSeconds))
  const minutes = Math.floor(safe / 60)
  const seconds = safe % 60

  if (minutes === 0) {
    return t('call.duration_seconds', { count: seconds })
  }

  if (seconds === 0) {
    return t('call.duration_minutes', { count: minutes })
  }

  return t('call.duration_minutes_seconds', { minutes, seconds })
}

/**
 * Tin nhắn hệ thống loại `Call` lưu dạng `call:{type}:{duration}:{reason}`
 * (xem `CallService.WriteCallMessageAsync`). Trả chuỗi đã dịch thay vì chuỗi thô.
 */
export function formatCallMessage(content: string | null, t: TFunction): string {
  const kind = content?.startsWith('call:Video:') ? t('call.message_video') : t('call.message_audio')

  if (!content) {
    return kind
  }

  const parts = content.split(':')
  const duration = Number.parseInt(parts[2] ?? '', 10)
  const reason = parts[3] ?? 'Completed'

  switch (reason) {
    case 'NoAnswer':
      return t('call.message_missed')
    case 'Declined':
      return `${kind} · ${t('call.message_declined')}`
    case 'Cancelled':
      return `${kind} · ${t('call.message_cancelled')}`
    case 'Failed':
      return `${kind} · ${t('call.message_failed')}`
    default:
      return Number.isFinite(duration) && duration > 0
        ? `${kind} · ${formatCallDuration(duration, t)}`
        : kind
  }
}

/** Nhãn trạng thái một dòng lịch sử cuộc gọi. */
export function callSummaryStatus(
  summary: {
    status: string
    isMissed: boolean
    iWasInitiator: boolean
    myStatus: string
    durationSeconds: number | null
  },
  t: TFunction,
): string {
  if (summary.isMissed) {
    return t('call.status_missed')
  }

  if ((summary.durationSeconds ?? 0) > 0) {
    return t('call.direction_with_duration', {
      direction: summary.iWasInitiator ? t('call.direction_outgoing') : t('call.direction_incoming'),
      duration: formatCallDuration(summary.durationSeconds ?? 0, t),
    })
  }

  switch (summary.status) {
    case 'Ringing':
      return t('call.status_ringing')
    case 'InProgress':
      return t('call.status_in_progress')
    case 'Declined':
      return summary.iWasInitiator ? t('call.status_declined_by_peer') : t('call.status_declined')
    case 'Cancelled':
      return summary.iWasInitiator ? t('call.status_cancelled') : t('call.status_cancelled_by_caller')
    case 'Missed':
      return t('call.status_no_answer')
    case 'Failed':
      return t('call.status_failed')
    default:
      return summary.myStatus === 'Invited'
        ? t('call.status_not_joined')
        : summary.iWasInitiator
          ? t('call.status_outgoing')
          : t('call.status_incoming')
  }
}
