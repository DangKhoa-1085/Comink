import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/chat_providers.dart';
import '../domain/chat_models.dart';
import 'create_group_dialog.dart';
import 'message_search_page.dart';

/// Tab Tin nhắn: danh sách hội thoại 1–1 và nhóm.
class ChatsPage extends ConsumerWidget {
  const ChatsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final conversations = ref.watch(conversationsProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.t('chats_title')),
        actions: [
          IconButton(
            tooltip: l10n.t('chat_search_all_messages'),
            onPressed: () async {
              final result = await Navigator.of(context).push<MessageSearchResult>(
                MaterialPageRoute(builder: (_) => const MessageSearchPage()),
              );
              if (result != null && context.mounted) {
                context.push('/chats/${result.conversationId}?focus=${result.messageId}');
              }
            },
            icon: const Icon(Icons.search),
          ),
          IconButton(
            tooltip: l10n.t('common_retry'),
            onPressed: () => ref.invalidate(conversationsProvider),
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final conversationId = await showDialog<String>(
            context: context,
            builder: (_) => const CreateGroupDialog(),
          );
          if (conversationId != null && context.mounted) {
            context.push('/chats/$conversationId');
          }
        },
        icon: const Icon(Icons.group_add_outlined),
        label: Text(l10n.t('chats_create_group')),
      ),
      body: conversations.when(
        data: (items) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(conversationsProvider);
            await ref.read(conversationsProvider.future);
          },
          child: items.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    SizedBox(height: MediaQuery.sizeOf(context).height * 0.2),
                    EmptyState(
                      message: l10n.t('chats_empty'),
                      icon: Icons.chat_bubble_outline,
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const Divider(height: 1, indent: 72),
                  itemBuilder: (context, index) => _ConversationTile(
                    conversation: items[index],
                    onTap: () => context.push('/chats/${items[index].id}'),
                  ),
                ),
        ),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
          retryLabel: l10n.t('common_retry'),
          onRetry: () => ref.invalidate(conversationsProvider),
        ),
      ),
    );
  }
}

class _ConversationTile extends StatelessWidget {
  const _ConversationTile({required this.conversation, required this.onTap});

  final ConversationSummary conversation;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final title = conversation.displayTitle;
    final lastMessage = conversation.lastMessage;
    final preview = lastMessage == null
        ? ''
        : (lastMessage.isDeleted
            ? l10n.t('chat_recalled')
            : lastMessage.type == 'Image'
                ? l10n.t('chat_image')
                : lastMessage.type == 'Call'
                    ? l10n.t('chat_call')
                    : lastMessage.preview);

    return ListTile(
      onTap: onTap,
      leading: conversation.isGroup
          ? CircleAvatar(
              radius: 24,
              backgroundColor: colors.primaryContainer,
              child: Icon(Icons.groups, color: colors.onPrimaryContainer),
            )
          : Stack(
              clipBehavior: Clip.none,
              children: [
                AppAvatar(
                  name: title,
                  url: conversation.avatarUrl,
                  radius: 24,
                ),
                // Chấm online (NC GĐ 7): chỉ chat 1–1 mới có trạng thái của người kia.
                if (conversation.peer?.isOnline == true)
                  Positioned(
                    right: 0,
                    bottom: 0,
                    child: Container(
                      width: 13,
                      height: 13,
                      decoration: BoxDecoration(
                        color: Colors.green,
                        shape: BoxShape.circle,
                        border: Border.all(color: colors.surface, width: 2),
                      ),
                    ),
                  ),
              ],
            ),
      title: Row(
        children: [
          Expanded(
            child: Text(
              title,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
          if (conversation.isMuted)
            Padding(
              padding: const EdgeInsets.only(left: 4),
              child: Icon(
                Icons.notifications_off_outlined,
                size: 14,
                color: colors.onSurfaceVariant,
                semanticLabel: l10n.t('chat_muted'),
              ),
            ),
          if (conversation.lastMessageAt != null)
            Text(
              MaterialLocalizations.of(context).formatShortDate(conversation.lastMessageAt!),
              style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
            ),
        ],
      ),
      subtitle: Text(
        preview,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(color: colors.onSurfaceVariant),
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (conversation.unreadCount > 0)
            Container(
              constraints: const BoxConstraints(minWidth: 20),
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              decoration: BoxDecoration(
                color: colors.primary,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                conversation.unreadCount > 99 ? '99+' : '${conversation.unreadCount}',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: colors.onPrimary,
                ),
              ),
            ),
          if (conversation.isGroup)
            Text(
              '${conversation.memberCount}',
              style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
            ),
        ],
      ),
    );
  }
}
