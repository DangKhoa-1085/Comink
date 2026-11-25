import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

/// Payload của thông báo: đủ dữ liệu để điều hướng khi người dùng bấm vào.
class NotificationPayload {
  const NotificationPayload({this.type, this.conversationId, this.callId});

  factory NotificationPayload.fromData(Map<String, dynamic> data) {
    return NotificationPayload(
      type: data['type']?.toString(),
      conversationId: data['conversationId']?.toString(),
      callId: data['callId']?.toString(),
    );
  }

  final String? type;
  final String? conversationId;
  final String? callId;

  Map<String, dynamic> toData() {
    return {
      if (type != null) 'type': type,
      if (conversationId != null) 'conversationId': conversationId,
      if (callId != null) 'callId': callId,
    };
  }

  String encode() => jsonEncode(toData());

  /// Trả về null nếu payload rỗng hoặc không phải JSON hợp lệ.
  static NotificationPayload? tryParse(String? raw) {
    if (raw == null || raw.isEmpty) {
      return null;
    }
    try {
      final decoded = jsonDecode(raw);
      if (decoded is Map) {
        return NotificationPayload.fromData(Map<String, dynamic>.from(decoded));
      }
    } on FormatException {
      return null;
    }
    return null;
  }
}

/// Cuộc gọi đến đã có màn hình cuộc gọi riêng nên không cần thêm thông báo khay.
bool shouldShowTrayNotification(NotificationPayload payload) {
  return payload.type != 'CallIncoming';
}

/// ID ổn định theo hội thoại: tin nhắn mới thay thế thông báo cũ thay vì xếp chồng.
int trayNotificationId(NotificationPayload payload) {
  final key = payload.conversationId ?? payload.type ?? 'comink';
  return key.hashCode & 0x7fffffff;
}

/// Hiển thị thông báo hệ thống.
///
/// Android không tự hiện thông báo FCM khi app đang mở (foreground) — service này bù
/// phần đó để tin nhắn/lời mời vẫn có thông báo trên khay (quyết định `docs/02`).
class LocalNotifications {
  LocalNotifications({FlutterLocalNotificationsPlugin? plugin})
    : _plugin = plugin ?? FlutterLocalNotificationsPlugin();

  static const String channelId = 'comink_messages';
  static const String channelName = 'Tin nhắn';
  static const String channelDescription =
      'Thông báo tin nhắn, lời mời kết bạn và cuộc gọi đến của Comink';

  final FlutterLocalNotificationsPlugin _plugin;
  bool _initialized = false;
  void Function(NotificationPayload payload)? _onTap;

  Future<void> initialize({
    void Function(NotificationPayload payload)? onTap,
  }) async {
    if (_initialized || kIsWeb) {
      return;
    }
    _onTap = onTap;

    const settings = InitializationSettings(
      android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      iOS: DarwinInitializationSettings(),
    );

    await _plugin.initialize(
      settings: settings,
      onDidReceiveNotificationResponse: (response) {
        final payload = NotificationPayload.tryParse(response.payload);
        if (payload != null) {
          _onTap?.call(payload);
        }
      },
    );

    // App được mở bằng cách bấm thông báo do plugin này tạo (khi app chưa chạy).
    final launch = await _plugin.getNotificationAppLaunchDetails();
    if (launch?.didNotificationLaunchApp ?? false) {
      final payload = NotificationPayload.tryParse(
        launch?.notificationResponse?.payload,
      );
      if (payload != null) {
        _onTap?.call(payload);
      }
    }

    _initialized = true;
  }

  Future<void> show({
    required NotificationPayload payload,
    required String title,
    required String body,
  }) async {
    if (!_initialized || kIsWeb) {
      return;
    }

    final androidDetails = AndroidNotificationDetails(
      channelId,
      channelName,
      channelDescription: channelDescription,
      importance: Importance.max,
      priority: Priority.high,
      styleInformation: BigTextStyleInformation(body, contentTitle: title),
    );

    await _plugin.show(
      id: trayNotificationId(payload),
      title: title,
      body: body,
      notificationDetails: NotificationDetails(
        android: androidDetails,
        iOS: const DarwinNotificationDetails(),
      ),
      payload: payload.encode(),
    );
  }
}
