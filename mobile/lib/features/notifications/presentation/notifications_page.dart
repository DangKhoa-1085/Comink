import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../chats/data/chat_providers.dart';
import '../../friends/data/friends_providers.dart';
import '../data/notification_providers.dart';
import '../domain/app_notification.dart';

enum _NotificationFilter { all, unread }

/// Hộp thư thông báo kiểu Facebook: bộ lọc Tất cả/Chưa đọc, phân nhóm Mới/Trước đó, avatar + huy hiệu loại.
class NotificationsPage extends ConsumerStatefulWidget {
  const NotificationsPage({super.key});

  @override
  ConsumerState<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends ConsumerState<NotificationsPage> {
  _NotificationFilter _filter = _NotificationFilter.all;
  bool _markingAll = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final notifications = ref.watch(notificationsProvider);
    final itemsList = notifications.asData?.value ?? const <AppNotification>[];
    final unreadItems = itemsList.where((item) => !item.isRead).toList(growable: false);

    return Scaffold(
      appBar: AppBar(
        title: Text(
          l10n.t('notifications_title'),
          style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
        ),
        shape: Border(bottom: BorderSide(color: colors.outlineVariant)),
        actions: [
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_horiz),
            onSelected: (value) async {
              if (value == 'mark_all' && unreadItems.isNotEmpty) {
                await _markAllRead(unreadItems);
              } else if (value == 'refresh') {
                ref.invalidate(notificationsProvider);
              }
            },
            itemBuilder: (context) => [
              PopupMenuItem(
                value: 'mark_all',
                enabled: unreadItems.isNotEmpty && !_markingAll,
                child: Row(
                  children: [
                    Icon(Icons.done_all, size: 18, color: colors.primary),
                    const SizedBox(width: 8),
                    Text(l10n.t('notifications_mark_all_read')),
                  ],
                ),
              ),
              PopupMenuItem(
                value: 'refresh',
                child: Row(
                  children: [
                    const Icon(Icons.refresh, size: 18),
                    const SizedBox(width: 8),
                    Text(l10n.t('notifications_refresh')),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Row(
              children: [
                _FilterPill(
                  label: l10n.t('notifications_filter_all'),
                  selected: _filter == _NotificationFilter.all,
                  onTap: () => setState(() => _filter = _NotificationFilter.all),
                ),
                const SizedBox(width: 8),
                _FilterPill(
                  label: l10n.t('notifications_filter_unread'),
                  selected: _filter == _NotificationFilter.unread,
                  onTap: () => setState(() => _filter = _NotificationFilter.unread),
                ),
              ],
            ),
          ),
          Expanded(
            child: notifications.when(
              data: (items) {
                final filtered = _filter == _NotificationFilter.unread
                    ? items.where((item) => !item.isRead).toList(growable: false)
                    : items;

                if (filtered.isEmpty) {
                  return RefreshIndicator(
                    onRefresh: () async {
                      ref.invalidate(notificationsProvider);
                      await ref.read(notificationsProvider.future);
                    },
                    child: ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        SizedBox(height: MediaQuery.sizeOf(context).height * 0.18),
                        EmptyState(
                          message: _filter == _NotificationFilter.unread
                              ? l10n.t('notifications_empty_unread')
                              : l10n.t('notifications_empty'),
                          icon: Icons.notifications_none,
                        ),
                      ],
                    ),
                  );
                }

                final now = DateTime.now();
                final recent = <AppNotification>[];
                final earlier = <AppNotification>[];
                for (final item in filtered) {
                  final ageHours = now.difference(item.createdAt).inHours;
                  if (!item.isRead || ageHours < 24) {
                    recent.add(item);
                  } else {
                    earlier.add(item);
                  }
                }

                return RefreshIndicator(
                  onRefresh: () async {
                    ref.invalidate(notificationsProvider);
                    await ref.read(notificationsProvider.future);
                  },
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(8, 4, 8, 24),
                    children: [
                      if (recent.isNotEmpty) ...[
                        _SectionHeader(
                          title: l10n.t('notifications_section_new'),
                          actionLabel: _filter == _NotificationFilter.unread
                              ? l10n.t('notifications_view_all')
                              : (unreadItems.isNotEmpty
                                  ? l10n.t('notifications_mark_all_read')
                                  : null),
                          onAction: _filter == _NotificationFilter.unread
                              ? () => setState(() => _filter = _NotificationFilter.all)
                              : (unreadItems.isNotEmpty
                                  ? () => _markAllRead(unreadItems)
                                  : null),
                        ),
                        for (final item in recent)
                          _buildTile(context, item),
                      ],
                      if (earlier.isNotEmpty) ...[
                        _SectionHeader(
                          title: l10n.t('notifications_section_earlier'),
                          actionLabel: recent.isEmpty && _filter == _NotificationFilter.unread
                              ? l10n.t('notifications_view_all')
                              : null,
                          onAction: recent.isEmpty && _filter == _NotificationFilter.unread
                              ? () => setState(() => _filter = _NotificationFilter.all)
                              : null,
                        ),
                        for (final item in earlier)
                          _buildTile(context, item),
                      ],
                    ],
                  ),
                );
              },
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorState(
                message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                retryLabel: l10n.t('common_retry'),
                onRetry: () => ref.invalidate(notificationsProvider),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTile(BuildContext context, AppNotification notification) {
    final actor = _resolveCachedActor(notification);
    return _NotificationTile(
      notification: notification,
      actorDisplayName: actor.displayName,
      actorAvatarUrl: actor.avatarUrl,
      onTap: () => _open(context, notification),
    );
  }

  /// Tra tên/avatar từ dữ liệu backend hoặc cache có sẵn (không kích hoạt request mới trong test).
  ({String? displayName, String? avatarUrl}) _resolveCachedActor(AppNotification item) {
    if (item.actorDisplayName != null) {
      return (displayName: item.actorDisplayName, avatarUrl: item.actorAvatarUrl);
    }
    final actorId = item.actorId;
    if (actorId == null || actorId.isEmpty) {
      return (displayName: null, avatarUrl: item.actorAvatarUrl);
    }

    if (ref.exists(conversationsProvider)) {
      final convs = ref.read(conversationsProvider).asData?.value;
      for (final conv in convs ?? const []) {
        if (conv.peer?.userId == actorId) {
          return (
            displayName: conv.peer!.displayName.isNotEmpty
                ? conv.peer!.displayName
                : conv.peer!.userName,
            avatarUrl: conv.peer!.avatarUrl,
          );
        }
      }
    }

    if (ref.exists(friendsProvider)) {
      final friends = ref.read(friendsProvider).asData?.value;
      for (final f in friends ?? const []) {
        if (f.user.id == actorId) {
          return (displayName: f.user.displayName, avatarUrl: f.user.avatarUrl);
        }
      }
    }

    if (ref.exists(incomingFriendRequestsProvider)) {
      final requests = ref.read(incomingFriendRequestsProvider).asData?.value;
      for (final r in requests ?? const []) {
        if (r.user.id == actorId) {
          return (displayName: r.user.displayName, avatarUrl: r.user.avatarUrl);
        }
      }
    }

    return (displayName: null, avatarUrl: item.actorAvatarUrl);
  }

  Future<void> _markAllRead(List<AppNotification> targets) async {
    if (_markingAll) {
      return;
    }
    setState(() => _markingAll = true);
    try {
      final api = ref.read(apiClientProvider);
      await Future.wait([
        for (final item in targets) api.post('/api/notifications/${item.id}/read'),
      ]);
      ref.invalidate(notificationsProvider);
    } on ApiException {
      // Bỏ qua lỗi từng phần; làm mới lại để phản ánh trạng thái mới nhất.
      ref.invalidate(notificationsProvider);
    } finally {
      if (mounted) {
        setState(() => _markingAll = false);
      }
    }
  }

  Future<void> _open(
    BuildContext context,
    AppNotification notification,
  ) async {
    if (!notification.isRead) {
      try {
        await ref.read(apiClientProvider).post(
          '/api/notifications/${notification.id}/read',
        );
        ref.invalidate(notificationsProvider);
      } on ApiException {
        // Vẫn cho phép mở đối tượng dù đánh dấu đọc lỗi.
      }
    }

    if (!context.mounted) {
      return;
    }

    switch (notification.type) {
      case 'FriendRequestReceived':
      case 'FriendRequestAccepted':
        context.go('/friends');
      case 'NewFollower':
        if (notification.actorId != null) {
          context.push('/users/${notification.actorId}');
        } else {
          context.go('/friends');
        }
      case 'MissedCall':
      case 'AddedToGroup':
        final conversationId = notification.conversationId;
        if (conversationId != null) {
          context.push('/chats/$conversationId');
        }
      default:
        if (notification.conversationId != null) {
          context.push('/chats/${notification.conversationId}');
        }
    }
  }
}

class _FilterPill extends StatelessWidget {
  const _FilterPill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Material(
      color: selected ? colors.primaryContainer : Colors.transparent,
      borderRadius: BorderRadius.circular(99),
      child: InkWell(
        borderRadius: BorderRadius.circular(99),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 13.5,
              fontWeight: FontWeight.w700,
              color: selected ? colors.primary : colors.onSurface,
            ),
          ),
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.title,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(10, 6, 10, 4),
      child: Row(
        children: [
          Text(
            title,
            style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w800),
          ),
          const Spacer(),
          if (actionLabel != null && onAction != null)
            TextButton(
              onPressed: onAction,
              style: TextButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                minimumSize: Size.zero,
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
              child: Text(
                actionLabel!,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: colors.primary,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({
    required this.notification,
    required this.onTap,
    this.actorDisplayName,
    this.actorAvatarUrl,
  });

  final AppNotification notification;
  final VoidCallback onTap;
  final String? actorDisplayName;
  final String? actorAvatarUrl;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    final (badgeIcon, badgeColor, actionText, fallbackTitle) = switch (notification.type) {
      'MissedCall' => (
          notification.callType == 'Video' ? Icons.videocam : Icons.call_missed,
          const Color(0xFFF43F5E),
          notification.callType == 'Video'
              ? l10n.t('notification_action_missed_video_call')
              : l10n.t('notification_action_missed_call'),
          l10n.t('notification_missed_call'),
        ),
      'FriendRequestReceived' => (
          Icons.person_add_alt_1,
          const Color(0xFF0284C7),
          l10n.t('notification_action_friend_request'),
          l10n.t('notification_friend_request'),
        ),
      'FriendRequestAccepted' => (
          Icons.people,
          const Color(0xFF10B981),
          l10n.t('notification_action_friend_accepted'),
          l10n.t('notification_friend_accepted'),
        ),
      'NewFollower' => (
          Icons.person_add,
          const Color(0xFF0EA5E9),
          l10n.t('notification_action_new_follower'),
          l10n.t('notification_other'),
        ),
      'PostLiked' => (
          Icons.thumb_up,
          const Color(0xFF0EA5E9),
          l10n.t('notification_action_post_liked'),
          l10n.t('notification_other'),
        ),
      'PostCommented' => (
          Icons.mode_comment,
          const Color(0xFF10B981),
          l10n.t('notification_action_post_commented'),
          l10n.t('notification_other'),
        ),
      'CommentReplied' => (
          Icons.reply,
          const Color(0xFF10B981),
          l10n.t('notification_action_comment_replied'),
          l10n.t('notification_other'),
        ),
      'PostShared' => (
          Icons.share,
          const Color(0xFF0284C7),
          l10n.t('notification_action_post_shared'),
          l10n.t('notification_other'),
        ),
      'Mentioned' => (
          Icons.alternate_email,
          const Color(0xFF0EA5E9),
          l10n.t('notification_action_mentioned'),
          l10n.t('notification_other'),
        ),
      'AddedToGroup' => (
          Icons.groups,
          const Color(0xFF0284C7),
          l10n.t('notification_action_added_to_group'),
          l10n.t('notification_other'),
        ),
      _ => (
          Icons.notifications,
          const Color(0xFF0284C7),
          l10n.t('notification_action_other'),
          l10n.t('notification_other'),
        ),
    };

    final hasActor = actorDisplayName != null && actorDisplayName!.trim().isNotEmpty;
    final relativeTime = _formatRelativeTime(context, l10n, notification.createdAt);

    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 2),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(14),
          color: notification.isRead
              ? Colors.transparent
              : colors.primaryContainer.withValues(alpha: 0.25),
        ),
        child: Row(
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                if (hasActor)
                  AppAvatar(
                    name: actorDisplayName!,
                    url: actorAvatarUrl,
                    radius: 26,
                  )
                else
                  CircleAvatar(
                    radius: 26,
                    backgroundColor: notification.isRead
                        ? colors.surfaceContainerHighest
                        : colors.primaryContainer,
                    child: Icon(
                      badgeIcon,
                      color: notification.isRead ? colors.onSurfaceVariant : colors.primary,
                    ),
                  ),
                Positioned(
                  right: -2,
                  bottom: -2,
                  child: Container(
                    width: 22,
                    height: 22,
                    decoration: BoxDecoration(
                      color: badgeColor,
                      shape: BoxShape.circle,
                      border: Border.all(color: colors.surface, width: 2),
                    ),
                    child: Icon(badgeIcon, size: 12, color: Colors.white),
                  ),
                ),
              ],
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (hasActor)
                    Text.rich(
                      TextSpan(
                        children: [
                          TextSpan(
                            text: actorDisplayName!,
                            style: TextStyle(
                              fontWeight: FontWeight.w800,
                              color: colors.onSurface,
                            ),
                          ),
                          TextSpan(
                            text: ' $actionText',
                            style: TextStyle(color: colors.onSurfaceVariant),
                          ),
                        ],
                      ),
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 13.5, height: 1.3),
                    )
                  else
                    Text(
                      fallbackTitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: notification.isRead ? FontWeight.w500 : FontWeight.w700,
                        color: colors.onSurface,
                      ),
                    ),
                  const SizedBox(height: 3),
                  Text(
                    relativeTime,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: notification.isRead ? FontWeight.w400 : FontWeight.w700,
                      color: notification.isRead ? colors.onSurfaceVariant : colors.primary,
                    ),
                  ),
                ],
              ),
            ),
            if (!notification.isRead) ...[
              const SizedBox(width: 8),
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: colors.primary,
                  shape: BoxShape.circle,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static String _formatRelativeTime(
    BuildContext context,
    AppLocalizations l10n,
    DateTime createdAt,
  ) {
    final diff = DateTime.now().difference(createdAt);
    if (diff.inSeconds < 60) {
      return l10n.t('time_just_now');
    }
    if (diff.inMinutes < 60) {
      return l10n.t('time_minutes', {'count': '${diff.inMinutes}'});
    }
    if (diff.inHours < 24) {
      return l10n.t('time_hours', {'count': '${diff.inHours}'});
    }
    if (diff.inDays < 7) {
      return l10n.t('time_days', {'count': '${diff.inDays}'});
    }
    return MaterialLocalizations.of(context).formatShortDate(createdAt);
  }
}
