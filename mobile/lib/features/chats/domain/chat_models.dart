/// Người dùng trong hội thoại.
class ConversationPeer {
  const ConversationPeer({
    required this.userId,
    required this.displayName,
    this.userName,
    this.avatarUrl,
  });

  final String userId;
  final String displayName;
  final String? userName;
  final String? avatarUrl;

  factory ConversationPeer.fromJson(Map<String, dynamic> json) => ConversationPeer(
        userId: (json['userId'] ?? json['id'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        userName: _nullable(json['userName']),
        avatarUrl: _nullable(json['avatarUrl']),
      );

  static String? _nullable(dynamic value) {
    final text = value?.toString().trim();
    return (text == null || text.isEmpty) ? null : text;
  }
}

class LastMessagePreview {
  const LastMessagePreview({
    required this.id,
    required this.senderId,
    required this.preview,
    required this.type,
    required this.createdAt,
  });

  final String id;
  final String senderId;
  final String preview;
  final String type;
  final DateTime createdAt;

  factory LastMessagePreview.fromJson(Map<String, dynamic> json) => LastMessagePreview(
        id: (json['id'] ?? '').toString(),
        senderId: (json['senderId'] ?? '').toString(),
        preview: (json['preview'] ?? '').toString(),
        type: (json['type'] ?? 'Text').toString(),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
      );
}

/// Một dòng trong danh sách hội thoại.
class ConversationSummary {
  const ConversationSummary({
    required this.id,
    required this.type,
    required this.memberCount,
    required this.unreadCount,
    this.title,
    this.peer,
    this.lastMessage,
    this.lastMessageAt,
  });

  final String id;
  final String type;
  final String? title;
  final ConversationPeer? peer;
  final LastMessagePreview? lastMessage;
  final int unreadCount;
  final int memberCount;
  final DateTime? lastMessageAt;

  bool get isGroup => type == 'Group';
  String get displayTitle => isGroup ? (title ?? '') : (peer?.displayName ?? '');
  String? get avatarUrl => isGroup ? null : peer?.avatarUrl;

  factory ConversationSummary.fromJson(Map<String, dynamic> json) => ConversationSummary(
        id: (json['id'] ?? '').toString(),
        type: (json['type'] ?? 'Direct').toString(),
        title: json['title']?.toString(),
        peer: json['peer'] is Map
            ? ConversationPeer.fromJson((json['peer'] as Map).cast<String, dynamic>())
            : null,
        lastMessage: json['lastMessage'] is Map
            ? LastMessagePreview.fromJson((json['lastMessage'] as Map).cast<String, dynamic>())
            : null,
        unreadCount: _intValue(json['unreadCount']),
        memberCount: _intValue(json['memberCount']),
        lastMessageAt:
            DateTime.tryParse((json['lastMessageAt'] ?? '').toString())?.toLocal(),
      );

  static int _intValue(dynamic value) =>
      value is int ? value : int.tryParse(value?.toString() ?? '') ?? 0;
}

/// Chi tiết hội thoại (`GET /api/conversations/{id}`), dùng để hiện tên nhóm/thành viên.
class ConversationDetail {
  const ConversationDetail({
    required this.id,
    required this.type,
    required this.members,
    this.title,
    this.avatarUrl,
  });

  final String id;
  final String type;
  final String? title;
  final String? avatarUrl;
  final List<ConversationPeer> members;

  bool get isGroup => type == 'Group';

  String displayTitle(String currentUserId) {
    if (isGroup) {
      return title ?? '';
    }
    for (final member in members) {
      if (member.userId != currentUserId) {
        return member.displayName;
      }
    }
    return '';
  }

  factory ConversationDetail.fromJson(Map<String, dynamic> json) => ConversationDetail(
        id: (json['id'] ?? '').toString(),
        type: (json['type'] ?? 'Direct').toString(),
        title: json['title']?.toString(),
        avatarUrl: json['avatarUrl']?.toString(),
        members: (json['members'] as List<dynamic>? ?? const [])
            .whereType<Map>()
            .map((item) => ConversationPeer.fromJson(item.cast<String, dynamic>()))
            .toList(growable: false),
      );
}

class MessageAttachment {
  const MessageAttachment({
    required this.url,
    this.mediaId,
    this.thumbnailUrl,
    this.width,
    this.height,
  });

  final String url;
  final String? mediaId;
  final String? thumbnailUrl;
  final int? width;
  final int? height;

  factory MessageAttachment.fromJson(Map<String, dynamic> json) => MessageAttachment(
        url: (json['url'] ?? '').toString(),
        mediaId: json['mediaId']?.toString(),
        thumbnailUrl: json['thumbnailUrl']?.toString(),
        width: json['width'] is int ? json['width'] as int : null,
        height: json['height'] is int ? json['height'] as int : null,
      );
}

/// Tin nhắn trong khung chat.
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.conversationId,
    required this.senderId,
    required this.type,
    required this.attachments,
    required this.clientMessageId,
    required this.createdAt,
    this.content,
  });

  final String id;
  final String conversationId;
  final String senderId;
  final String type;
  final String? content;
  final List<MessageAttachment> attachments;
  final String clientMessageId;
  final DateTime createdAt;

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        id: (json['id'] ?? '').toString(),
        conversationId: (json['conversationId'] ?? '').toString(),
        senderId: (json['senderId'] ?? '').toString(),
        type: (json['type'] ?? 'Text').toString(),
        content: (json['content']?.toString().trim().isEmpty ?? true)
            ? null
            : json['content'].toString(),
        attachments: (json['attachments'] as List<dynamic>? ?? const [])
            .whereType<Map>()
            .map((item) => MessageAttachment.fromJson(item.cast<String, dynamic>()))
            .toList(growable: false),
        clientMessageId: (json['clientMessageId'] ?? '').toString(),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
      );
}

/// Một trang lịch sử chat: tin theo thứ tự cũ → mới + con trỏ tải trang cũ hơn.
class ChatHistory {
  const ChatHistory({
    required this.messages,
    required this.nextCursor,
    required this.hasMore,
  });

  final List<ChatMessage> messages;
  final String? nextCursor;
  final bool hasMore;
}

