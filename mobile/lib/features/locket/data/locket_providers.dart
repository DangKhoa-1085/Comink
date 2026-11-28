import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/providers.dart';
import '../domain/locket_models.dart';

/// Danh sách Comink Locket còn hiệu lực 24 giờ của bản thân và bạn bè.
final locketsProvider = FutureProvider.autoDispose<List<LocketMoment>>((ref) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/lockets',
        query: {'limit': 40},
      );
  final page = PagedResult.fromJson(data, LocketMoment.fromJson);
  return page.items.where((moment) => !moment.isExpired()).toList(growable: false);
});
