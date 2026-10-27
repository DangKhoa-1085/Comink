import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/friends_providers.dart';
import '../domain/friends_models.dart';

/// Tab Bạn bè: lời mời kết bạn, danh sách bạn, tìm người dùng + theo dõi.
class FriendsPage extends ConsumerStatefulWidget {
  const FriendsPage({super.key});

  @override
  ConsumerState<FriendsPage> createState() => _FriendsPageState();
}

class _FriendsPageState extends ConsumerState<FriendsPage> {
  final _searchController = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _submitSearch() {
    final value = _searchController.text.trim();
    if (value == _query) {
      return;
    }
    setState(() => _query = value);
  }

  void _clearSearch() {
    _searchController.clear();
    setState(() => _query = '');
  }

  void _invalidateLists() {
    ref.invalidate(friendsProvider);
    ref.invalidate(incomingFriendRequestsProvider);
    if (_query.isNotEmpty) {
      ref.invalidate(userSearchProvider(_query));
    }
  }

  Future<void> _runAction(Future<dynamic> Function() action, String successKey) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await action();
      _invalidateLists();
      messenger.showSnackBar(SnackBar(content: Text(l10n.t(successKey))));
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final friends = ref.watch(friendsProvider);
    final incoming = ref.watch(incomingFriendRequestsProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.t('friends_title')),
        actions: [
          IconButton(
            tooltip: l10n.t('common_retry'),
            onPressed: _invalidateLists,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          _invalidateLists();
          await ref.read(friendsProvider.future);
          await ref.read(incomingFriendRequestsProvider.future);
          if (_query.isNotEmpty) {
            await ref.read(userSearchProvider(_query).future);
          }
        },
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
          children: [
            TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => _submitSearch(),
              decoration: InputDecoration(
                hintText: l10n.t('friends_search_hint'),
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _query.isEmpty
                    ? null
                    : IconButton(
                        onPressed: _clearSearch,
                        icon: const Icon(Icons.close),
                      ),
              ),
            ),
            const SizedBox(height: 12),
            if (_query.isNotEmpty)
              _buildSearchResults(l10n)
            else ...[
              _SectionHeader(
                icon: Icons.person_add_alt_1_outlined,
                title: l10n.t('friends_requests_title'),
              ),
              incoming.when(
                data: (items) => items.isEmpty
                    ? _EmptyLine(text: l10n.t('friends_requests_empty'))
                    : Column(
                        children: items
                            .map((request) => _IncomingRequestTile(
                                  request: request,
                                  onAccept: () => _runAction(
                                    () => ref.read(apiClientProvider).post(
                                          '/api/friendships/requests/${request.id}/accept',
                                        ),
                                    'friends_accepted',
                                  ),
                                  onDecline: () => _runAction(
                                    () => ref.read(apiClientProvider).delete(
                                          '/api/friendships/requests/${request.id}',
                                        ),
                                    'friends_declined',
                                  ),
                                ))
                            .toList(growable: false),
                      ),
                loading: () => const Padding(
                  padding: EdgeInsets.all(16),
                  child: Center(child: CircularProgressIndicator()),
                ),
                error: (error, _) => _InlineError(
                  message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                  retryLabel: l10n.t('common_retry'),
                  onRetry: () => ref.invalidate(incomingFriendRequestsProvider),
                ),
              ),
              const SizedBox(height: 12),
              _SectionHeader(icon: Icons.people_outline, title: l10n.t('friends_title')),
              friends.when(
                data: (items) => items.isEmpty
                    ? _EmptyLine(text: l10n.t('friends_empty'))
                    : Column(
                        children: items
                            .map((friendship) => _FriendTile(
                                  friendship: friendship,
                                  onUnfriend: () => _confirmUnfriend(friendship),
                                ))
                            .toList(growable: false),
                      ),
                loading: () => const Padding(
                  padding: EdgeInsets.all(16),
                  child: Center(child: CircularProgressIndicator()),
                ),
                error: (error, _) => _InlineError(
                  message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                  retryLabel: l10n.t('common_retry'),
                  onRetry: () => ref.invalidate(friendsProvider),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildSearchResults(AppLocalizations l10n) {
    final results = ref.watch(userSearchProvider(_query));

    return results.when(
      data: (users) {
        if (users.isEmpty) {
          return EmptyState(
            message: l10n.t('friends_search_empty'),
            icon: Icons.search_off,
          );
        }

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _SectionHeader(
              icon: Icons.search,
              title: l10n.t('friends_search_results'),
            ),
            ...users.map(
              (user) => _SearchResultTile(
                user: user,
                onAddFriend: () => _runAction(
                  () => ref.read(apiClientProvider).post(
                        '/api/friendships/requests',
                        data: {'addresseeId': user.id},
                      ),
                  'friends_sent',
                ),
                onToggleFollow: () => _runAction(
                  () => user.relationship.following
                      ? ref.read(apiClientProvider).delete('/api/follows/${user.id}')
                      : ref.read(apiClientProvider).post('/api/follows/${user.id}'),
                  user.relationship.following ? 'friends_unfollowed' : 'friends_followed',
                ),
              ),
            ),
          ],
        );
      },
      loading: () => const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => _InlineError(
        message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
        retryLabel: l10n.t('common_retry'),
        onRetry: () => ref.invalidate(userSearchProvider(_query)),
      ),
    );
  }

  Future<void> _confirmUnfriend(FriendshipSummary friendship) async {
    final l10n = AppLocalizations.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('friends_unfriend')),
        content: Text(l10n.t('friends_unfriend_confirm')),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: Text(l10n.t('common_cancel')),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(l10n.t('friends_unfriend')),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await _runAction(
        () => ref.read(apiClientProvider).delete('/api/friendships/${friendship.user.id}'),
        'friends_unfriended',
      );
    }
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.icon, required this.title});

  final IconData icon;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 8, 4, 6),
      child: Row(
        children: [
          Icon(icon, size: 18, color: Theme.of(context).colorScheme.primary),
          const SizedBox(width: 6),
          Text(
            title,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}

class _EmptyLine extends StatelessWidget {
  const _EmptyLine({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
      child: Text(
        text,
        style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
      ),
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(message, style: TextStyle(color: Theme.of(context).colorScheme.error))),
        TextButton(onPressed: onRetry, child: Text(retryLabel)),
      ],
    );
  }
}

class _IncomingRequestTile extends StatelessWidget {
  const _IncomingRequestTile({
    required this.request,
    required this.onAccept,
    required this.onDecline,
  });

  final FriendshipSummary request;
  final VoidCallback onAccept;
  final VoidCallback onDecline;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(10),
        child: Row(
          children: [
            AppAvatar(name: request.user.displayName, url: request.user.avatarUrl, radius: 18),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(request.user.displayName, style: const TextStyle(fontWeight: FontWeight.w600)),
                  Text(
                    '@${request.user.userName}',
                    style: TextStyle(
                      fontSize: 12,
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            FilledButton.tonal(
              onPressed: onAccept,
              child: Text(l10n.t('friends_accept')),
            ),
            IconButton(
              tooltip: l10n.t('friends_decline'),
              onPressed: onDecline,
              icon: const Icon(Icons.close),
            ),
          ],
        ),
      ),
    );
  }
}

class _FriendTile extends StatelessWidget {
  const _FriendTile({required this.friendship, required this.onUnfriend});

  final FriendshipSummary friendship;
  final VoidCallback onUnfriend;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: AppAvatar(name: friendship.user.displayName, url: friendship.user.avatarUrl),
        title: Text(friendship.user.displayName),
        subtitle: Text('@${friendship.user.userName}'),
        trailing: PopupMenuButton<String>(
          onSelected: (value) {
            if (value == 'unfriend') {
              onUnfriend();
            }
          },
          itemBuilder: (context) => [
            PopupMenuItem(
              value: 'unfriend',
              child: Text(l10n.t('friends_unfriend')),
            ),
          ],
        ),
      ),
    );
  }
}

class _SearchResultTile extends StatelessWidget {
  const _SearchResultTile({
    required this.user,
    required this.onAddFriend,
    required this.onToggleFollow,
  });

  final UserSummary user;
  final VoidCallback onAddFriend;
  final VoidCallback onToggleFollow;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final friendship = user.relationship.friendship;
    final (statusText, showAdd) = switch (friendship) {
      'Friends' => (l10n.t('friends_status_friends'), false),
      'OutgoingPending' => (l10n.t('friends_status_outgoing'), false),
      'IncomingPending' => (l10n.t('friends_status_incoming'), false),
      _ => ('', true),
    };

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(10),
        child: Row(
          children: [
            AppAvatar(name: user.displayName, url: user.avatarUrl, radius: 18),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(user.displayName, style: const TextStyle(fontWeight: FontWeight.w600)),
                  Text(
                    '@${user.userName}',
                    style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
                  ),
                  if (statusText.isNotEmpty)
                    Text(
                      statusText,
                      style: TextStyle(fontSize: 11, color: colors.primary),
                    ),
                ],
              ),
            ),
            if (showAdd)
              FilledButton.tonal(
                onPressed: onAddFriend,
                child: Text(l10n.t('friends_add')),
              ),
            IconButton(
              tooltip: user.relationship.following
                  ? l10n.t('friends_following')
                  : l10n.t('friends_follow'),
              onPressed: onToggleFollow,
              icon: Icon(
                user.relationship.following ? Icons.person_remove_outlined : Icons.person_add_alt,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
