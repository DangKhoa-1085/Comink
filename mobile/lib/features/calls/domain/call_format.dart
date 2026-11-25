import '../../../core/i18n/app_localizations.dart';
import 'call_models.dart';

/// Định dạng thời lượng cuộc gọi theo ngôn ngữ đang chọn (NC Ưu tiên 3).
String formatCallDuration(int totalSeconds, AppLocalizations l10n) {
  final safe = totalSeconds < 0 ? 0 : totalSeconds;
  final minutes = safe ~/ 60;
  final seconds = safe % 60;

  if (minutes == 0) {
    return l10n.t('call_duration_seconds', {'count': '$seconds'});
  }
  if (seconds == 0) {
    return l10n.t('call_duration_minutes', {'count': '$minutes'});
  }
  return l10n.t('call_duration_minutes_seconds', {
    'minutes': '$minutes',
    'seconds': '$seconds',
  });
}

/// Tin nhắn hệ thống loại `Call` lưu dạng `call:{type}:{duration}:{reason}`.
String formatCallMessage(String? content, AppLocalizations l10n) {
  final kind = content?.startsWith('call:Video:') == true
      ? l10n.t('call_message_video')
      : l10n.t('call_message_audio');

  if (content == null || content.isEmpty) {
    return kind;
  }

  final parts = content.split(':');
  final duration = int.tryParse(parts.length > 2 ? parts[2] : '');
  final reason = parts.length > 3 ? parts[3] : 'Completed';

  switch (reason) {
    case 'NoAnswer':
      return l10n.t('call_message_missed');
    case 'Declined':
      return '$kind · ${l10n.t('call_message_declined')}';
    case 'Cancelled':
      return '$kind · ${l10n.t('call_message_cancelled')}';
    case 'Failed':
      return '$kind · ${l10n.t('call_message_failed')}';
    default:
      return duration != null && duration > 0
          ? '$kind · ${formatCallDuration(duration, l10n)}'
          : kind;
  }
}

/// Nhãn trạng thái một dòng lịch sử cuộc gọi.
String callSummaryStatus(CallSummary summary, AppLocalizations l10n) {
  if (summary.isMissed) {
    return l10n.t('call_status_missed');
  }

  final duration = summary.durationSeconds ?? 0;
  if (duration > 0) {
    return l10n.t('call_direction_with_duration', {
      'direction': summary.iWasInitiator
          ? l10n.t('call_direction_outgoing')
          : l10n.t('call_direction_incoming'),
      'duration': formatCallDuration(duration, l10n),
    });
  }

  switch (summary.status) {
    case 'Ringing':
      return l10n.t('call_status_ringing');
    case 'InProgress':
      return l10n.t('call_status_in_progress');
    case 'Declined':
      return summary.iWasInitiator
          ? l10n.t('call_status_declined_by_peer')
          : l10n.t('call_status_declined');
    case 'Cancelled':
      return summary.iWasInitiator
          ? l10n.t('call_status_cancelled')
          : l10n.t('call_status_cancelled_by_caller');
    case 'Missed':
      return l10n.t('call_status_no_answer');
    case 'Failed':
      return l10n.t('call_status_failed');
    default:
      if (summary.myStatus == 'Invited') {
        return l10n.t('call_status_not_joined');
      }
      return summary.iWasInitiator
          ? l10n.t('call_status_outgoing')
          : l10n.t('call_status_incoming');
  }
}
