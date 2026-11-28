import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/i18n/app_localizations.dart';
import 'package:comink_mobile/features/locket/domain/locket_models.dart';

void main() {
  test('LocketMoment parses author, caption, reactions and 24h expiry', () {
    final now = DateTime.utc(2026, 11, 28, 10, 0);
    final moment = LocketMoment.fromJson({
      'id': 'lk-1',
      'author': {
        'id': 'u1',
        'userName': 'khoa.tester',
        'displayName': 'Khoa Võ',
        'avatarUrl': 'https://cdn.example.com/a.jpg',
      },
      'imageUrl': 'https://cdn.example.com/locket.jpg',
      'caption': 'con tuiiiiiiiiiiiiii',
      'audienceType': 'AllFriends',
      'createdAt': now.toIso8601String(),
      'expiresAt': now.add(const Duration(hours: 24)).toIso8601String(),
      'isMine': true,
      'reactions': [
        {'emoji': '😂', 'count': 2, 'reactedByMe': true},
      ],
    });

    expect(moment.id, 'lk-1');
    expect(moment.author.displayName, 'Khoa Võ');
    expect(moment.caption, 'con tuiiiiiiiiiiiiii');
    expect(moment.isMine, isTrue);
    expect(moment.reactions.single.emoji, '😂');
    expect(moment.reactions.single.count, 2);
    expect(moment.reactions.single.reactedByMe, isTrue);

    expect(moment.isExpired(now.toLocal().add(const Duration(hours: 23))), isFalse);
    expect(moment.isExpired(now.toLocal().add(const Duration(hours: 24))), isTrue);

    const vi = AppLocalizations(Locale('vi'));
    expect(
      moment.remainingLabel(vi, now.toLocal().add(const Duration(hours: 2))),
      'Còn 22g',
    );
    expect(
      moment.remainingLabel(vi, now.toLocal().add(const Duration(hours: 23, minutes: 45))),
      'Còn 15p',
    );
  });
}
