// Test phần thuần logic của thông báo cục bộ (payload, quyết định hiện khay, ID ổn định).
import 'package:comink_mobile/features/notifications/data/local_notifications.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('NotificationPayload', () {
    test('encode rồi parse giữ nguyên dữ liệu điều hướng', () {
      const payload = NotificationPayload(
        type: 'Message',
        conversationId: 'c1',
      );

      final parsed = NotificationPayload.tryParse(payload.encode());

      expect(parsed, isNotNull);
      expect(parsed!.type, 'Message');
      expect(parsed.conversationId, 'c1');
      expect(parsed.callId, isNull);
    });

    test('fromData chuyển giá trị không phải chuỗi sang chuỗi', () {
      final payload = NotificationPayload.fromData(const {
        'type': 'CallIncoming',
        'callId': 42,
      });

      expect(payload.type, 'CallIncoming');
      expect(payload.callId, '42');
    });

    test('payload rỗng hoặc JSON hỏng trả về null', () {
      expect(NotificationPayload.tryParse(null), isNull);
      expect(NotificationPayload.tryParse(''), isNull);
      expect(NotificationPayload.tryParse('không phải json'), isNull);
      expect(NotificationPayload.tryParse('[1,2,3]'), isNull);
    });
  });

  group('shouldShowTrayNotification', () {
    test('tin nhắn và lời mời kết bạn hiện trên khay', () {
      expect(
        shouldShowTrayNotification(const NotificationPayload(type: 'Message')),
        isTrue,
      );
      expect(
        shouldShowTrayNotification(
          const NotificationPayload(type: 'FriendRequest'),
        ),
        isTrue,
      );
    });

    test('cuộc gọi đến không hiện thêm vì đã có màn hình cuộc gọi', () {
      expect(
        shouldShowTrayNotification(
          const NotificationPayload(type: 'CallIncoming'),
        ),
        isFalse,
      );
    });
  });

  group('trayNotificationId', () {
    test(
      'cùng hội thoại thì cùng ID để thông báo mới thay thế thông báo cũ',
      () {
        const first = NotificationPayload(
          type: 'Message',
          conversationId: 'c1',
        );
        const second = NotificationPayload(
          type: 'Message',
          conversationId: 'c1',
        );

        expect(trayNotificationId(first), trayNotificationId(second));
      },
    );

    test('hội thoại khác nhau thì ID khác nhau', () {
      const first = NotificationPayload(type: 'Message', conversationId: 'c1');
      const second = NotificationPayload(type: 'Message', conversationId: 'c2');

      expect(trayNotificationId(first), isNot(trayNotificationId(second)));
    });

    test('ID luôn không âm', () {
      expect(trayNotificationId(const NotificationPayload()) >= 0, isTrue);
    });
  });
}
