import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/providers.dart';
import 'chat_providers.dart';

/// Tín hiệu cuộc gọi thô từ SignalR; tầng call tự parse DTO.
class CallRealtimeSignal {
  const CallRealtimeSignal(this.eventName, this.payload);

  final String eventName;
  final Map<String, dynamic> payload;
}

/// Kết nối SignalR `/hub/chat` để nhận tin nhắn mới và sự kiện cuộc gọi.
///
/// Chỉ cần kết nối khi đã đăng nhập; `CominkApp` gọi connect/disconnect theo session.
class ChatRealtime {
  ChatRealtime(this._ref);

  static const String messageCreatedEvent = 'MessageCreated';
  static const String callIncomingEvent = 'CallIncoming';
  static const String callUpdatedEvent = 'CallUpdated';
  static const String callEndedEvent = 'CallEnded';

  final Ref _ref;
  HubConnection? _connection;
  bool _connecting = false;
  final StreamController<CallRealtimeSignal> _callEvents =
      StreamController<CallRealtimeSignal>.broadcast();

  /// Sự kiện CallIncoming/CallUpdated/CallEnded để `CallController` xử lý.
  Stream<CallRealtimeSignal> get callEvents => _callEvents.stream;

  Future<void> connect() async {
    if (_connection != null || _connecting) {
      return;
    }

    _connecting = true;
    final connection = HubConnectionBuilder()
        .withUrl(
          AppConfig.hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async =>
                _ref.read(apiClientProvider).accessToken ?? '',
          ),
        )
        .withAutomaticReconnect()
        .build();

    connection.on(messageCreatedEvent, _onMessageCreated);
    connection.on(callIncomingEvent, (args) => _emitCall(callIncomingEvent, args));
    connection.on(callUpdatedEvent, (args) => _emitCall(callUpdatedEvent, args));
    connection.on(callEndedEvent, (args) => _emitCall(callEndedEvent, args));
    connection.onclose(({error}) => _connection = null);

    try {
      await connection.start();
      _connection = connection;
    } catch (_) {
      // Không có mạng hoặc token chưa sẵn sàng: sẽ kết nối lại ở lần đăng nhập sau.
      _connection = null;
      try {
        await connection.stop();
      } catch (_) {
        // Bỏ qua — kết nối chưa mở.
      }
    } finally {
      _connecting = false;
    }
  }

  Future<void> disconnect() async {
    final connection = _connection;
    _connection = null;
    if (connection == null) {
      return;
    }

    try {
      await connection.stop();
    } catch (_) {
      // Bỏ qua lỗi khi ngắt kết nối.
    }
  }

  void dispose() {
    _connection = null;
    unawaited(_callEvents.close());
  }

  void _emitCall(String eventName, List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || _callEvents.isClosed) {
      return;
    }
    final payload = arguments.first;
    if (payload is! Map) {
      return;
    }
    _callEvents.add(
      CallRealtimeSignal(eventName, payload.cast<String, dynamic>()),
    );
  }

  void _onMessageCreated(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty) {
      return;
    }

    final payload = arguments.first;
    if (payload is! Map) {
      return;
    }

    final data = payload.cast<String, dynamic>();
    final conversationId = data['conversationId']?.toString();
    _ref.invalidate(conversationsProvider);
    if (conversationId != null && conversationId.isNotEmpty) {
      _ref.invalidate(messagesProvider(conversationId));
    }
  }
}

final chatRealtimeProvider = Provider<ChatRealtime>((ref) {
  final realtime = ChatRealtime(ref);
  ref.onDispose(realtime.dispose);
  return realtime;
});
