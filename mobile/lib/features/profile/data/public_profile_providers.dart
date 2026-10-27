import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../feed/domain/post.dart';
import '../domain/public_profile.dart';

final publicProfileProvider =
    FutureProvider.autoDispose.family<PublicProfile, String>((ref, userId) async {
  final data = await ref.watch(apiClientProvider).get('/api/users/$userId');
  if (data is! Map) {
    throw const ApiException(code: 'INTERNAL_ERROR');
  }
  return PublicProfile.fromJson(data.cast<String, dynamic>());
});

final userPostsProvider =
    FutureProvider.autoDispose.family<List<Post>, String>((ref, userId) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/users/$userId/posts',
        query: {'limit': 20},
      );
  final page = PagedResult.fromJson(data, Post.fromJson);
  return page.items;
});
