import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/config/app_config.dart';
import '../../../core/i18n/app_localizations.dart';
import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../media/data/media_service.dart';
import '../data/feed_providers.dart';
import '../domain/post.dart';
import 'comments_sheet.dart';

/// Tab Bảng tin: bài của bạn bè/người theo dõi, đăng bài, thích.
class FeedPage extends ConsumerStatefulWidget {
  const FeedPage({super.key});

  @override
  ConsumerState<FeedPage> createState() => _FeedPageState();
}

class _FeedPageState extends ConsumerState<FeedPage> {
  final List<Post> _morePosts = [];
  String? _nextCursor;
  bool _hasMore = false;
  bool _loadingMore = false;

  Future<void> _loadMore(FeedHistory history) async {
    final cursor = _nextCursor ?? history.nextCursor;
    final canLoad = _morePosts.isEmpty ? history.hasMore : _hasMore;
    if (cursor == null || !canLoad || _loadingMore) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _loadingMore = true);

    try {
      final data = await ref.read(apiClientProvider).get(
            '/api/feed',
            query: {'cursor': cursor, 'limit': 20},
          );
      final page = PagedResult.fromJson(data, Post.fromJson);
      if (!mounted) {
        return;
      }
      setState(() {
        _morePosts.addAll(page.items);
        _nextCursor = page.nextCursor;
        _hasMore = page.hasMore;
        _loadingMore = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() => _loadingMore = false);
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  Future<void> _refresh() async {
    setState(() {
      _morePosts.clear();
      _nextCursor = null;
      _hasMore = false;
    });
    ref.invalidate(feedProvider);
    await ref.read(feedProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final feed = ref.watch(feedProvider);

    Widget buildHeader() => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    l10n.t('feed_title'),
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    l10n.t('feed_subtitle'),
                    style: TextStyle(fontSize: 12.5, color: colors.onSurfaceVariant),
                  ),
                ],
              ),
            ),
            Card(
              child: InkWell(
                borderRadius: BorderRadius.circular(18),
                onTap: () => showDialog<void>(
                  context: context,
                  builder: (_) => const _CreatePostDialog(),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Row(
                    children: [
                      Container(
                        width: 38,
                        height: 38,
                        decoration: BoxDecoration(
                          color: colors.primaryContainer,
                          shape: BoxShape.circle,
                        ),
                        child: Icon(Icons.edit_outlined, size: 18, color: colors.primary),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                          decoration: BoxDecoration(
                            color: colors.surfaceContainerHighest,
                            borderRadius: BorderRadius.circular(24),
                          ),
                          child: Text(
                            l10n.t('feed_composer_hint'),
                            style: TextStyle(color: colors.onSurfaceVariant, fontSize: 13.5),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Icon(Icons.photo_library_outlined, color: colors.primary),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
          ],
        );

    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => showDialog<void>(
          context: context,
          builder: (_) => const _CreatePostDialog(),
        ),
        icon: const Icon(Icons.edit_outlined),
        label: Text(l10n.t('feed_post')),
      ),
      body: feed.when(
        skipLoadingOnRefresh: true,
        data: (history) {
          final merged = <String, Post>{
            for (final post in [...history.posts, ..._morePosts]) post.id: post,
          }.values.toList(growable: false);
          final canLoad = _morePosts.isEmpty ? history.hasMore : _hasMore;

          return RefreshIndicator(
            onRefresh: _refresh,
            child: NotificationListener<ScrollNotification>(
              onNotification: (notification) {
                if (canLoad &&
                    !_loadingMore &&
                    notification.metrics.extentAfter < 300 &&
                    notification.metrics.axis == Axis.vertical) {
                  _loadMore(history);
                }
                return false;
              },
              child: merged.isEmpty
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.fromLTRB(12, 12, 12, 96),
                      children: [
                        buildHeader(),
                        SizedBox(height: MediaQuery.sizeOf(context).height * 0.14),
                        EmptyState(
                          message: l10n.t('feed_empty'),
                          icon: Icons.dynamic_feed_outlined,
                        ),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.fromLTRB(12, 12, 12, 96),
                      itemCount: merged.length + 1 + (canLoad ? 1 : 0),
                      separatorBuilder: (_, index) =>
                          index == 0 ? const SizedBox.shrink() : const SizedBox(height: 10),
                      itemBuilder: (context, index) {
                        if (index == 0) {
                          return buildHeader();
                        }
                        final postIndex = index - 1;
                        if (postIndex == merged.length) {
                          return Center(
                            child: _loadingMore
                                ? const Padding(
                                    padding: EdgeInsets.all(8),
                                    child: SizedBox(
                                      height: 18,
                                      width: 18,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    ),
                                  )
                                : TextButton(
                                    onPressed: () => _loadMore(history),
                                    child: Text(l10n.t('feed_load_more')),
                                  ),
                          );
                        }
                        return _PostCard(post: merged[postIndex]);
                      },
                    ),
            ),
          );
        },
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
                GestureDetector(
                  onTap: () => context.push('/users/${post.author.id}'),
                  child: AppAvatar(name: post.author.displayName, url: post.author.avatarUrl),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: GestureDetector(
                    onTap: () => context.push('/users/${post.author.id}'),
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
              _PostImages(media: post.media),
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
                    GestureDetector(
                      onTap: () => context.push('/users/${post.sharedPost!.author.id}'),
                      child: Text(
                        post.sharedPost!.author.displayName,
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                          color: colors.primary,
                        ),
                      ),
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
                  icon: post.likedByMe ? Icons.thumb_up : Icons.thumb_up_outlined,
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
                  onTap: () => _showComments(context),
                ),
                const SizedBox(width: 8),
                _ActionButton(
                  icon: Icons.share_outlined,
                  label: post.shareCount > 0 ? '${post.shareCount}' : l10n.t('feed_share'),
                  onTap: () => _confirmShare(context, ref),
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

  void _showComments(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => CommentsSheet(postId: post.id),
    );
  }

  Future<void> _confirmShare(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        content: Text(l10n.t('feed_share_confirm')),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: Text(l10n.t('common_cancel')),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(l10n.t('feed_share')),
          ),
        ],
      ),
    );

    if (confirmed != true) {
      return;
    }

    try {
      await ref.read(apiClientProvider).post(
        '/api/posts',
        data: {'sharedPostId': post.id},
      );
      ref.invalidate(feedProvider);
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('feed_shared'))));
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
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

class _PostImages extends StatelessWidget {
  const _PostImages({required this.media});

  final List<PostMediaItem> media;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    Widget image(PostMediaItem item) => ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: Image.network(
            AppConfig.resolveMediaUrl(item.url),
            fit: BoxFit.cover,
            errorBuilder: (_, _, _) => Container(
              color: colors.surfaceContainerHighest,
              alignment: Alignment.center,
              child: Icon(Icons.broken_image_outlined, color: colors.outline),
            ),
          ),
        );

    if (media.length == 1) {
      return SizedBox(height: 210, width: double.infinity, child: image(media.first));
    }

    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: media.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 6,
        crossAxisSpacing: 6,
      ),
      itemBuilder: (context, index) => image(media[index]),
    );
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
              Icon(icon, size: 18, color: active ? colors.primary : colors.onSurfaceVariant),
              const SizedBox(width: 6),
              Text(
                label,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: active ? FontWeight.w600 : FontWeight.w400,
                  color: active ? colors.primary : colors.onSurfaceVariant,
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

class _PickedImage {
  const _PickedImage({required this.file, required this.bytes});

  final XFile file;
  final Uint8List bytes;
}

class _CreatePostDialogState extends ConsumerState<_CreatePostDialog> {
  final _controller = TextEditingController();
  final List<_PickedImage> _images = [];
  String _visibility = 'Friends';
  bool _posting = false;
  bool _uploading = false;
  String? _errorCode;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _pickImages() async {
    if (_posting) {
      return;
    }

    final remaining = MediaService.maxImagesPerPost - _images.length;
    if (remaining <= 0) {
      setState(() => _errorCode = 'feed_too_many_images');
      return;
    }

    try {
      final files = await ImagePicker().pickMultiImage(imageQuality: 85, maxWidth: 1920);
      if (files.isEmpty) {
        return;
      }

      for (final file in files.take(remaining)) {
        final bytes = await file.readAsBytes();
        if (bytes.length > MediaService.maxImageBytes) {
          setState(() => _errorCode = 'feed_image_too_large');
          continue;
        }
        setState(() {
          _images.add(_PickedImage(file: file, bytes: bytes));
          _errorCode = null;
        });
      }

      if (files.length > remaining) {
        setState(() => _errorCode = 'feed_too_many_images');
      }
    } catch (_) {
      if (mounted) {
        setState(() => _errorCode = 'INTERNAL_ERROR');
      }
    }
  }

  Future<void> _submit() async {
    if (_posting) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final content = _controller.text.trim();
    if (content.isEmpty && _images.isEmpty) {
      setState(() => _errorCode = 'feed_content_required');
      return;
    }

    setState(() {
      _posting = true;
      _errorCode = null;
    });

    try {
      final mediaIds = <String>[];
      if (_images.isNotEmpty) {
        setState(() => _uploading = true);
        mediaIds.addAll(
          await ref.read(mediaServiceProvider).uploadAll(
                _images.map((image) => image.file).toList(growable: false),
                context: 'Post',
              ),
        );
        if (mounted) {
          setState(() => _uploading = false);
        }
      }

      await ref.read(apiClientProvider).post(
        '/api/posts',
        data: {
          'content': content.isEmpty ? null : content,
          'visibility': _visibility,
          'mediaIds': mediaIds,
        },
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
        _uploading = false;
        _errorCode = error.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final errorText = switch (_errorCode) {
      'feed_content_required' => l10n.t('feed_content_required'),
      'feed_too_many_images' => l10n.t('feed_too_many_images'),
      'feed_image_too_large' => l10n.t('feed_image_too_large'),
      null => null,
      final code => l10n.error(code),
    };

    return AlertDialog(
      title: Text(l10n.t('feed_post')),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextField(
                controller: _controller,
                minLines: 3,
                maxLines: 6,
                maxLength: 5000,
                autofocus: true,
                enabled: !_posting,
                decoration: InputDecoration(
                  hintText: l10n.t('feed_composer_hint'),
                  errorText: errorText,
                ),
              ),
              if (_images.isNotEmpty) ...[
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (var index = 0; index < _images.length; index++)
                      Stack(
                        children: [
                          ClipRRect(
                            borderRadius: BorderRadius.circular(10),
                            child: Image.memory(
                              _images[index].bytes,
                              width: 78,
                              height: 78,
                              fit: BoxFit.cover,
                            ),
                          ),
                          Positioned(
                            top: 2,
                            right: 2,
                            child: GestureDetector(
                              onTap: _posting
                                  ? null
                                  : () => setState(() => _images.removeAt(index)),
                              child: Container(
                                padding: const EdgeInsets.all(2),
                                decoration: BoxDecoration(
                                  color: Colors.black54,
                                  borderRadius: BorderRadius.circular(10),
                                ),
                                child: const Icon(Icons.close, size: 14, color: Colors.white),
                              ),
                            ),
                          ),
                        ],
                      ),
                  ],
                ),
              ],
              const SizedBox(height: 8),
              Row(
                children: [
                  OutlinedButton.icon(
                    onPressed: _posting || _uploading ? null : _pickImages,
                    icon: const Icon(Icons.image_outlined, size: 18),
                    label: Text(l10n.t('feed_add_image')),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    '${_images.length}/${MediaService.maxImagesPerPost}',
                    style: TextStyle(
                      fontSize: 12,
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
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
                onChanged: _posting
                    ? null
                    : (value) => setState(() => _visibility = value ?? 'Friends'),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _posting ? null : () => Navigator.pop(context),
          child: Text(l10n.t('common_cancel')),
        ),
        FilledButton(
          onPressed: _posting ? null : _submit,
          child: _posting
              ? Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const SizedBox(
                      height: 16,
                      width: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                    const SizedBox(width: 8),
                    Text(_uploading ? l10n.t('feed_uploading') : l10n.t('feed_posting')),
                  ],
                )
              : Text(l10n.t('feed_post')),
        ),
      ],
    );
  }
}
