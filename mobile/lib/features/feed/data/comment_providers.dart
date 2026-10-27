import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/providers.dart';
import '../domain/comment.dart';

final commentsProvider =
    FutureProvider.autoDispose.family<List<PostComment>, String>((ref, postId) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/posts/$postId/comments',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, PostComment.fromJson);
  return page.items;
});

final repliesProvider =
    FutureProvider.autoDispose.family<List<PostComment>, String>((ref, commentId) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/comments/$commentId/replies',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, PostComment.fromJson);
  return page.items;
});
