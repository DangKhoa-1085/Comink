import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/providers.dart';
import '../domain/app_notification.dart';

final notificationsProvider =
    FutureProvider.autoDispose<List<AppNotification>>((ref) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/notifications',
        query: {'limit': 50},
      );
  return PagedResult.fromJson(data, AppNotification.fromJson).items;
});

final unreadNotificationCountProvider = Provider.autoDispose<int>((ref) {
  return ref.watch(notificationsProvider).maybeWhen(
        data: (items) => items.where((item) => !item.isRead).length,
        orElse: () => 0,
      );
});
