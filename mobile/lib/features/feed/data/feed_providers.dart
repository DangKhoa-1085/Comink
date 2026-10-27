import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/providers.dart';
import '../domain/post.dart';

/// Trang đầu của bảng tin; các trang cũ hơn do FeedPage tải bằng `nextCursor`.
final feedProvider = FutureProvider.autoDispose<FeedHistory>((ref) async {
  final api = ref.watch(apiClientProvider);
  final data = await api.get('/api/feed', query: {'limit': 20});
  final page = PagedResult.fromJson(data, Post.fromJson);
  return FeedHistory(
    posts: page.items,
    nextCursor: page.nextCursor,
    hasMore: page.hasMore,
  );
});
