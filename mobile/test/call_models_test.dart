import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/i18n/app_localizations.dart';
import 'package:comink_mobile/features/calls/domain/call_format.dart';
import 'package:comink_mobile/features/calls/domain/call_models.dart';

void main() {
  test('CallSummary parses peer, status and duration', () {
    final summary = CallSummary.fromJson({
      'id': 'c1',
      'conversationId': 'conv1',
      'type': 'Video',
      'status': 'Missed',
      'endReason': 'NoAnswer',
      'createdAt': '2026-11-21T10:00:00Z',
      'durationSeconds': null,
      'initiatedBy': 'u1',
      'iWasInitiator': false,
      'myStatus': 'Missed',
      'isMissed': true,
      'conversationType': 'Direct',
      'conversationTitle': null,
      'peer': {'userId': 'u1', 'displayName': 'Alice', 'avatarUrl': null},
      'initiatorDisplayName': 'Alice',
      'answeredAt': null,
      'endedAt': '2026-11-21T10:00:30Z',
    });

    expect(summary.isMissed, isTrue);
    expect(summary.myStatus, 'Missed');
    expect(summary.isVideo, isTrue);
    expect(summary.isGroup, isFalse);
    expect(summary.peer?.displayName, 'Alice');
    expect(summary.initiatorDisplayName, 'Alice');
  });

  test('CallSummary parses group call without peer', () {
    final summary = CallSummary.fromJson({
      'id': 'c2',
      'conversationId': 'g1',
      'type': 'Audio',
      'status': 'Completed',
      'createdAt': '2026-11-21T10:00:00Z',
      'durationSeconds': 65,
      'initiatedBy': 'u1',
      'iWasInitiator': true,
      'myStatus': 'Joined',
      'isMissed': false,
      'conversationType': 'Group',
      'conversationTitle': 'Nhóm học tập',
      'peer': null,
      'initiatorDisplayName': 'Alice',
    });

    expect(summary.isGroup, isTrue);
    expect(summary.peer, isNull);
    expect(summary.conversationTitle, 'Nhóm học tập');
    expect(summary.durationSeconds, 65);
  });

  test('formatCallMessage translates stored call records', () {
    final l10n = AppLocalizations(const Locale('vi'));

    expect(
      formatCallMessage('call:Audio:39:Completed', l10n),
      'Cuộc gọi thoại · 39 giây',
    );
    expect(formatCallMessage('call:Video:0:NoAnswer', l10n), 'Cuộc gọi nhỡ');
    expect(
      formatCallMessage('call:Video:65:Completed', l10n),
      'Cuộc gọi video · 1 phút 5 giây',
    );
    expect(
      formatCallMessage('call:Audio:0:Declined', l10n),
      'Cuộc gọi thoại · Đã bị từ chối',
    );
  });

  test('formatCallDuration handles minutes and zero', () {
    final l10n = AppLocalizations(const Locale('vi'));

    expect(formatCallDuration(0, l10n), '0 giây');
    expect(formatCallDuration(39, l10n), '39 giây');
    expect(formatCallDuration(60, l10n), '1 phút');
    expect(formatCallDuration(125, l10n), '2 phút 5 giây');
  });
}
