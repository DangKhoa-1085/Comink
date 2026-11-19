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

/// Kết nối SignalR `/hub/chat` để nhận tin nhắn mới, cuộc gọi, presence,
/// "đang nhập" và trạng thái đã đọc (NC GĐ 7).
///
/// Chỉ cần kết nối khi đã đăng nhập; `CominkApp` gọi connect/disconnect theo session.
class ChatRealtime {
  ChatRealtime(this._ref);

  static const String messageCreatedEvent = 'MessageCreated';
  static const String messageUpdatedEvent = 'MessageUpdated';
  static const String pinnedMessagesChangedEvent = 'PinnedMessagesChanged';
  static const String presenceChangedEvent = 'PresenceChanged';
  static const String typingChangedEvent = 'TypingChanged';
  static const String messagesReadEvent = 'MessagesRead';
  static const String callIncomingEvent = 'CallIncoming';
  static const String callUpdatedEvent = 'CallUpdated';
  static const String callEndedEvent = 'CallEnded';

  /// Nhịp làm mới TTL presence (backend TTL 90 giây).
  static const Duration presencePingInterval = Duration(seconds: 30);

  /// Tự ẩn "đang nhập" nếu không nhận thêm tín hiệu (client gõ tự throttle 5 giây).
  static const Duration typingIdleTimeout = Duration(seconds: 6);

  final Ref _ref;
  HubConnection? _connection;
  bool _connecting = false;
  Timer? _pingTimer;
  final StreamController<CallRealtimeSignal> _callEvents =
      StreamController<CallRealtimeSignal>.broadcast();

  /// conversationId → userId → thời điểm hết hạn khỏi trạng thái "đang nhập".
  final Map<String, Map<String, DateTime>> _typing = {};
  final Map<String, Timer> _typingTimers = {};
  final StreamController<void> _typingChanges = StreamController<void>.broadcast();

  /// Sự kiện CallIncoming/CallUpdated/CallEnded để `CallController` xử lý.
  Stream<CallRealtimeSignal> get callEvents => _callEvents.stream;

  /// Bắn mỗi khi danh sách người đang gõ thay đổi (UI gọi `setState`).
  Stream<void> get typingChanges => _typingChanges.stream;

  /// Danh sách userId đang gõ trong hội thoại (đã loại người đã hết hạn).
  Set<String> typingUserIds(String conversationId) =>
      _typing[conversationId]?.keys.toSet() ?? <String>{};

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
    connection.on(messageUpdatedEvent, _onMessageUpdated);
    connection.on(pinnedMessagesChangedEvent, _onPinnedMessagesChanged);
    connection.on(presenceChangedEvent, _onPresenceChanged);
    connection.on(typingChangedEvent, _onTypingChanged);
    connection.on(messagesReadEvent, _onMessagesRead);
    connection.on(callIncomingEvent, (args) => _emitCall(callIncomingEvent, args));
    connection.on(callUpdatedEvent, (args) => _emitCall(callUpdatedEvent, args));
    connection.on(callEndedEvent, (args) => _emitCall(callEndedEvent, args));
    connection.onclose(({error}) {
      _connection = null;
      _stopPing();
      _clearTyping();
    });
    connection.onreconnected(({connectionId}) {
      // Quay lại sau khi mất mạng: làm mới TTL presence và dữ liệu có thể đã lỡ.
      unawaited(sendPing());
      _ref.invalidate(conversationsProvider);
      _ref.invalidate(conversationDetailProvider);
    });
    connection.onreconnecting(({error}) {
      _clearTyping();
    });

    try {
      await connection.start();
      _connection = connection;
      _startPing();
    } catch (_) {
      // Không có mạng hoặc token chưa sẵn sàng: sẽ kết nối lại ở lần đăng nhập sau.
      _connection = null;
      _stopPing();
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
    _stopPing();
    _clearTyping();
    if (connection == null) {
      return;
    }

    try {
      await connection.stop();
    } catch (_) {
      // Bỏ qua lỗi khi ngắt kết nối.
    }
  }

  /// Gửi tín hiệu "đang nhập". Lỗi mạng bị nuốt — đây là tín hiệu tạm thời.
  Future<void> sendTyping(String conversationId, bool isTyping) async {
    final connection = _connection;
    if (connection == null || connection.state != HubConnectionState.Connected) {
      return;
    }

    try {
      await connection.invoke('Typing', args: [conversationId, isTyping]);
    } catch (_) {
      // Bỏ qua: lần gõ sau sẽ gửi lại.
    }
  }

  /// Làm mới TTL presence (backend TTL 90 giây).
  Future<void> sendPing() async {
    final connection = _connection;
    if (connection == null || connection.state != HubConnectionState.Connected) {
      return;
    }

    try {
      await connection.invoke('Ping');
    } catch (_) {
      // Bỏ qua: SignalR tự kết nối lại.
    }
  }

  /// Đánh dấu đã đọc tới một tin (server chỉ tiến mốc, gọi lặp lại vô hại).
  Future<void> markRead(String conversationId, String messageId) async {
    try {
      await _ref.read(apiClientProvider).post(
            '/api/conversations/$conversationId/read',
            data: {'messageId': messageId},
          );
      _ref.invalidate(conversationsProvider);
    } catch (_) {
      // Mất mạng: lần mở khung chat kế tiếp sẽ đánh dấu lại.
    }
  }

  void dispose() {
    _connection = null;
    _stopPing();
    _clearTyping();
    _typingTimers.clear();
    unawaited(_callEvents.close());
    unawaited(_typingChanges.close());
  }

  void _startPing() {
    _pingTimer?.cancel();
    _pingTimer = Timer.periodic(presencePingInterval, (_) => unawaited(sendPing()));
  }

  void _stopPing() {
    _pingTimer?.cancel();
    _pingTimer = null;
  }

  void _clearTyping() {
    _typing.clear();
    for (final timer in _typingTimers.values) {
      timer.cancel();
    }
    _typingTimers.clear();
    if (!_typingChanges.isClosed) {
      _typingChanges.add(null);
    }
  }

  void _emitTypingChanged() {
    if (!_typingChanges.isClosed) {
      _typingChanges.add(null);
    }
  }

  /// Đọc payload Map từ tham số đầu tiên của sự kiện; trả null nếu không hợp lệ.
  static Map<String, dynamic>? _payload(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty) {
      return null;
    }
    final payload = arguments.first;
    if (payload is! Map) {
      return null;
    }
    return payload.cast<String, dynamic>();
  }

  void _emitCall(String eventName, List<Object?>? arguments) {
    final data = _payload(arguments);
    if (data == null || _callEvents.isClosed) {
      return;
    }
    _callEvents.add(CallRealtimeSignal(eventName, data));
  }

  void _onMessageCreated(List<Object?>? arguments) {
    final data = _payload(arguments);
    if (data == null) {
      return;
    }

    final conversationId = data['conversationId']?.toString();
    _ref.invalidate(conversationsProvider);
    if (conversationId != null && conversationId.isNotEmpty) {
      _ref.invalidate(messagesProvider(conversationId));
    }
  }

  /// Tin bị sửa/thu hồi/đổi cảm xúc (NC Ưu tiên 2) → tải lại lịch sử hội thoại.
  void _onMessageUpdated(List<Object?>? arguments) {
    final data = _payload(arguments);
    if (data == null) {
      return;
    }

    final conversationId = data['conversationId']?.toString();
    _ref.invalidate(conversationsProvider);
    if (conversationId != null && conversationId.isNotEmpty) {
      _ref.invalidate(messagesProvider(conversationId));
    }
  }

  /// Danh sách ghim thay đổi (ghim/bỏ ghim/thu hồi tin đang ghim).
  void _onPinnedMessagesChanged(List<Object?>? arguments) {
    final data = _payload(arguments);
    if (data == null) {
      return;
    }

    final conversationId = data['conversationId']?.toString();
    _ref.invalidate(conversationsProvider);
    if (conversationId != null && conversationId.isNotEmpty) {
      _ref.invalidate(pinnedMessagesProvider(conversationId));
    }
  }

  void _onPresenceChanged(List<Object?>? arguments) {
    if (_payload(arguments) == null) {
      return;
    }

    // Không biết hội thoại nào chứa người này → làm mới danh sách + chi tiết đang mở.
    _ref.invalidate(conversationsProvider);
    _ref.invalidate(conversationDetailProvider);
  }

  void _onMessagesRead(List<Object?>? arguments) {
    if (_payload(arguments) == null) {
      return;
    }

    _ref.invalidate(conversationsProvider);
    _ref.invalidate(conversationDetailProvider);
  }

  void _onTypingChanged(List<Object?>? arguments) {
    final data = _payload(arguments);
    if (data == null) {
      return;
    }

    final conversationId = data['conversationId']?.toString();
    final userId = data['userId']?.toString();
    if (conversationId == null || conversationId.isEmpty || userId == null || userId.isEmpty) {
      return;
    }

    final isTyping = data['isTyping'] == true;
    final key = '$conversationId:$userId';
    _typingTimers.remove(key)?.cancel();

    if (!isTyping) {
      _typing[conversationId]?.remove(userId);
      if (_typing[conversationId]?.isEmpty ?? false) {
        _typing.remove(conversationId);
      }
      _emitTypingChanged();
      return;
    }

    _typing.putIfAbsent(conversationId, () => {})[userId] = DateTime.now();
    _typingTimers[key] = Timer(typingIdleTimeout, () {
      _typingTimers.remove(key);
      _typing[conversationId]?.remove(userId);
      if (_typing[conversationId]?.isEmpty ?? false) {
        _typing.remove(conversationId);
      }
      _emitTypingChanged();
    });
    _emitTypingChanged();
  }
}

final chatRealtimeProvider = Provider<ChatRealtime>((ref) {
  final realtime = ChatRealtime(ref);
  ref.onDispose(realtime.dispose);
  return realtime;
});
