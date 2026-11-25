import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/features/chats/domain/chat_models.dart';

void main() {
  test('ChatMessage parses quote, reactions and edited state', () {
    final message = ChatMessage.fromJson({
      'id': 'm1',
      'conversationId': 'c1',
      'senderId': 'u1',
      'type': 'Text',
      'content': 'đã sửa',
      'attachments': const [],
      'clientMessageId': 'client-1',
      'createdAt': '2026-11-20T10:00:00Z',
      'editedAt': '2026-11-20T10:05:00Z',
      'replyTo': {
        'messageId': 'm0',
        'senderId': 'u2',
        'senderDisplayName': 'Bob',
        'contentPreview': 'tin gốc',
        'type': 'Text',
        'isDeleted': false,
      },
      'reactions': [
        {'reaction': 'love', 'emoji': '❤️', 'count': 2, 'reactedByMe': true},
      ],
    });

    expect(message.isDeleted, isFalse);
    expect(message.editedAt, isNotNull);
    expect(message.replyTo?.messageId, 'm0');
    expect(message.replyTo?.senderDisplayName, 'Bob');
    expect(message.reactions.single.reaction, 'love');
    expect(message.reactions.single.count, 2);
    expect(message.reactions.single.reactedByMe, isTrue);
  });

  test('ChatMessage hides content of recalled message', () {
    final message = ChatMessage.fromJson({
      'id': 'm2',
      'conversationId': 'c1',
      'senderId': 'u1',
      'type': 'Text',
      'content': null,
      'attachments': const [],
      'clientMessageId': 'client-2',
      'createdAt': '2026-11-20T10:00:00Z',
      'deletedAt': '2026-11-20T10:10:00Z',
      'replyTo': null,
      'reactions': const [],
    });

    expect(message.isDeleted, isTrue);
    expect(message.content, isNull);
    expect(message.reactions, isEmpty);
  });

  test('ConversationSummary parses mute and deleted preview', () {
    final summary = ConversationSummary.fromJson({
      'id': 'c1',
      'type': 'Direct',
      'title': null,
      'memberCount': 2,
      'unreadCount': 3,
      'isMuted': true,
      'mutedUntil': '2026-11-20T11:00:00Z',
      'peer': {'userId': 'u2', 'displayName': 'Bob', 'isOnline': false},
      'lastMessage': {
        'id': 'm1',
        'senderId': 'u2',
        'preview': '',
        'type': 'Text',
        'createdAt': '2026-11-20T10:00:00Z',
        'isDeleted': true,
      },
    });

    expect(summary.isMuted, isTrue);
    expect(summary.mutedUntil, isNotNull);
    expect(summary.lastMessage?.isDeleted, isTrue);
  });

  test('PinnedMessages and MessageSearchResult parse API payloads', () {
    final pinned = PinnedMessages.fromJson({
      'conversationId': 'c1',
      'pinned': [
        {
          'message': {
            'id': 'm1',
            'conversationId': 'c1',
            'senderId': 'u1',
            'type': 'Text',
            'content': 'tin ghim',
            'attachments': const [],
            'clientMessageId': 'client-1',
            'createdAt': '2026-11-20T10:00:00Z',
            'reactions': const [],
          },
          'pinnedByUserId': 'u2',
          'pinnedByDisplayName': 'Bob',
          'pinnedAt': '2026-11-20T10:05:00Z',
        },
      ],
    });
    expect(pinned.pinned.single.pinnedByDisplayName, 'Bob');

    final result = MessageSearchResult.fromJson({
      'messageId': 'm1',
      'conversationId': 'c1',
      'conversationTitle': null,
      'conversationType': 'Direct',
      'peerDisplayName': 'Bob',
      'senderId': 'u1',
      'senderDisplayName': 'Alice',
      'contentPreview': 'Cuộc họp nhóm',
      'createdAt': '2026-11-20T10:00:00Z',
    });
    expect(result.conversationLabel, 'Bob');
    expect(result.contentPreview, 'Cuộc họp nhóm');
  });
}
