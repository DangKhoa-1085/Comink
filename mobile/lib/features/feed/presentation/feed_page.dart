import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/theme/theme_controller.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/feed_providers.dart';
import '../domain/post.dart';

/// Tab Bảng tin: bài của bạn bè/người theo dõi, đăng bài, thích.
class FeedPage extends ConsumerWidget {
  const FeedPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final feed = ref.watch(feedProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.t('feed_title')),
        actions: [
          IconButton(
            tooltip: l10n.t('profile_theme'),
            onPressed: () => ref
                .read(themeControllerProvider.notifier)
                .toggle(Theme.of(context).brightness),
            icon: Icon(isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => showDialog<void>(
          context: context,
          builder: (_) => const _CreatePostDialog(),
        ),
        icon: const Icon(Icons.edit_outlined),
        label: Text(l10n.t('feed_post')),
      ),
      body: feed.when(
        data: (posts) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(feedProvider);
            await ref.read(feedProvider.future);
          },
          child: posts.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    SizedBox(height: MediaQuery.sizeOf(context).height * 0.2),
                    EmptyState(
                      message: l10n.t('feed_empty'),
                      icon: Icons.dynamic_feed_outlined,
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(12, 8, 12, 96),
                  itemCount: posts.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 10),
                  itemBuilder: (context, index) => _PostCard(post: posts[index]),
                ),
        ),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
          retryLabel: l10n.t('common_retry'),
          onRetry: () => ref.invalidate(feedProvider),
        ),
      ),
    );
  }
}

class _PostCard extends ConsumerWidget {
  const _PostCard({required this.post});

  final Post post;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                AppAvatar(name: post.author.displayName, url: post.author.avatarUrl),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        post.author.displayName,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                      Text(
                        '@${post.author.userName} · ${_formatDate(context, post.createdAt)}',
                        style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                _VisibilityChip(visibility: post.visibility),
              ],
            ),
            if (post.content != null) ...[
              const SizedBox(height: 12),
              Text(post.content!, style: const TextStyle(fontSize: 15, height: 1.35)),
            ],
            if (post.media.isNotEmpty) ...[
              const SizedBox(height: 10),
              ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Image.network(
                  post.media.first.url,
                  fit: BoxFit.cover,
                  height: 190,
                  width: double.infinity,
                  errorBuilder: (_, _, _) => Container(
                    height: 120,
                    color: colors.surfaceContainerHighest,
                    alignment: Alignment.center,
                    child: Icon(Icons.broken_image_outlined, color: colors.outline),
                  ),
                ),
              ),
            ],
            if (post.sharedPost != null) ...[
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  border: Border.all(color: colors.outlineVariant),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      post.sharedPost!.author.displayName,
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                    ),
                    if (post.sharedPost!.content != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        post.sharedPost!.content!,
                        maxLines: 4,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 13),
                      ),
                    ],
                  ],
                ),
              ),
            ],
            const SizedBox(height: 8),
            Divider(color: colors.outlineVariant.withValues(alpha: 0.5), height: 16),
            Row(
              children: [
                _ActionButton(
                  icon: post.likedByMe ? Icons.favorite : Icons.favorite_border,
                  label: post.likeCount > 0 ? '${post.likeCount}' : l10n.t('feed_like'),
                  active: post.likedByMe,
                  onTap: () => _toggleLike(context, ref),
                ),
                const SizedBox(width: 8),
                _ActionButton(
                  icon: Icons.mode_comment_outlined,
                  label: post.commentCount > 0
                      ? '${post.commentCount}'
                      : l10n.t('feed_comment'),
                  onTap: () => _showComingSoon(context),
                ),
                const SizedBox(width: 8),
                _ActionButton(
                  icon: Icons.share_outlined,
                  label: post.shareCount > 0 ? '${post.shareCount}' : l10n.t('feed_share'),
                  onTap: () => _showComingSoon(context),
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
      ref.invalidate(feedProvider);
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  void _showComingSoon(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(l10n.t('common_coming_soon'))),
    );
  }

  static String _formatDate(BuildContext context, DateTime date) {
    final localizations = MaterialLocalizations.of(context);
    final time = localizations.formatTimeOfDay(
      TimeOfDay.fromDateTime(date),
      alwaysUse24HourFormat: MediaQuery.alwaysUse24HourFormatOf(context),
    );
    return '${localizations.formatShortDate(date)} $time';
  }
}

class _VisibilityChip extends StatelessWidget {
  const _VisibilityChip({required this.visibility});

  final String visibility;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final (icon, label) = switch (visibility) {
      'Public' => (Icons.public, l10n.t('feed_public')),
      'OnlyMe' => (Icons.lock_outline, l10n.t('feed_only_me')),
      _ => (Icons.group_outlined, l10n.t('feed_friends')),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: colors.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: colors.onSurfaceVariant),
          const SizedBox(width: 4),
          Text(
            label,
            style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
    this.active = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Expanded(
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 18, color: active ? colors.error : colors.onSurfaceVariant),
              const SizedBox(width: 6),
              Text(
                label,
                style: TextStyle(
                  fontSize: 13,
                  color: active ? colors.error : colors.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CreatePostDialog extends ConsumerStatefulWidget {
  const _CreatePostDialog();

  @override
  ConsumerState<_CreatePostDialog> createState() => _CreatePostDialogState();
}

class _CreatePostDialogState extends ConsumerState<_CreatePostDialog> {
  final _controller = TextEditingController();
  String _visibility = 'Friends';
  bool _posting = false;
  String? _errorCode;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_posting) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final content = _controller.text.trim();
    if (content.isEmpty) {
      setState(() => _errorCode = 'feed_content_required');
      return;
    }

    setState(() {
      _posting = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).post(
        '/api/posts',
        data: {'content': content, 'visibility': _visibility},
      );
      ref.invalidate(feedProvider);
      if (!mounted) {
        return;
      }
      Navigator.pop(context);
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('feed_created'))));
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _posting = false;
        _errorCode = error.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final errorText = _errorCode == 'feed_content_required'
        ? l10n.t('feed_content_required')
        : (_errorCode == null ? null : l10n.error(_errorCode));

    return AlertDialog(
      title: Text(l10n.t('feed_post')),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: _controller,
            minLines: 3,
            maxLines: 6,
            maxLength: 5000,
            autofocus: true,
            decoration: InputDecoration(
              hintText: l10n.t('feed_composer_hint'),
              errorText: errorText,
            ),
          ),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            initialValue: _visibility,
            decoration: InputDecoration(labelText: l10n.t('feed_visibility')),
            items: [
              DropdownMenuItem(value: 'Public', child: Text(l10n.t('feed_public'))),
              DropdownMenuItem(value: 'Friends', child: Text(l10n.t('feed_friends'))),
              DropdownMenuItem(value: 'OnlyMe', child: Text(l10n.t('feed_only_me'))),
            ],
            onChanged: _posting ? null : (value) => setState(() => _visibility = value ?? 'Friends'),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: _posting ? null : () => Navigator.pop(context),
          child: Text(l10n.t('common_cancel')),
        ),
        FilledButton(
          onPressed: _posting ? null : _submit,
          child: _posting
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : Text(l10n.t('feed_post')),
        ),
      ],
    );
  }
}
