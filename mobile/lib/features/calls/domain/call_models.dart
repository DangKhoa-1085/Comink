/// DTO cuộc gọi — khớp `CallDto`, `CallTokenDto` của backend (docs/05 mục 9).
class CallParticipant {
  const CallParticipant({
    required this.userId,
    required this.displayName,
    required this.status,
  });

  final String userId;
  final String displayName;
  final String status;

  factory CallParticipant.fromJson(Map<String, dynamic> json) => CallParticipant(
        userId: (json['userId'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        status: (json['status'] ?? '').toString(),
      );
}

class CallInfo {
  const CallInfo({
    required this.id,
    required this.conversationId,
    required this.type,
    required this.status,
    required this.roomName,
    required this.initiatedBy,
    required this.participants,
    this.answeredAt,
    this.endedAt,
    this.durationSeconds,
    this.endReason,
  });

  final String id;
  final String conversationId;
  final String type;
  final String status;
  final String roomName;
  final String initiatedBy;
  final List<CallParticipant> participants;
  final DateTime? answeredAt;
  final DateTime? endedAt;
  final int? durationSeconds;
  final String? endReason;

  bool get isVideo => type == 'Video';
  bool get isTerminal =>
      const {'Completed', 'Declined', 'Cancelled', 'Missed', 'Failed'}.contains(status);

  factory CallInfo.fromJson(Map<String, dynamic> json) => CallInfo(
        id: (json['id'] ?? '').toString(),
        conversationId: (json['conversationId'] ?? '').toString(),
        type: (json['type'] ?? 'Audio').toString(),
        status: (json['status'] ?? '').toString(),
        roomName: (json['roomName'] ?? '').toString(),
        initiatedBy: (json['initiatedBy'] ?? '').toString(),
        participants: (json['participants'] as List<dynamic>? ?? const [])
            .whereType<Map>()
            .map((item) => CallParticipant.fromJson(item.cast<String, dynamic>()))
            .toList(growable: false),
        answeredAt: DateTime.tryParse((json['answeredAt'] ?? '').toString())?.toLocal(),
        endedAt: DateTime.tryParse((json['endedAt'] ?? '').toString())?.toLocal(),
        durationSeconds: json['durationSeconds'] is int ? json['durationSeconds'] as int : null,
        endReason: json['endReason']?.toString(),
      );
}

class CallToken {
  const CallToken({required this.url, required this.token, required this.roomName});

  final String url;
  final String token;
  final String roomName;

  factory CallToken.fromJson(Map<String, dynamic> json) => CallToken(
        url: (json['url'] ?? '').toString(),
        token: (json['token'] ?? '').toString(),
        roomName: (json['roomName'] ?? '').toString(),
      );
}

enum CallPhase { idle, incoming, outgoing, connecting, active, ended }

class CallState {
  const CallState({
    required this.phase,
    this.call,
    this.errorCode,
    this.micEnabled = true,
    this.cameraEnabled = false,
    this.revision = 0,
    this.endedReason,
  });

  static const idle = CallState(phase: CallPhase.idle);

  final CallPhase phase;
  final CallInfo? call;
  final String? errorCode;
  final bool micEnabled;
  final bool cameraEnabled;
  final int revision;
  final String? endedReason;

  bool get isIdle => phase == CallPhase.idle;

  CallState copyWith({
    CallPhase? phase,
    CallInfo? call,
    String? errorCode,
    bool? micEnabled,
    bool? cameraEnabled,
    int? revision,
    String? endedReason,
    bool clearError = false,
  }) =>
      CallState(
        phase: phase ?? this.phase,
        call: call ?? this.call,
        errorCode: clearError ? null : (errorCode ?? this.errorCode),
        micEnabled: micEnabled ?? this.micEnabled,
        cameraEnabled: cameraEnabled ?? this.cameraEnabled,
        revision: revision ?? this.revision,
        endedReason: endedReason ?? this.endedReason,
      );
}
