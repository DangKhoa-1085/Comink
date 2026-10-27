import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../data/chat_providers.dart';
import '../domain/chat_models.dart';

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
            tooltip: l10n.t('common_retry'),
            onPressed: () => ref.invalidate(conversationsProvider),
            icon: const Icon(Icons.refresh),
          ),
        ],
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
        : (lastMessage.type == 'Image'
            ? l10n.t('chat_image')
            : lastMessage.preview);

    return ListTile(
      onTap: onTap,
      leading: conversation.isGroup
          ? CircleAvatar(
              radius: 24,
              backgroundColor: colors.primaryContainer,
              child: Icon(Icons.groups, color: colors.onPrimaryContainer),
            )
          : AppAvatar(
              name: title,
              url: conversation.avatarUrl,
              radius: 24,
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
      trailing: conversation.isGroup
          ? Text(
              '${conversation.memberCount}',
              style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
            )
          : null,
    );
  }
}
