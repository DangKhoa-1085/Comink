import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/friends_providers.dart';
import '../domain/friends_models.dart';

enum _FriendsTab { requests, friends }

/// Tab Bạn bè: tìm kiếm người dùng, lời mời kết bạn và danh sách bạn bè.
class FriendsPage extends ConsumerStatefulWidget {
  const FriendsPage({super.key});

  @override
  ConsumerState<FriendsPage> createState() => _FriendsPageState();
}

class _FriendsPageState extends ConsumerState<FriendsPage> {
  final _searchController = TextEditingController();
  String _query = '';
  _FriendsTab _tab = _FriendsTab.friends;

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
    final colors = Theme.of(context).colorScheme;
    final friends = ref.watch(friendsProvider);
    final incoming = ref.watch(incomingFriendRequestsProvider);
    final requestCount = incoming.asData?.value.length ?? 0;
    final friendCount = friends.asData?.value.length ?? 0;

    return Scaffold(
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
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    l10n.t('friends_title'),
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                ),
                IconButton(
                  tooltip: l10n.t('common_retry'),
                  onPressed: _invalidateLists,
                  icon: const Icon(Icons.refresh),
                ),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => _submitSearch(),
              decoration: InputDecoration(
                hintText: l10n.t('friends_search_hint'),
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _query.isEmpty
                    ? IconButton(
                        tooltip: l10n.t('common_search'),
                        onPressed: _submitSearch,
                        icon: Icon(Icons.arrow_forward, color: colors.primary),
                      )
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
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  ChoiceChip(
                    selected: _tab == _FriendsTab.friends,
                    onSelected: (_) => setState(() => _tab = _FriendsTab.friends),
                    label: Text('${l10n.t('friends_title')} ($friendCount)'),
                  ),
                  ChoiceChip(
                    selected: _tab == _FriendsTab.requests,
                    onSelected: (_) => setState(() => _tab = _FriendsTab.requests),
                    label: Text('${l10n.t('friends_requests_title')} ($requestCount)'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (_tab == _FriendsTab.requests)
                incoming.when(
                  data: (items) => items.isEmpty
                      ? EmptyState(
                          message: l10n.t('friends_requests_empty'),
                          icon: Icons.person_add_alt_1_outlined,
                        )
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
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (error, _) => ErrorState(
                    message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                    retryLabel: l10n.t('common_retry'),
                    onRetry: () => ref.invalidate(incomingFriendRequestsProvider),
                  ),
                )
              else
                friends.when(
                  data: (items) => items.isEmpty
                      ? EmptyState(
                          message: l10n.t('friends_empty'),
                          icon: Icons.people_outline,
                        )
                      : Column(
                          children: items
                              .map((friendship) => _FriendTile(
                                    friendship: friendship,
                                    onUnfriend: () => _confirmUnfriend(friendship),
                                  ))
                              .toList(growable: false),
                        ),
                  loading: () => const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (error, _) => ErrorState(
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
      error: (error, _) => ErrorState(
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
      padding: const EdgeInsets.fromLTRB(4, 4, 4, 8),
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
            Expanded(
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: () => context.push('/users/${request.user.id}'),
                child: Row(
                  children: [
                    AppAvatar(
                      name: request.user.displayName,
                      url: request.user.avatarUrl,
                      radius: 20,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            request.user.displayName,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          Text(
                            '@${request.user.userName}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 12,
                              color: Theme.of(context).colorScheme.onSurfaceVariant,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 8),
            FilledButton(
              onPressed: onAccept,
              style: FilledButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
              ),
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
        onTap: () => context.push('/users/${friendship.user.id}'),
        leading: AppAvatar(name: friendship.user.displayName, url: friendship.user.avatarUrl),
        title: Text(
          friendship.user.displayName,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
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
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: () => context.push('/users/${user.id}'),
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Row(
            children: [
              AppAvatar(name: user.displayName, url: user.avatarUrl, radius: 20),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(user.displayName, style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      '@${user.userName}',
                      style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
                    ),
                    if (statusText.isNotEmpty)
                      Text(
                        statusText,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: colors.primary,
                        ),
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
      ),
    );
  }
}
