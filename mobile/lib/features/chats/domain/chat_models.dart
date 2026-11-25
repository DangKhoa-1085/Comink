/// Người dùng trong hội thoại.
class ConversationPeer {  const ConversationPeer({
    required this.userId,
    required this.displayName,
    this.userName,
    this.avatarUrl,
    this.isOnline = false,
    this.lastSeenAt,
    this.lastReadMessageId,
    this.lastReadAt,
  });

  final String userId;
  final String displayName;
  final String? userName;
  final String? avatarUrl;

  /// Trạng thái online (NC GĐ 7) — chỉ đúng cho người cùng hội thoại.
  final bool isOnline;

  /// Lần cuối online (chỉ khi đang offline và đã từng kết nối).
  final DateTime? lastSeenAt;

  /// Mốc đã đọc của riêng người này (dùng hiện "Đã đọc"/"Đã xem bởi N").
  final String? lastReadMessageId;
  final DateTime? lastReadAt;

  factory ConversationPeer.fromJson(Map<String, dynamic> json) => ConversationPeer(
        userId: (json['userId'] ?? json['id'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        userName: _nullable(json['userName']),
        avatarUrl: _nullable(json['avatarUrl']),
        isOnline: json['isOnline'] == true,
        lastSeenAt: _dateTime(json['lastSeenAt']),
        lastReadMessageId: _nullable(json['lastReadMessageId']),
        lastReadAt: _dateTime(json['lastReadAt']),
      );

  static String? _nullable(dynamic value) {
    final text = value?.toString().trim();
    return (text == null || text.isEmpty) ? null : text;
  }

  static DateTime? _dateTime(dynamic value) {
    final text = value?.toString();
    if (text == null || text.isEmpty) {
      return null;
    }
    return DateTime.tryParse(text)?.toLocal();
  }
}

class LastMessagePreview {
  const LastMessagePreview({
    required this.id,
    required this.senderId,
    required this.preview,
    required this.type,
    required this.createdAt,
    this.isDeleted = false,
  });

  final String id;
  final String senderId;
  final String preview;
  final String type;
  final DateTime createdAt;
  final bool isDeleted;

  factory LastMessagePreview.fromJson(Map<String, dynamic> json) => LastMessagePreview(
        id: (json['id'] ?? '').toString(),
        senderId: (json['senderId'] ?? '').toString(),
        preview: (json['preview'] ?? '').toString(),
        type: (json['type'] ?? 'Text').toString(),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
        isDeleted: json['isDeleted'] == true,
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
    this.isMuted = false,
    this.mutedUntil,
  });

  final String id;
  final String type;
  final String? title;
  final ConversationPeer? peer;
  final LastMessagePreview? lastMessage;
  final int unreadCount;
  final int memberCount;
  final DateTime? lastMessageAt;

  /// Trạng thái tắt thông báo của chính tôi (NC Ưu tiên 2).
  final bool isMuted;
  final DateTime? mutedUntil;

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
        isMuted: json['isMuted'] == true,
        mutedUntil: _parseDate(json['mutedUntil']),
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
    this.isMuted = false,
    this.mutedUntil,
  });

  final String id;
  final String type;
  final String? title;
  final String? avatarUrl;
  final List<ConversationPeer> members;
  final bool isMuted;
  final DateTime? mutedUntil;

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
        isMuted: json['isMuted'] == true,
        mutedUntil: _parseDate(json['mutedUntil']),
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

/// Ảnh chụp tin được phản hồi (NC Ưu tiên 2).
class MessageQuote {
  const MessageQuote({
    required this.messageId,
    required this.senderId,
    required this.senderDisplayName,
    required this.type,
    required this.isDeleted,
    this.contentPreview,
  });

  final String messageId;
  final String senderId;
  final String senderDisplayName;
  final String? contentPreview;
  final String type;
  final bool isDeleted;

  factory MessageQuote.fromJson(Map<String, dynamic> json) => MessageQuote(
        messageId: (json['messageId'] ?? '').toString(),
        senderId: (json['senderId'] ?? '').toString(),
        senderDisplayName: (json['senderDisplayName'] ?? '').toString(),
        contentPreview: (json['contentPreview']?.toString().isEmpty ?? true)
            ? null
            : json['contentPreview'].toString(),
        type: (json['type'] ?? 'Text').toString(),
        isDeleted: json['isDeleted'] == true,
      );
}

/// Nguồn của tin chuyển tiếp (NC Ưu tiên 2).
class ForwardedFrom {
  const ForwardedFrom({
    required this.messageId,
    required this.senderId,
    required this.senderDisplayName,
  });

  final String messageId;
  final String senderId;
  final String senderDisplayName;

  factory ForwardedFrom.fromJson(Map<String, dynamic> json) => ForwardedFrom(
        messageId: (json['messageId'] ?? '').toString(),
        senderId: (json['senderId'] ?? '').toString(),
        senderDisplayName: (json['senderDisplayName'] ?? '').toString(),
      );
}

/// Một loại cảm xúc đang có trên tin (NC Ưu tiên 2).
class ChatReaction {
  const ChatReaction({
    required this.reaction,
    required this.emoji,
    required this.count,
    required this.reactedByMe,
  });

  final String reaction;
  final String emoji;
  final int count;
  final bool reactedByMe;

  factory ChatReaction.fromJson(Map<String, dynamic> json) => ChatReaction(
        reaction: (json['reaction'] ?? '').toString(),
        emoji: (json['emoji'] ?? '').toString(),
        count: json['count'] is int ? json['count'] as int : 0,
        reactedByMe: json['reactedByMe'] == true,
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
    this.replyTo,
    this.forwardedFrom,
    this.editedAt,
    this.deletedAt,
    this.reactions = const [],
  });

  final String id;
  final String conversationId;
  final String senderId;
  final String type;
  final String? content;
  final List<MessageAttachment> attachments;
  final String clientMessageId;
  final DateTime createdAt;
  final MessageQuote? replyTo;
  final ForwardedFrom? forwardedFrom;
  final DateTime? editedAt;

  /// Khác null = tin đã bị thu hồi; nội dung/tệp/cảm xúc đã rỗng.
  final DateTime? deletedAt;
  final List<ChatReaction> reactions;

  bool get isDeleted => deletedAt != null;

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
        replyTo: json['replyTo'] is Map
            ? MessageQuote.fromJson((json['replyTo'] as Map).cast<String, dynamic>())
            : null,
        forwardedFrom: json['forwardedFrom'] is Map
            ? ForwardedFrom.fromJson((json['forwardedFrom'] as Map).cast<String, dynamic>())
            : null,
        editedAt: _parseDate(json['editedAt']),
        deletedAt: _parseDate(json['deletedAt']),
        reactions: (json['reactions'] as List<dynamic>? ?? const [])
            .whereType<Map>()
            .map((item) => ChatReaction.fromJson(item.cast<String, dynamic>()))
            .toList(growable: false),
      );
}

DateTime? _parseDate(dynamic value) {
  final text = value?.toString();
  if (text == null || text.isEmpty) {
    return null;
  }
  return DateTime.tryParse(text)?.toLocal();
}

/// Một tin đang được ghim trong hội thoại.
class PinnedMessage {
  const PinnedMessage({
    required this.message,
    required this.pinnedByUserId,
    required this.pinnedByDisplayName,
    required this.pinnedAt,
  });

  final ChatMessage message;
  final String pinnedByUserId;
  final String pinnedByDisplayName;
  final DateTime pinnedAt;

  factory PinnedMessage.fromJson(Map<String, dynamic> json) => PinnedMessage(
        message: ChatMessage.fromJson(
          (json['message'] as Map).cast<String, dynamic>(),
        ),
        pinnedByUserId: (json['pinnedByUserId'] ?? '').toString(),
        pinnedByDisplayName: (json['pinnedByDisplayName'] ?? '').toString(),
        pinnedAt: _parseDate(json['pinnedAt']) ?? DateTime.now(),
      );
}

/// Danh sách ghim hiện tại của hội thoại.
class PinnedMessages {
  const PinnedMessages({required this.conversationId, required this.pinned});

  final String conversationId;
  final List<PinnedMessage> pinned;

  factory PinnedMessages.fromJson(Map<String, dynamic> json) => PinnedMessages(
        conversationId: (json['conversationId'] ?? '').toString(),
        pinned: (json['pinned'] as List<dynamic>? ?? const [])
            .whereType<Map>()
            .map((item) => PinnedMessage.fromJson(item.cast<String, dynamic>()))
            .toList(growable: false),
      );
}

/// Một kết quả tìm kiếm tin nhắn.
class MessageSearchResult {
  const MessageSearchResult({
    required this.messageId,
    required this.conversationId,
    required this.conversationType,
    required this.senderId,
    required this.senderDisplayName,
    required this.createdAt,
    this.conversationTitle,
    this.peerDisplayName,
    this.contentPreview,
  });

  final String messageId;
  final String conversationId;
  final String conversationType;
  final String? conversationTitle;
  final String? peerDisplayName;
  final String senderId;
  final String senderDisplayName;
  final String? contentPreview;
  final DateTime createdAt;

  String get conversationLabel =>
      conversationTitle ?? peerDisplayName ?? senderDisplayName;

  factory MessageSearchResult.fromJson(Map<String, dynamic> json) => MessageSearchResult(
        messageId: (json['messageId'] ?? '').toString(),
        conversationId: (json['conversationId'] ?? '').toString(),
        conversationType: (json['conversationType'] ?? 'Direct').toString(),
        conversationTitle: (json['conversationTitle']?.toString().isEmpty ?? true)
            ? null
            : json['conversationTitle'].toString(),
        peerDisplayName: (json['peerDisplayName']?.toString().isEmpty ?? true)
            ? null
            : json['peerDisplayName'].toString(),
        senderId: (json['senderId'] ?? '').toString(),
        senderDisplayName: (json['senderDisplayName'] ?? '').toString(),
        contentPreview: (json['contentPreview']?.toString().isEmpty ?? true)
            ? null
            : json['contentPreview'].toString(),
        createdAt: _parseDate(json['createdAt']) ?? DateTime.now(),
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

