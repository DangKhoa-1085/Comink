import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/comment_providers.dart';
import '../data/feed_providers.dart';
import '../domain/comment.dart';

/// Bottom sheet xem và gửi bình luận/trả lời cho một bài viết.
class CommentsSheet extends ConsumerStatefulWidget {
  const CommentsSheet({super.key, required this.postId});

  final String postId;

  @override
  ConsumerState<CommentsSheet> createState() => _CommentsSheetState();
}

class _CommentsSheetState extends ConsumerState<CommentsSheet> {
  final _controller = TextEditingController();
  PostComment? _replyTo;
  bool _sending = false;
  String? _errorCode;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final content = _controller.text.trim();
    if (content.isEmpty || _sending) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _sending = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).post(
        '/api/posts/${widget.postId}/comments',
        data: {
          'content': content,
          'parentCommentId': _replyTo?.id,
        },
      );
      _controller.clear();
      ref.invalidate(commentsProvider(widget.postId));
      ref.invalidate(feedProvider);
      if (_replyTo != null) {
        ref.invalidate(repliesProvider(_replyTo!.id));
      }
      if (mounted) {
        setState(() {
          _sending = false;
          _replyTo = null;
        });
      }
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('comment_added'))));
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _sending = false;
        _errorCode = error.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final comments = ref.watch(commentsProvider(widget.postId));

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SizedBox(
        height: MediaQuery.sizeOf(context).height * 0.72,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 8, 8),
              child: Row(
                children: [
                  Text(
                    l10n.t('comments_title'),
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const Spacer(),
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                    tooltip: l10n.t('common_close'),
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: comments.when(
                data: (items) => items.isEmpty
                    ? EmptyState(
                        message: l10n.t('comments_empty'),
                        icon: Icons.mode_comment_outlined,
                      )
                    : ListView.builder(
                        padding: const EdgeInsets.all(12),
                        itemCount: items.length,
                        itemBuilder: (context, index) => _CommentTile(
                          comment: items[index],
                          onReply: () => setState(() => _replyTo = items[index]),
                        ),
                      ),
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => ErrorState(
                  message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                  retryLabel: l10n.t('common_retry'),
                  onRetry: () => ref.invalidate(commentsProvider(widget.postId)),
                ),
              ),
            ),
            if (_replyTo != null)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                color: Theme.of(context).colorScheme.surfaceContainerHighest,
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        '${l10n.t('comment_reply_to')} ${_replyTo!.author.displayName}',
                        style: const TextStyle(fontSize: 12),
                      ),
                    ),
                    IconButton(
                      onPressed: () => setState(() => _replyTo = null),
                      icon: const Icon(Icons.close, size: 16),
                    ),
                  ],
                ),
              ),
            SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 6, 12, 10),
                child: Column(
                  children: [
                    if (_errorCode != null)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 4),
                        child: Text(
                          l10n.error(_errorCode),
                          style: TextStyle(
                            fontSize: 12,
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ),
                    Row(
                      children: [
                        Expanded(
                          child: TextField(
                            controller: _controller,
                            minLines: 1,
                            maxLines: 3,
                            textInputAction: TextInputAction.send,
                            onSubmitted: (_) => _send(),
                            decoration: InputDecoration(
                              hintText: l10n.t('comment_placeholder'),
                              contentPadding:
                                  const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        IconButton.filled(
                          onPressed: _sending ? null : _send,
                          icon: _sending
                              ? const SizedBox(
                                  height: 18,
                                  width: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.send),
                          tooltip: l10n.t('comment_send'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CommentTile extends ConsumerStatefulWidget {
  const _CommentTile({required this.comment, required this.onReply});

  final PostComment comment;
  final VoidCallback onReply;

  @override
  ConsumerState<_CommentTile> createState() => _CommentTileState();
}

class _CommentTileState extends ConsumerState<_CommentTile> {
  bool _showReplies = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final comment = widget.comment;
    final colors = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AppAvatar(name: comment.author.displayName, url: comment.author.avatarUrl, radius: 16),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        comment.author.displayName,
                        style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                      ),
                    ),
                    Text(
                      MaterialLocalizations.of(context).formatTimeOfDay(
                        TimeOfDay.fromDateTime(comment.createdAt),
                        alwaysUse24HourFormat: MediaQuery.alwaysUse24HourFormatOf(context),
                      ),
                      style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
                Text(comment.content, style: const TextStyle(fontSize: 14)),
                Row(
                  children: [
                    TextButton(
                      onPressed: widget.onReply,
                      style: TextButton.styleFrom(
                        padding: EdgeInsets.zero,
                        minimumSize: const Size(0, 28),
                        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      ),
                      child: Text(l10n.t('comment_reply'), style: const TextStyle(fontSize: 12)),
                    ),
                    if (comment.replyCount > 0)
                      TextButton(
                        onPressed: () => setState(() => _showReplies = !_showReplies),
                        style: TextButton.styleFrom(
                          padding: EdgeInsets.zero,
                          minimumSize: const Size(0, 28),
                          tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                        ),
                        child: Text(
                          '${comment.replyCount} ${l10n.t('comment_replies')}',
                          style: const TextStyle(fontSize: 12),
                        ),
                      ),
                  ],
                ),
                if (_showReplies) _RepliesList(commentId: comment.id),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _RepliesList extends ConsumerWidget {
  const _RepliesList({required this.commentId});

  final String commentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final replies = ref.watch(repliesProvider(commentId));

    return replies.when(
      data: (items) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final reply in items)
            Padding(
              padding: const EdgeInsets.only(left: 8, top: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  AppAvatar(name: reply.author.displayName, url: reply.author.avatarUrl, radius: 11),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          reply.author.displayName,
                          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 11),
                        ),
                        Text(reply.content, style: const TextStyle(fontSize: 13)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
      loading: () => const Padding(
        padding: EdgeInsets.all(6),
        child: SizedBox(
          height: 14,
          width: 14,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      ),
      error: (_, _) => Text(
        l10n.t('common_error'),
        style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.error),
      ),
    );
  }
}
