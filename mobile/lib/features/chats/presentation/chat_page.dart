import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/utils/uuid.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../data/chat_providers.dart';
import '../domain/chat_models.dart';

/// Khung chat: lịch sử tin nhắn + gửi tin văn bản (real-time sẽ bổ sung sau).
class ChatPage extends ConsumerStatefulWidget {
  const ChatPage({super.key, required this.conversationId});

  final String conversationId;

  @override
  ConsumerState<ChatPage> createState() => _ChatPageState();
}

class _ChatPageState extends ConsumerState<ChatPage> {
  final _controller = TextEditingController();
  bool _sending = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _sending) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _sending = true);

    try {
      await ref.read(apiClientProvider).post(
        '/api/conversations/${widget.conversationId}/messages',
        data: {
          'clientMessageId': generateUuidV4(),
          'content': text,
        },
      );
      _controller.clear();
      ref.invalidate(messagesProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
    } on ApiException catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('${l10n.t('chat_error')}: ${l10n.error(error.code)}')),
      );
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final session = ref.watch(sessionControllerProvider);
    final currentUserId = session.user?.id ?? '';
    final detail = ref.watch(conversationDetailProvider(widget.conversationId));
    final messages = ref.watch(messagesProvider(widget.conversationId));

    final title = detail.maybeWhen(
      data: (value) => value.displayTitle(currentUserId),
      orElse: () => l10n.t('chats_title'),
    );

    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: Column(
        children: [
          Expanded(
            child: messages.when(
              data: (items) => items.isEmpty
                  ? EmptyState(
                      message: l10n.t('chat_no_messages'),
                      icon: Icons.forum_outlined,
                    )
                  : _MessageList(
                      messages: items,
                      currentUserId: currentUserId,
                      memberNames: detail.maybeWhen(
                        data: (value) => {
                          for (final member in value.members) member.userId: member.displayName,
                        },
                        orElse: () => const <String, String>{},
                      ),
                      showSenderName: detail.maybeWhen(
                        data: (value) => value.isGroup,
                        orElse: () => false,
                      ),
                    ),
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorState(
                message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                retryLabel: l10n.t('common_retry'),
                onRetry: () => ref.invalidate(messagesProvider(widget.conversationId)),
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(10, 6, 10, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: TextField(
                      controller: _controller,
                      minLines: 1,
                      maxLines: 4,
                      textInputAction: TextInputAction.send,
                      onSubmitted: (_) => _send(),
                      decoration: InputDecoration(
                        hintText: l10n.t('chat_placeholder'),
                        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
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
                    tooltip: l10n.t('chat_send'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MessageList extends StatelessWidget {
  const _MessageList({
    required this.messages,
    required this.currentUserId,
    required this.memberNames,
    required this.showSenderName,
  });

  final List<ChatMessage> messages;
  final String currentUserId;
  final Map<String, String> memberNames;
  final bool showSenderName;

  @override
  Widget build(BuildContext context) {
    final reversed = messages.reversed.toList(growable: false);

    return ListView.builder(
      reverse: true,
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
      itemCount: reversed.length,
      itemBuilder: (context, index) {
        final message = reversed[index];
        return _MessageBubble(
          message: message,
          isMine: message.senderId == currentUserId,
          senderName: showSenderName ? memberNames[message.senderId] ?? '' : '',
        );
      },
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({
    required this.message,
    required this.isMine,
    required this.senderName,
  });

  final ChatMessage message;
  final bool isMine;
  final String senderName;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final bubbleColor = isMine ? colors.primaryContainer : colors.surfaceContainerHighest;
    final textColor = isMine ? colors.onPrimaryContainer : colors.onSurface;
    final time = MaterialLocalizations.of(context).formatTimeOfDay(
      TimeOfDay.fromDateTime(message.createdAt),
      alwaysUse24HourFormat: MediaQuery.alwaysUse24HourFormatOf(context),
    );

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.78),
        margin: const EdgeInsets.symmetric(vertical: 3),
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 6),
        decoration: BoxDecoration(
          color: bubbleColor,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(14),
            topRight: const Radius.circular(14),
            bottomLeft: Radius.circular(isMine ? 14 : 4),
            bottomRight: Radius.circular(isMine ? 4 : 14),
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (senderName.isNotEmpty)
              Text(
                senderName,
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: isMine ? colors.onPrimaryContainer : colors.primary,
                ),
              ),
            if (message.attachments.isNotEmpty) ...[
              for (final attachment in message.attachments)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.network(
                      attachment.url,
                      width: double.infinity,
                      height: 160,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => Container(
                        height: 80,
                        alignment: Alignment.center,
                        color: colors.surfaceContainerHighest,
                        child: Icon(Icons.broken_image_outlined, color: colors.outline),
                      ),
                    ),
                  ),
                ),
            ],
            if (message.content != null)
              Text(message.content!, style: TextStyle(color: textColor, fontSize: 15)),
            const SizedBox(height: 3),
            Text(
              time,
              style: TextStyle(
                fontSize: 10,
                color: textColor.withValues(alpha: 0.7),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
