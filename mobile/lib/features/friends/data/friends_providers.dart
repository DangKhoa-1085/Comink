import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/paged_result.dart';
import '../../../core/network/providers.dart';
import '../domain/friends_models.dart';

final friendsProvider = FutureProvider.autoDispose<List<FriendshipSummary>>((ref) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/friendships',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, FriendshipSummary.fromJson);
  return page.items.where((friendship) => friendship.isAccepted).toList(growable: false);
});

final incomingFriendRequestsProvider =
    FutureProvider.autoDispose<List<FriendshipSummary>>((ref) async {
  final data = await ref.watch(apiClientProvider).get(
        '/api/friendships/requests/incoming',
        query: {'limit': 50},
      );
  final page = PagedResult.fromJson(data, FriendshipSummary.fromJson);
  return page.items.where((friendship) => friendship.isPending).toList(growable: false);
});

final userSearchProvider =
    FutureProvider.autoDispose.family<List<UserSummary>, String>((ref, query) async {
  final term = query.trim();
  if (term.isEmpty) {
    return const [];
  }

  final data = await ref.watch(apiClientProvider).get(
        '/api/users',
        query: {'search': term, 'limit': 20},
      );
  final page = PagedResult.fromJson(data, UserSummary.fromJson);
  return page.items;
});
