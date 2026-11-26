import 'dart:convert';

/// Một thông báo trong hộp thư (`GET /api/notifications`).
class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.isRead,
    required this.createdAt,
    this.actorId,
    this.actorDisplayName,
    this.actorAvatarUrl,
    this.entityType,
    this.entityId,
    this.data = const {},
    this.readAt,
  });

  final String id;
  final String type;
  final String? actorId;
  final String? actorDisplayName;
  final String? actorAvatarUrl;
  final String? entityType;
  final String? entityId;
  final Map<String, dynamic> data;
  final bool isRead;
  final DateTime createdAt;
  final DateTime? readAt;

  String? get conversationId => data['conversationId']?.toString();
  String? get callId => data['callId']?.toString();
  String? get callType => data['callType']?.toString();

  factory AppNotification.fromJson(Map<String, dynamic> json) {
    final rawData = json['data'];
    Map<String, dynamic> data = const {};
    if (rawData is String && rawData.trim().isNotEmpty) {
      try {
        final decoded = jsonDecode(rawData);
        if (decoded is Map) {
          data = decoded.cast<String, dynamic>();
        }
      } on FormatException {
        data = const {};
      }
    } else if (rawData is Map) {
      data = rawData.cast<String, dynamic>();
    }

    final actorNameRaw = json['actorDisplayName']?.toString().trim();
    final actorAvatarRaw = json['actorAvatarUrl']?.toString().trim();

    return AppNotification(
      id: (json['id'] ?? '').toString(),
      type: (json['type'] ?? '').toString(),
      actorId: json['actorId']?.toString(),
      actorDisplayName: actorNameRaw != null && actorNameRaw.isNotEmpty ? actorNameRaw : null,
      actorAvatarUrl: actorAvatarRaw != null && actorAvatarRaw.isNotEmpty ? actorAvatarRaw : null,
      entityType: json['entityType']?.toString(),
      entityId: json['entityId']?.toString(),
      data: data,
      isRead: json['isRead'] == true,
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
          DateTime.now(),
      readAt: DateTime.tryParse((json['readAt'] ?? '').toString())?.toLocal(),
    );
  }
}
