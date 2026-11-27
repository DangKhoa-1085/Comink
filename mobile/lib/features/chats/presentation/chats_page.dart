import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../calls/domain/call_format.dart';
import '../data/chat_providers.dart';
import '../domain/chat_models.dart';
import 'create_group_dialog.dart';
import 'message_search_page.dart';

enum _ChatFilter { all, unread, groups }

/// Tab Tin nhắn: danh sách hội thoại 1–1 và nhóm với bộ lọc Tất cả / Chưa đọc / Nhóm.
class ChatsPage extends ConsumerStatefulWidget {
  const ChatsPage({super.key});

  @override
  ConsumerState<ChatsPage> createState() => _ChatsPageState();
}

class _ChatsPageState extends ConsumerState<ChatsPage> {
  _ChatFilter _filter = _ChatFilter.all;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final conversations = ref.watch(conversationsProvider);

    return Scaffold(
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        l10n.t('chats_title'),
                        style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                              fontWeight: FontWeight.w800,
                            ),
                      ),
                    ),
                    IconButton(
                      tooltip: l10n.t('chat_search_all_messages'),
                      onPressed: () async {
                        final selection = await Navigator.of(context).push<MessageSearchSelection>(
                          MaterialPageRoute(builder: (_) => const MessageSearchPage()),
                        );
                        if (selection != null && context.mounted) {
                          context.push(
                            '/chats/${selection.result.conversationId}'
                            '?focus=${selection.result.messageId}'
                            '&q=${Uri.encodeQueryComponent(selection.query)}',
                          );
                        }
                      },
                      icon: const Icon(Icons.search),
                    ),
                    IconButton(
                      tooltip: l10n.t('common_retry'),
                      onPressed: () => ref.invalidate(conversationsProvider),
                      icon: const Icon(Icons.refresh),
                    ),
                    const SizedBox(width: 4),
                    FilledButton.icon(
                      onPressed: () async {
                        final conversationId = await showDialog<String>(
                          context: context,
                          builder: (_) => const CreateGroupDialog(),
                        );
                        if (conversationId != null && context.mounted) {
                          context.push('/chats/$conversationId');
                        }
                      },
                      style: FilledButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      ),
                      icon: const Icon(Icons.group_add_outlined, size: 18),
                      label: Text(l10n.t('chats_create_group')),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: [
                    ChoiceChip(
                      selected: _filter == _ChatFilter.all,
                      onSelected: (_) => setState(() => _filter = _ChatFilter.all),
                      label: Text(l10n.t('chats_filter_all')),
                    ),
                    ChoiceChip(
                      selected: _filter == _ChatFilter.unread,
                      onSelected: (_) => setState(() => _filter = _ChatFilter.unread),
                      label: Text(l10n.t('chats_filter_unread')),
                    ),
                    ChoiceChip(
                      selected: _filter == _ChatFilter.groups,
                      onSelected: (_) => setState(() => _filter = _ChatFilter.groups),
                      label: Text(l10n.t('chats_filter_groups')),
                    ),
                  ],
                ),
              ],
            ),
          ),
          Divider(height: 1, color: colors.outlineVariant),
          Expanded(
            child: conversations.when(
              data: (items) {
                final filtered = switch (_filter) {
                  _ChatFilter.all => items,
                  _ChatFilter.unread => items.where((item) => item.unreadCount > 0).toList(),
                  _ChatFilter.groups => items.where((item) => item.isGroup).toList(),
                };

                return RefreshIndicator(
                  onRefresh: () async {
                    ref.invalidate(conversationsProvider);
                    await ref.read(conversationsProvider.future);
                  },
                  child: filtered.isEmpty
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          children: [
                            SizedBox(height: MediaQuery.sizeOf(context).height * 0.18),
                            EmptyState(
                              message: l10n.t('chats_empty'),
                              icon: Icons.chat_bubble_outline,
                            ),
                          ],
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.fromLTRB(12, 10, 12, 24),
                          itemCount: filtered.length,
                          separatorBuilder: (_, _) => const SizedBox(height: 8),
                          itemBuilder: (context, index) => _ConversationTile(
                            conversation: filtered[index],
                            onTap: () => context.push('/chats/${filtered[index].id}'),
                          ),
                        ),
                );
              },
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorState(
                message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                retryLabel: l10n.t('common_retry'),
                onRetry: () => ref.invalidate(conversationsProvider),
              ),
            ),
          ),
        ],
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
                    ? formatCallMessage(lastMessage.preview, l10n)
                    : lastMessage.preview);

    return Card(
      child: ListTile(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        onTap: onTap,
        leading: conversation.isGroup
            ? (conversation.avatarUrl != null && conversation.avatarUrl!.isNotEmpty
                ? AppAvatar(
                    name: title,
                    url: conversation.avatarUrl,
                    radius: 24,
                  )
                : CircleAvatar(
                    radius: 24,
                    backgroundColor: colors.primaryContainer,
                    child: Icon(Icons.groups, color: colors.onPrimaryContainer),
                  ))
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
                          color: const Color(0xFF10B981),
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
                style: TextStyle(
                  fontWeight: conversation.unreadCount > 0 ? FontWeight.w700 : FontWeight.w600,
                ),
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
              Padding(
                padding: const EdgeInsets.only(left: 6),
                child: Text(
                  MaterialLocalizations.of(context).formatShortDate(conversation.lastMessageAt!),
                  style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
                ),
              ),
          ],
        ),
        subtitle: Text(
          preview,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: conversation.unreadCount > 0 ? colors.onSurface : colors.onSurfaceVariant,
            fontWeight: conversation.unreadCount > 0 ? FontWeight.w600 : FontWeight.w400,
          ),
        ),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            if (conversation.unreadCount > 0)
              Container(
                constraints: const BoxConstraints(minWidth: 20),
                padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
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
      ),
    );
  }
}
