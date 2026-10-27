import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:permission_handler/permission_handler.dart';

import '../../../core/config/app_config.dart';
import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/ui/app_keys.dart';
import '../../calls/data/call_controller.dart';
import '../../chats/data/chat_providers.dart';
import '../../../router/app_router.dart';

/// Xử lý FCM ở background (không dùng ref/router).
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  if (!AppConfig.firebaseConfigured) {
    return;
  }
  await Firebase.initializeApp(
    options: FirebaseOptions(
      apiKey: AppConfig.firebaseApiKey,
      appId: AppConfig.firebaseAppId,
      messagingSenderId: AppConfig.firebaseMessagingSenderId,
      projectId: AppConfig.firebaseProjectId,
      storageBucket: AppConfig.firebaseStorageBucket.isEmpty
          ? null
          : AppConfig.firebaseStorageBucket,
    ),
  );
}

/// Đăng ký token FCM, xin quyền thông báo và điều hướng khi bấm notification.
class PushService {
  PushService(this._ref);

  final Ref _ref;
  StreamSubscription<String>? _tokenSubscription;
  StreamSubscription<RemoteMessage>? _messageSubscription;
  StreamSubscription<RemoteMessage>? _openedSubscription;
  bool _started = false;
  String? _token;

  Future<void> start() async {
    if (_started || kIsWeb || !AppConfig.firebaseConfigured) {
      return;
    }

    try {
      await Firebase.initializeApp(
        options: FirebaseOptions(
          apiKey: AppConfig.firebaseApiKey,
          appId: AppConfig.firebaseAppId,
          messagingSenderId: AppConfig.firebaseMessagingSenderId,
          projectId: AppConfig.firebaseProjectId,
          storageBucket: AppConfig.firebaseStorageBucket.isEmpty
              ? null
              : AppConfig.firebaseStorageBucket,
        ),
      );
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

      final messaging = FirebaseMessaging.instance;
      final settings = await messaging.requestPermission(
        alert: true,
        badge: true,
        sound: true,
      );
      if (settings.authorizationStatus == AuthorizationStatus.denied) {
        _showPermissionMessage();
        return;
      }
      if (defaultTargetPlatform == TargetPlatform.android) {
        final permission = await Permission.notification.request();
        if (!permission.isGranted) {
          _showPermissionMessage();
        }
      }

      _token = await _registerWithFirebase(messaging);
      if (_token != null) {
        await _register(_token!);
      }

      _tokenSubscription = messaging.onRegistered.listen((token) async {
        _token = token;
        await _register(token);
      });
      _messageSubscription = FirebaseMessaging.onMessage.listen(_onForegroundMessage);
      _openedSubscription =
          FirebaseMessaging.onMessageOpenedApp.listen(_onMessageOpened);

      final initial = await messaging.getInitialMessage();
      if (initial != null) {
        _onMessageOpened(initial);
      }

      _started = true;
    } catch (error) {
      debugPrint('Không khởi tạo được FCM: $error');
    }
  }

  Future<void> stop() async {
    await _tokenSubscription?.cancel();
    await _messageSubscription?.cancel();
    await _openedSubscription?.cancel();
    _tokenSubscription = null;
    _messageSubscription = null;
    _openedSubscription = null;
    _started = false;
  }

  /// Gọi trước khi đăng xuất (còn access token) để server ngừng gửi push.
  Future<void> unregister() async {
    final token = _token;
    if (token != null) {
      try {
        await _ref.read(apiClientProvider).delete(
              '/api/devices?token=${Uri.encodeQueryComponent(token)}',
            );
      } on ApiException {
        // Bỏ qua — đăng xuất phía client vẫn phải thành công.
      }
    }
    _token = null;
    await stop();
  }

  /// Đăng ký FCM và chờ Firebase Installation ID đầu tiên.
  Future<String?> _registerWithFirebase(FirebaseMessaging messaging) async {
    final registered = messaging.onRegistered.first.timeout(
      const Duration(seconds: 20),
      onTimeout: () => '',
    );
    await messaging.register();
    final token = await registered;
    return token.isEmpty ? null : token;
  }

  Future<void> _register(String token) async {    try {
      await _ref.read(apiClientProvider).post(
        '/api/devices',
        data: {
          'pushToken': token,
          'platform': _platform,
          'deviceName': _deviceName,
        },
      );
    } on ApiException catch (error) {
      debugPrint('Không đăng ký được thiết bị push: ${error.code}');
    }
  }

  void _showPermissionMessage() {
    final context = scaffoldMessengerKey.currentContext;
    if (context == null) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(AppLocalizations.of(context).t('notification_permission_denied'))),
    );
  }

  void _onForegroundMessage(RemoteMessage message) {
    final data = message.data;
    _handleCallPush(data);

    final title = message.notification?.title ?? 'Comink';
    final body = message.notification?.body;
    final messenger = scaffoldMessengerKey.currentState;
    if (messenger != null) {
      messenger.showSnackBar(
        SnackBar(
          content: Text(body == null || body.isEmpty ? title : '$title: $body'),
        ),
      );
    }

    if (data['type'] == 'Message' && data['conversationId'] != null) {
      _ref.invalidate(conversationsProvider);
      _ref.invalidate(messagesProvider(data['conversationId']!));
    }
  }

  void _onMessageOpened(RemoteMessage message) {
    final data = message.data;
    _handleCallPush(data);

    switch (data['type']) {
      case 'Message':
        final id = data['conversationId'];
        if (id != null) {
          _openConversation(id);
        }
      case 'FriendRequest':
      case 'FriendAccepted':
        _ref.read(routerProvider).go('/friends');
      case 'CallIncoming':
        final callId = data['callId'];
        if (callId != null) {
          unawaited(_ref.read(callControllerProvider.notifier).openFromPush(callId));
        }
    }
  }

  void _handleCallPush(Map<String, dynamic> data) {
    if (data['type'] != 'CallIncoming') {
      return;
    }
    final callId = data['callId'];
    if (callId != null) {
      unawaited(_ref.read(callControllerProvider.notifier).openFromPush(callId));
    }
  }

  void _openConversation(String conversationId) {
    _ref.read(routerProvider).push('/chats/$conversationId');
  }

  String get _platform {
    if (kIsWeb) {
      return 'Web';
    }
    return defaultTargetPlatform == TargetPlatform.iOS ? 'iOS' : 'Android';
  }

  String get _deviceName {
    if (kIsWeb) {
      return 'Web';
    }
    return defaultTargetPlatform.name;
  }
}

final pushServiceProvider = Provider<PushService>((ref) {
  return PushService(ref);
});
