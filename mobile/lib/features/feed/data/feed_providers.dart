import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../domain/post.dart';

/// Trang đầu của bảng tin (phân trang cuộn sẽ bổ sung cùng màn hình chat/list).
final feedProvider = FutureProvider.autoDispose<List<Post>>((ref) async {
  final api = ref.watch(apiClientProvider);
  final data = await api.get('/api/feed', query: {'limit': 20});
  if (data is! Map) {
    throw const ApiException(code: 'INTERNAL_ERROR');
  }

  final items = (data['items'] as List<dynamic>? ?? const []);
  return items
      .whereType<Map>()
      .map((item) => Post.fromJson(item.cast<String, dynamic>()))
      .toList(growable: false);
});
