import '../../../core/i18n/app_localizations.dart';

/// Danh mục cảm xúc hợp lệ trên Locket — khớp `LocketRules.AllowedReactions` ở backend.
const locketAllowedReactions = <String>['❤️', '😂', '😮', '🔥', '🥹'];

/// Tác giả của một khoảnh khắc Locket.
class LocketAuthor {
  const LocketAuthor({
    required this.id,
    required this.userName,
    required this.displayName,
    this.avatarUrl,
  });

  final String id;
  final String userName;
  final String displayName;
  final String? avatarUrl;

  factory LocketAuthor.fromJson(Map<String, dynamic> json) => LocketAuthor(
        id: (json['id'] ?? '').toString(),
        userName: (json['userName'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        avatarUrl: _cleanNullable(json['avatarUrl']),
      );
}

/// Thống kê một emoji cảm xúc trên ảnh Locket.
class LocketReactionSummary {
  const LocketReactionSummary({
    required this.emoji,
    required this.count,
    required this.reactedByMe,
  });

  final String emoji;
  final int count;
  final bool reactedByMe;

  factory LocketReactionSummary.fromJson(Map<String, dynamic> json) => LocketReactionSummary(
        emoji: (json['emoji'] ?? '').toString(),
        count: json['count'] is int ? json['count'] as int : int.tryParse('${json['count']}') ?? 0,
        reactedByMe: json['reactedByMe'] == true,
      );
}

/// Khoảnh khắc Locket (hiệu lực 24 giờ kể từ lúc đăng).
class LocketMoment {
  const LocketMoment({
    required this.id,
    required this.author,
    required this.imageUrl,
    required this.audienceType,
    required this.createdAt,
    required this.expiresAt,
    required this.isMine,
    required this.reactions,
    this.caption,
  });

  final String id;
  final LocketAuthor author;
  final String imageUrl;
  final String? caption;
  final String audienceType;
  final DateTime createdAt;
  final DateTime expiresAt;
  final bool isMine;
  final List<LocketReactionSummary> reactions;

  bool isExpired([DateTime? now]) {
    final current = now ?? DateTime.now();
    return !expiresAt.isAfter(current);
  }

  /// Nhãn thời gian còn lại trước khi Locket tự xóa sau 24 giờ.
  String remainingLabel(AppLocalizations l10n, [DateTime? now]) {
    final diff = expiresAt.difference(now ?? DateTime.now());
    if (diff.isNegative || diff.inMinutes < 1) {
      return l10n.t('locket_expires_soon');
    }
    if (diff.inHours >= 1) {
      return l10n.t('locket_remaining_hours', {'hours': '${diff.inHours}'});
    }
    return l10n.t('locket_remaining_minutes', {'minutes': '${diff.inMinutes}'});
  }

  factory LocketMoment.fromJson(Map<String, dynamic> json) {
    final authorMap = json['author'] is Map
        ? (json['author'] as Map).cast<String, dynamic>()
        : const <String, dynamic>{};

    return LocketMoment(
      id: (json['id'] ?? '').toString(),
      author: LocketAuthor.fromJson(authorMap),
      imageUrl: (json['imageUrl'] ?? '').toString(),
      caption: _cleanNullable(json['caption']),
      audienceType: (json['audienceType'] ?? 'AllFriends').toString(),
      createdAt:
          DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ?? DateTime.now(),
      expiresAt:
          DateTime.tryParse((json['expiresAt'] ?? '').toString())?.toLocal() ??
              DateTime.now().add(const Duration(hours: 24)),
      isMine: json['isMine'] == true,
      reactions: (json['reactions'] as List<dynamic>? ?? const [])
          .whereType<Map>()
          .map((item) => LocketReactionSummary.fromJson(item.cast<String, dynamic>()))
          .toList(growable: false),
    );
  }
}

String? _cleanNullable(Object? value) {
  if (value == null) {
    return null;
  }
  final text = value.toString().trim();
  return text.isEmpty ? null : text;
}
