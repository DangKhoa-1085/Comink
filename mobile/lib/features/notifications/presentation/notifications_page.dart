import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/states.dart';
import '../data/notification_providers.dart';
import '../domain/app_notification.dart';

/// Hộp thư thông báo xã hội/cuộc gọi; bấm để đánh dấu đã đọc và mở đối tượng.
class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final notifications = ref.watch(notificationsProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.t('notifications_title')),
        actions: [
          IconButton(
            tooltip: l10n.t('common_retry'),
            onPressed: () => ref.invalidate(notificationsProvider),
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: notifications.when(
        data: (items) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(notificationsProvider);
            await ref.read(notificationsProvider.future);
          },
          child: items.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    SizedBox(height: MediaQuery.sizeOf(context).height * 0.2),
                    EmptyState(
                      message: l10n.t('notifications_empty'),
                      icon: Icons.notifications_none,
                    ),
                  ],
                )
              : ListView.separated(
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const Divider(height: 1, indent: 60),
                  itemBuilder: (context, index) => _NotificationTile(
                    notification: items[index],
                    onTap: () => _open(context, ref, items[index]),
                  ),
                ),
        ),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
          retryLabel: l10n.t('common_retry'),
          onRetry: () => ref.invalidate(notificationsProvider),
        ),
      ),
    );
  }

  Future<void> _open(
    BuildContext context,
    WidgetRef ref,
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
      case 'MissedCall':
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

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({required this.notification, required this.onTap});

  final AppNotification notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final (icon, title) = switch (notification.type) {
      'FriendRequestReceived' => (Icons.person_add_alt_1, l10n.t('notification_friend_request')),
      'FriendRequestAccepted' => (Icons.people_outline, l10n.t('notification_friend_accepted')),
      'MissedCall' => (Icons.call_missed, l10n.t('notification_missed_call')),
      _ => (Icons.notifications_outlined, l10n.t('notification_other')),
    };

    return ListTile(
      onTap: onTap,
      leading: CircleAvatar(
        backgroundColor: notification.isRead ? colors.surfaceContainerHighest : colors.primaryContainer,
        child: Icon(
          icon,
          color: notification.isRead ? colors.onSurfaceVariant : colors.onPrimaryContainer,
        ),
      ),
      title: Text(
        title,
        style: TextStyle(
          fontWeight: notification.isRead ? FontWeight.w500 : FontWeight.w700,
        ),
      ),
      subtitle: Text(
        MaterialLocalizations.of(context).formatShortDate(notification.createdAt),
        style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
      ),
      trailing: notification.isRead
          ? null
          : Container(
              width: 9,
              height: 9,
              decoration: BoxDecoration(color: colors.primary, shape: BoxShape.circle),
            ),
    );
  }
}
