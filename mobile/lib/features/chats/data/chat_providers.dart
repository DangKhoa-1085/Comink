import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../domain/chat_models.dart';

final conversationsProvider =
    FutureProvider.autoDispose<List<ConversationSummary>>((ref) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/conversations',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, ConversationSummary.fromJson);
  return page.items;
});

final conversationDetailProvider =
    FutureProvider.autoDispose.family<ConversationDetail, String>((ref, id) async {
  final data = await ref.watch(apiClientProvider).get('/api/conversations/$id');
  if (data is! Map) {
    throw const ApiException(code: 'INTERNAL_ERROR');
  }
  return ConversationDetail.fromJson(data.cast<String, dynamic>());
});

final messagesProvider =
    FutureProvider.autoDispose.family<ChatHistory, String>((ref, conversationId) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/conversations/$conversationId/messages',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, ChatMessage.fromJson);
  // Backend trả tin mới nhất trước; đảo lại để hiển thị theo thời gian.
  return ChatHistory(
    messages: page.items.reversed.toList(growable: false),
    nextCursor: page.nextCursor,
    hasMore: page.hasMore,
  );
});
