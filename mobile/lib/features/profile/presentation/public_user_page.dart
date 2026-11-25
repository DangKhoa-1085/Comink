import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../../feed/data/feed_providers.dart';
import '../../feed/domain/post.dart';
import '../../feed/presentation/comments_sheet.dart';
import '../../friends/data/friends_providers.dart';
import '../data/public_profile_providers.dart';
import '../domain/public_profile.dart';

/// Hồ sơ công khai của người dùng khác: theo dõi, kết bạn, nhắn tin, bài viết.
class PublicUserPage extends ConsumerWidget {
  const PublicUserPage({super.key, required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final profile = ref.watch(publicProfileProvider(userId));

    return Scaffold(
      appBar: AppBar(),
      body: profile.when(
        data: (value) => _PublicProfileView(profile: value),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: error is ApiException && error.code == 'NOT_FOUND'
              ? l10n.t('profile_not_found')
              : l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
          retryLabel: l10n.t('common_retry'),
          onRetry: () => ref.invalidate(publicProfileProvider(userId)),
        ),
      ),
    );
  }
}

class _PublicProfileView extends ConsumerWidget {
  const _PublicProfileView({required this.profile});

  final PublicProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final posts = ref.watch(userPostsProvider(profile.id));
    final isSelf = ref.watch(sessionControllerProvider).user?.id == profile.id;
    final friendship = profile.relationship.friendship;

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(publicProfileProvider(profile.id));
        ref.invalidate(userPostsProvider(profile.id));
        await ref.read(publicProfileProvider(profile.id).future);
        await ref.read(userPostsProvider(profile.id).future);
      },
      child: ListView(
        padding: const EdgeInsets.fromLTRB(14, 0, 14, 24),
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              AppAvatar(name: profile.displayName, url: profile.avatarUrl, radius: 38),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      profile.displayName,
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    Text('@${profile.userName}', style: TextStyle(color: colors.onSurfaceVariant)),
                    if (profile.location != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Row(
                          children: [
                            Icon(Icons.place_outlined, size: 14, color: colors.onSurfaceVariant),
                            const SizedBox(width: 4),
                            Text(
                              profile.location!,
                              style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
          if (profile.bio != null) ...[
            const SizedBox(height: 12),
            Text(profile.bio!),
          ],
          const SizedBox(height: 14),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: [
              _Stat(label: l10n.t('profile_friends'), value: profile.friendCount),
              _Stat(label: l10n.t('profile_followers'), value: profile.followerCount),
              _Stat(label: l10n.t('profile_following'), value: profile.followingCount),
            ],
          ),
          if (!isSelf) ...[
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () => _toggleFollow(context, ref),
                    icon: Icon(
                      profile.relationship.following
                          ? Icons.person_remove_outlined
                          : Icons.person_add_alt,
                    ),
                    label: Text(
                      profile.relationship.following
                          ? l10n.t('profile_following')
                          : l10n.t('profile_follow'),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(child: _friendButton(context, ref, friendship)),
              ],
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: FilledButton.tonalIcon(
                onPressed: friendship == 'Friends' ? () => _openChat(context, ref) : null,
                icon: const Icon(Icons.chat_bubble_outline),
                label: Text(l10n.t('profile_message')),
              ),
            ),
            if (friendship != 'Friends')
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  l10n.t('profile_cannot_message'),
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
                ),
              ),
          ],
          const SizedBox(height: 18),
          Text(
            l10n.t('profile_posts'),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          posts.when(
            data: (items) => items.isEmpty
                ? EmptyState(
                    message: l10n.t('profile_no_posts'),
                    icon: Icons.article_outlined,
                  )
                : Column(
                    children: [
                      for (final post in items)
                        _PublicPostCard(post: post, userId: profile.id),
                    ],
                  ),
            loading: () => const Padding(
              padding: EdgeInsets.all(20),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, _) => ErrorState(
              message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
              retryLabel: l10n.t('common_retry'),
              onRetry: () => ref.invalidate(userPostsProvider(profile.id)),
            ),
          ),
        ],
      ),
    );
  }

  Widget _friendButton(BuildContext context, WidgetRef ref, String friendship) {
    final l10n = AppLocalizations.of(context);
    return switch (friendship) {
      'Friends' => OutlinedButton(
          onPressed: null,
          child: Text(l10n.t('profile_friends')),
        ),
      'OutgoingPending' => OutlinedButton(
          onPressed: null,
          child: Text(l10n.t('profile_friend_requested')),
        ),
      'IncomingPending' => OutlinedButton(
          onPressed: null,
          child: Text(l10n.t('friends_status_incoming')),
        ),
      _ => FilledButton(
          onPressed: () => _sendFriendRequest(context, ref),
          child: Text(l10n.t('profile_add_friend')),
        ),
    };
  }

  Future<void> _toggleFollow(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final api = ref.read(apiClientProvider);
    try {
      if (profile.relationship.following) {
        await api.delete('/api/follows/${profile.id}');
      } else {
        await api.post('/api/follows/${profile.id}');
      }
      ref.invalidate(publicProfileProvider(profile.id));
      ref.invalidate(friendsProvider);
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            l10n.t(profile.relationship.following ? 'profile_unfollowed' : 'profile_followed'),
          ),
        ),
      );
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  Future<void> _sendFriendRequest(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(apiClientProvider).post(
        '/api/friendships/requests',
        data: {'addresseeId': profile.id},
      );
      ref.invalidate(publicProfileProvider(profile.id));
      ref.invalidate(friendsProvider);
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('profile_friend_sent'))));
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  Future<void> _openChat(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final data = await ref.read(apiClientProvider).post(
        '/api/conversations/direct',
        data: {'userId': profile.id},
      );
      if (data is! Map) {
        return;
      }
      final id = (data['id'] ?? '').toString();
      if (id.isNotEmpty && context.mounted) {
        context.push('/chats/$id');
      }
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});

  final String label;
  final int value;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text('$value', style: const TextStyle(fontWeight: FontWeight.w700)),
        Text(
          label,
          style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      ],
    );
  }
}

class _PublicPostCard extends ConsumerWidget {
  const _PublicPostCard({required this.post, required this.userId});

  final Post post;
  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = Theme.of(context).colorScheme;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              MaterialLocalizations.of(context).formatShortDate(post.createdAt),
              style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
            ),
            if (post.content != null) ...[
              const SizedBox(height: 6),
              Text(post.content!),
            ],
            if (post.media.isNotEmpty) ...[
              const SizedBox(height: 8),
              for (final item in post.media)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.network(
                      item.url,
                      height: 170,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => const SizedBox.shrink(),
                    ),
                  ),
                ),
            ],
            if (post.sharedPost != null) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  border: Border.all(color: colors.outlineVariant),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  post.sharedPost!.content ?? '',
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 13),
                ),
              ),
            ],
            const SizedBox(height: 6),
            Row(
              children: [
                TextButton.icon(
                  onPressed: () => _toggleLike(context, ref),
                  icon: Icon(
                    post.likedByMe ? Icons.favorite : Icons.favorite_border,
                    size: 17,
                    color: post.likedByMe ? colors.error : colors.onSurfaceVariant,
                  ),
                  label: Text('${post.likeCount}'),
                ),
                TextButton.icon(
                  onPressed: () => showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    useSafeArea: true,
                    builder: (_) => CommentsSheet(postId: post.id),
                  ).then((_) => ref.invalidate(userPostsProvider(userId))),
                  icon: const Icon(Icons.mode_comment_outlined, size: 17),
                  label: Text('${post.commentCount}'),
                ),
                TextButton.icon(
                  onPressed: null,
                  icon: const Icon(Icons.share_outlined, size: 17),
                  label: Text('${post.shareCount}'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _toggleLike(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final api = ref.read(apiClientProvider);
    try {
      if (post.likedByMe) {
        await api.delete('/api/posts/${post.id}/likes');
      } else {
        await api.post('/api/posts/${post.id}/likes');
      }
      ref.invalidate(userPostsProvider(userId));
      ref.invalidate(feedProvider);
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }
}
