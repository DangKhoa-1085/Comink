import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/app_config.dart';
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

/// Hồ sơ công khai của người dùng khác: ảnh bìa thương hiệu, kết bạn, theo dõi, nhắn tin và bài viết.
class PublicUserPage extends ConsumerWidget {
  const PublicUserPage({super.key, required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final profile = ref.watch(publicProfileProvider(userId));

    return Scaffold(
      appBar: AppBar(
        title: Text(
          profile.hasValue ? '@${profile.requireValue.userName}' : l10n.t('profile_title'),
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
        ),
        shape: Border(bottom: BorderSide(color: colors.outlineVariant)),
      ),
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
    final joinedDate = MaterialLocalizations.of(context).formatShortDate(profile.createdAt);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(publicProfileProvider(profile.id));
        ref.invalidate(userPostsProvider(profile.id));
        await ref.read(publicProfileProvider(profile.id).future);
        await ref.read(userPostsProvider(profile.id).future);
      },
      child: ListView(
        padding: const EdgeInsets.fromLTRB(14, 14, 14, 24),
        children: [
          Card(
            clipBehavior: Clip.antiAlias,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Container(
                      height: 136,
                      width: double.infinity,
                      decoration: const BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            Color(0xFF0284C7),
                            Color(0xFF0EA5E9),
                            Color(0xFF06B6D4),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                      ),
                      child: (profile.coverUrl != null && profile.coverUrl!.trim().isNotEmpty)
                          ? Stack(
                              fit: StackFit.expand,
                              children: [
                                Image.network(
                                  AppConfig.resolveMediaUrl(profile.coverUrl!),
                                  fit: BoxFit.cover,
                                  errorBuilder: (_, _, _) => const SizedBox.shrink(),
                                ),
                                DecoratedBox(
                                  decoration: BoxDecoration(
                                    gradient: LinearGradient(
                                      colors: [
                                        Colors.black.withValues(alpha: 0.35),
                                        Colors.transparent,
                                      ],
                                      begin: Alignment.bottomCenter,
                                      end: Alignment.topCenter,
                                    ),
                                  ),
                                ),
                              ],
                            )
                          : null,
                    ),
                    Positioned(
                      right: 12,
                      bottom: 10,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.black.withValues(alpha: 0.25),
                          borderRadius: BorderRadius.circular(99),
                        ),
                        child: Text(
                          '${l10n.t('profile_joined')}: $joinedDate',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 11,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ),
                    Positioned(
                      left: 16,
                      bottom: -36,
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          color: colors.surface,
                          shape: BoxShape.circle,
                        ),
                        child: AppAvatar(
                          name: profile.displayName,
                          url: profile.avatarUrl,
                          radius: 38,
                        ),
                      ),
                    ),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 44, 16, 16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        profile.displayName,
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w800,
                            ),
                      ),
                      Text('@${profile.userName}', style: TextStyle(color: colors.onSurfaceVariant)),
                      if (profile.location != null && profile.location!.trim().isNotEmpty) ...[
                        const SizedBox(height: 6),
                        Row(
                          children: [
                            Icon(Icons.place_outlined, size: 14, color: colors.onSurfaceVariant),
                            const SizedBox(width: 4),
                            Text(
                              profile.location!,
                              style: TextStyle(fontSize: 12.5, color: colors.onSurfaceVariant),
                            ),
                          ],
                        ),
                      ],
                      if (profile.bio != null && profile.bio!.trim().isNotEmpty) ...[
                        const SizedBox(height: 10),
                        Text(profile.bio!, style: const TextStyle(height: 1.35)),
                      ],
                      const SizedBox(height: 14),
                      Row(
                        children: [
                          Expanded(
                            child: _Stat(label: l10n.t('profile_friends'), value: profile.friendCount),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _Stat(
                              label: l10n.t('profile_followers'),
                              value: profile.followerCount,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _Stat(
                              label: l10n.t('profile_following'),
                              value: profile.followingCount,
                            ),
                          ),
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
                                  size: 18,
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
                            icon: const Icon(Icons.chat_bubble_outline, size: 18),
                            label: Text(l10n.t('profile_message')),
                          ),
                        ),
                        if (friendship != 'Friends')
                          Padding(
                            padding: const EdgeInsets.only(top: 6),
                            child: Text(
                              l10n.t('profile_cannot_message'),
                              textAlign: TextAlign.center,
                              style: TextStyle(fontSize: 11.5, color: colors.onSurfaceVariant),
                            ),
                          ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),
          Text(
            l10n.t('profile_posts'),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w800),
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
    final colors = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 6),
      decoration: BoxDecoration(
        color: colors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: colors.outlineVariant),
      ),
      child: Column(
        children: [
          Text(
            '$value',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  color: colors.primary,
                ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 11.5, color: colors.onSurfaceVariant),
          ),
        ],
      ),
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
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              MaterialLocalizations.of(context).formatShortDate(post.createdAt),
              style: TextStyle(fontSize: 11.5, color: colors.onSurfaceVariant),
            ),
            if (post.content != null) ...[
              const SizedBox(height: 6),
              Text(post.content!, style: const TextStyle(height: 1.35)),
            ],
            if (post.media.isNotEmpty) ...[
              const SizedBox(height: 8),
              for (final item in post.media)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(12),
                    child: Image.network(
                      AppConfig.resolveMediaUrl(item.url),
                      height: 180,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => const SizedBox.shrink(),
                    ),
                  ),
                ),
            ],
            if (post.sharedPost != null) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  border: Border.all(color: colors.outlineVariant),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    GestureDetector(
                      onTap: () => context.push('/users/${post.sharedPost!.author.id}'),
                      child: Text(
                        post.sharedPost!.author.displayName,
                        style: TextStyle(
                          fontSize: 12.5,
                          fontWeight: FontWeight.w700,
                          color: colors.primary,
                        ),
                      ),
                    ),
                    if (post.sharedPost!.content != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        post.sharedPost!.content!,
                        maxLines: 3,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 13),
                      ),
                    ],
                  ],
                ),
              ),
            ],
            const SizedBox(height: 6),
            Row(
              children: [
                TextButton.icon(
                  onPressed: () => _toggleLike(context, ref),
                  icon: Icon(
                    post.likedByMe ? Icons.thumb_up : Icons.thumb_up_outlined,
                    size: 17,
                    color: post.likedByMe ? colors.primary : colors.onSurfaceVariant,
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
