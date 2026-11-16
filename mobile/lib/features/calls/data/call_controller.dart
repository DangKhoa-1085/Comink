import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:livekit_client/livekit_client.dart';
import 'package:permission_handler/permission_handler.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../chats/data/chat_realtime.dart';
import '../domain/call_models.dart';

/// Điều khiển cuộc gọi: nhận SignalR, xin quyền, cấp token và kết nối LiveKit.
class CallController extends Notifier<CallState> {
  Room? _room;
  StreamSubscription<CallRealtimeSignal>? _subscription;
  Timer? _resetTimer;
  bool _busy = false;

  Room? get room => _room;

  @override
  CallState build() {
    _subscription = ref
        .watch(chatRealtimeProvider)
        .callEvents
        .listen(_onSignal);
    ref.onDispose(() {
      _subscription?.cancel();
      _resetTimer?.cancel();
      unawaited(_disconnectRoom());
    });
    return CallState.idle;
  }

  /// Bắt đầu cuộc gọi trong hội thoại hiện tại (1–1 hoặc nhóm).
  Future<void> start(String conversationId, {required bool video}) async {
    if (!state.isIdle || _busy) {
      return;
    }

    _busy = true;
    _resetTimer?.cancel();
    try {
      if (!await _ensurePermissions(video: video)) {
        _showError('CALL_PERMISSION_DENIED');
        return;
      }

      final data = await ref
          .read(apiClientProvider)
          .post(
            '/api/calls',
            data: {
              'conversationId': conversationId,
              'type': video ? 'Video' : 'Audio',
            },
          );
      if (data is! Map) {
        _showError('INTERNAL_ERROR');
        return;
      }

      final call = CallInfo.fromJson(data.cast<String, dynamic>());
      state = CallState(
        phase: CallPhase.outgoing,
        call: call,
        cameraEnabled: video,
      );
      await _joinRoom(call);
    } on ApiException catch (error) {
      _showError(error.code);
    } catch (error) {
      // Ghi lại nguyên nhân thật (LiveKit/timeout/mạng) — nếu không, UI chỉ hiện câu chung
      // "Không kết nối được phòng gọi" và rất khó chẩn đoán khi máy thật lỗi media.
      debugPrint('Không kết nối được phòng gọi: $error');
      _showError('CALL_MEDIA_FAILED');
    } finally {
      _busy = false;
    }
  }

  /// Chấp nhận cuộc gọi đang đổ chuông.
  /// Mở cuộc gọi từ push/notification (app vừa được mở).
  Future<void> openFromPush(String callId) async {
    if (!state.isIdle) {
      return;
    }
    try {
      final data = await ref.read(apiClientProvider).get('/api/calls/$callId');
      if (data is! Map) {
        return;
      }
      final call = CallInfo.fromJson(data.cast<String, dynamic>());
      if (call.id.isEmpty || call.isTerminal) {
        return;
      }
      state = CallState(
        phase: CallPhase.incoming,
        call: call,
        cameraEnabled: call.isVideo,
      );
    } on ApiException {
      // Cuộc gọi đã kết thúc/không còn hiệu lực.
    }
  }

  Future<void> accept() async {
    final call = state.call;
    if (call == null || state.phase != CallPhase.incoming || _busy) {
      return;
    }

    _busy = true;
    try {
      if (!await _ensurePermissions(video: call.isVideo)) {
        await _declineRemote(call.id);
        _showError('CALL_PERMISSION_DENIED');
        return;
      }

      final data = await ref
          .read(apiClientProvider)
          .post('/api/calls/${call.id}/answer');
      final answered = data is Map
          ? CallInfo.fromJson(data.cast<String, dynamic>())
          : call;
      state = state.copyWith(
        phase: CallPhase.connecting,
        call: answered,
        clearError: true,
      );
      await _joinRoom(answered);
    } on ApiException catch (error) {
      _showError(error.code);
    } catch (error) {
      debugPrint('Không kết nối được phòng gọi (trả lời): $error');
      _showError('CALL_MEDIA_FAILED');
    } finally {
      _busy = false;
    }
  }

  Future<void> decline() async {
    final call = state.call;
    if (call == null) {
      _finish('Declined');
      return;
    }
    try {
      await ref.read(apiClientProvider).post('/api/calls/${call.id}/decline');
    } on ApiException {
      // Cuộc gọi có thể đã kết thúc; vẫn đóng UI.
    }
    _finish('Declined');
  }

  Future<void> end() async {
    final call = state.call;
    if (call != null) {
      try {
        await ref.read(apiClientProvider).post('/api/calls/${call.id}/end');
      } on ApiException {
        // Cuộc gọi có thể đã kết thúc từ phía bên kia.
      }
    }
    _finish('Completed');
  }

  Future<void> toggleMicrophone() async {
    final participant = _room?.localParticipant;
    if (participant == null) {
      return;
    }
    final enabled = !state.micEnabled;
    await participant.setMicrophoneEnabled(enabled);
    if (state.phase != CallPhase.idle) {
      state = state.copyWith(micEnabled: enabled, revision: state.revision + 1);
    }
  }

  Future<void> toggleCamera() async {
    final participant = _room?.localParticipant;
    if (participant == null || state.call?.isVideo != true) {
      return;
    }
    final enabled = !state.cameraEnabled;
    await participant.setCameraEnabled(enabled);
    if (state.phase != CallPhase.idle) {
      state = state.copyWith(
        cameraEnabled: enabled,
        revision: state.revision + 1,
      );
    }
  }

  void dismissEnded() {
    if (state.phase == CallPhase.ended) {
      _reset();
    }
  }

  /// Đóng UI và ngắt LiveKit khi đăng xuất/hết phiên.
  void clear() {
    _resetTimer?.cancel();
    _resetTimer = null;
    unawaited(_disconnectRoom());
    state = CallState.idle;
  }

  Future<void> _joinRoom(CallInfo call) async {
    final tokenData = await ref
        .read(apiClientProvider)
        .post('/api/calls/${call.id}/token');
    if (tokenData is! Map) {
      throw const ApiException(code: 'CALL_PROVIDER_UNAVAILABLE');
    }
    final token = CallToken.fromJson(tokenData.cast<String, dynamic>());

    await _disconnectRoom();
    final room = Room();
    _room = room;
    room.addListener(_onRoomChanged);

    await room.connect(token.url, token.token);
    await room.localParticipant?.setMicrophoneEnabled(true);
    if (call.isVideo) {
      await room.localParticipant?.setCameraEnabled(true);
    }

    state = CallState(
      phase: CallPhase.active,
      call: state.call ?? call,
      micEnabled: true,
      cameraEnabled: call.isVideo,
      revision: state.revision + 1,
    );
  }

  void _onSignal(CallRealtimeSignal signal) {
    if (signal.eventName == ChatRealtime.callEndedEvent) {
      final id = signal.payload['callId']?.toString();
      if (id != null && id == state.call?.id) {
        _finish(signal.payload['endReason']?.toString() ?? 'Completed');
      }
      return;
    }

    final CallInfo call;
    try {
      call = CallInfo.fromJson(signal.payload);
    } catch (_) {
      return;
    }
    if (call.id.isEmpty) {
      return;
    }

    if (signal.eventName == ChatRealtime.callIncomingEvent) {
      if (state.isIdle) {
        _resetTimer?.cancel();
        state = CallState(
          phase: CallPhase.incoming,
          call: call,
          cameraEnabled: call.isVideo,
        );
      }
      return;
    }

    if (signal.eventName == ChatRealtime.callUpdatedEvent) {
      if (state.call?.id != call.id) {
        return;
      }
      if (call.isTerminal) {
        _finish(call.endReason ?? call.status);
        return;
      }
      state = state.copyWith(
        call: call,
        phase: state.phase == CallPhase.outgoing
            ? CallPhase.active
            : state.phase,
        revision: state.revision + 1,
      );
    }
  }

  void _onRoomChanged() {
    if (state.isIdle) {
      return;
    }
    state = state.copyWith(revision: state.revision + 1);
  }

  void _showError(String code) {
    state = CallState(phase: CallPhase.ended, errorCode: code);
    _scheduleReset();
  }

  void _finish(String reason) {
    unawaited(_disconnectRoom());
    if (state.phase == CallPhase.ended) {
      return;
    }
    state = CallState(
      phase: CallPhase.ended,
      call: state.call,
      endedReason: reason,
    );
    _scheduleReset();
  }

  void _scheduleReset() {
    _resetTimer?.cancel();
    _resetTimer = Timer(const Duration(seconds: 3), _reset);
  }

  void _reset() {
    _resetTimer?.cancel();
    _resetTimer = null;
    unawaited(_disconnectRoom());
    state = CallState.idle;
  }

  Future<void> _disconnectRoom() async {
    final room = _room;
    _room = null;
    if (room == null) {
      return;
    }
    room.removeListener(_onRoomChanged);
    try {
      await room.disconnect();
    } catch (_) {
      // Bỏ qua lỗi khi ngắt kết nối.
    }
    try {
      await room.dispose();
    } catch (_) {
      // Bỏ qua lỗi giải phóng room.
    }
  }

  Future<void> _declineRemote(String callId) async {
    try {
      await ref.read(apiClientProvider).post('/api/calls/$callId/decline');
    } on ApiException {
      // Bỏ qua.
    }
  }

  Future<bool> _ensurePermissions({required bool video}) async {
    if (kIsWeb) {
      // Trình duyệt tự hỏi quyền khi WebRTC mở mic/camera.
      return true;
    }

    final microphone = await Permission.microphone.request();
    if (!microphone.isGranted) {
      return false;
    }
    if (video) {
      final camera = await Permission.camera.request();
      return camera.isGranted;
    }
    return true;
  }
}

final callControllerProvider = NotifierProvider<CallController, CallState>(
  CallController.new,
);
