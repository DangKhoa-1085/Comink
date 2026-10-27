import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/features/notifications/domain/app_notification.dart';

void main() {
  test('AppNotification parse data JSON và trạng thái đã đọc', () {
    final notification = AppNotification.fromJson({
      'id': 'n1',
      'type': 'FriendRequestReceived',
      'actorId': 'u1',
      'entityType': 'Friendship',
      'entityId': 'f1',
      'data': '{"type":"FriendRequest","friendshipId":"f1"}',
      'isRead': true,
      'createdAt': '2026-10-27T05:00:00Z',
      'readAt': '2026-10-27T05:01:00Z',
    });

    expect(notification.id, 'n1');
    expect(notification.type, 'FriendRequestReceived');
    expect(notification.data['friendshipId'], 'f1');
    expect(notification.isRead, isTrue);
    expect(notification.readAt, isNotNull);
  });

  test('AppNotification chịu được data rỗng/hỏng', () {
    final notification = AppNotification.fromJson({
      'id': 'n2',
      'type': 'MissedCall',
      'data': 'not-json',
      'isRead': false,
      'createdAt': '2026-10-27T05:00:00Z',
    });

    expect(notification.data, isEmpty);
    expect(notification.isRead, isFalse);
  });
}
