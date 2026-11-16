import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/utils/uuid.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../../calls/data/call_controller.dart';
import '../../media/data/media_service.dart';
import '../data/chat_providers.dart';
import '../data/chat_realtime.dart';
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
  XFile? _pendingImage;
  Uint8List? _pendingBytes;
  bool _sending = false;
  bool _uploading = false;

  final List<ChatMessage> _olderMessages = [];
  String? _olderCursor;
  bool _olderHasMore = false;
  bool _loadingOlder = false;
  Timer? _syncTimer;

  // NC GĐ 7: "đang nhập" và mốc đã đọc.
  StreamSubscription<void>? _typingSub;
  Timer? _typingStopTimer;
  DateTime _lastTypingSentAt = DateTime.fromMillisecondsSinceEpoch(0);
  bool _typingSent = false;
  String? _lastMarkedMessageId;

  @override
  void initState() {
    super.initState();
    // Lưới an toàn khi SignalR mất kết nối: tải lại trang mới nhất định kỳ.
    _syncTimer = Timer.periodic(const Duration(seconds: 15), (_) {
      if (!mounted || _sending) {
        return;
      }
      ref.invalidate(messagesProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
    });

    // Danh sách người đang gõ thay đổi → vẽ lại phần tiêu đề.
    _typingSub = ref
        .read(chatRealtimeProvider)
        .typingChanges
        .listen((_) => _onTypingChanged());
  }

  @override
  void dispose() {
    _syncTimer?.cancel();
    _typingStopTimer?.cancel();
    unawaited(_typingSub?.cancel());
    // Rời khung chat khi vẫn đang gõ → báo ngừng để người kia không thấy "đang nhập" treo.
    if (_typingSent) {
      unawaited(ref
          .read(chatRealtimeProvider)
          .sendTyping(widget.conversationId, false));
    }
    _controller.dispose();
    super.dispose();
  }

  void _onTypingChanged() {
    if (mounted) {
      setState(() {});
    }
  }

  /// Gõ → báo "đang nhập" (throttle 3 giây); ngừng 5 giây thì tự báo ngừng.
  void _notifyTyping() {
    final realtime = ref.read(chatRealtimeProvider);
    final now = DateTime.now();
    if (now.difference(_lastTypingSentAt) > const Duration(seconds: 3)) {
      _lastTypingSentAt = now;
      _typingSent = true;
      unawaited(realtime.sendTyping(widget.conversationId, true));
    }

    _typingStopTimer?.cancel();
    _typingStopTimer = Timer(const Duration(seconds: 5), _stopTyping);
  }

  void _stopTyping() {
    _typingStopTimer?.cancel();
    _typingStopTimer = null;
    _lastTypingSentAt = DateTime.fromMillisecondsSinceEpoch(0);
    if (!_typingSent) {
      return;
    }
    _typingSent = false;
    unawaited(ref.read(chatRealtimeProvider).sendTyping(widget.conversationId, false));
  }

  /// Đánh dấu đã đọc tới tin mới nhất đang thấy (server chỉ tiến mốc).
  void _markRead(String messageId) {
    if (_lastMarkedMessageId == messageId) {
      return;
    }
    _lastMarkedMessageId = messageId;
    unawaited(ref.read(chatRealtimeProvider).markRead(widget.conversationId, messageId));
  }

  Future<void> _loadOlder(ChatHistory history) async {
    final cursor = _olderCursor ?? history.nextCursor;
    final canLoad = _olderMessages.isEmpty ? history.hasMore : _olderHasMore;
    if (cursor == null || !canLoad || _loadingOlder) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _loadingOlder = true);

    try {
      final data = await ref.read(apiClientProvider).get(
            '/api/conversations/${widget.conversationId}/messages',
            query: {'before': cursor, 'limit': 50},
          );
      final page = PagedResult.fromJson(data, ChatMessage.fromJson);
      final older = page.items.reversed.toList(growable: false);
      if (!mounted) {
        return;
      }
      setState(() {
        _olderMessages.insertAll(0, older);
        _olderCursor = page.nextCursor;
        _olderHasMore = page.hasMore;
        _loadingOlder = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() => _loadingOlder = false);
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  Future<void> _pickImage() async {
    if (_sending) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final file = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        imageQuality: 85,
        maxWidth: 1920,
      );
      if (file == null) {
        return;
      }
      final bytes = await file.readAsBytes();
      if (bytes.length > MediaService.maxImageBytes) {
        messenger.showSnackBar(
          SnackBar(content: Text(l10n.error('MEDIA_TOO_LARGE'))),
        );
        return;
      }
      setState(() {
        _pendingImage = file;
        _pendingBytes = bytes;
      });
    } catch (_) {
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.error('MEDIA_UPLOAD_FAILED'))),
      );
    }
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if ((text.isEmpty && _pendingImage == null) || _sending) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _sending = true);

    try {
      final mediaIds = <String>[];
      if (_pendingImage != null) {
        setState(() => _uploading = true);
        mediaIds.addAll(
          await ref.read(mediaServiceProvider).uploadAll(
                [_pendingImage!],
                context: 'Message',
              ),
        );
        if (mounted) {
          setState(() => _uploading = false);
        }
      }

      await ref.read(apiClientProvider).post(
        '/api/conversations/${widget.conversationId}/messages',
        data: {
          'clientMessageId': generateUuidV4(),
          if (text.isNotEmpty) 'content': text,
          if (mediaIds.isNotEmpty) 'mediaIds': mediaIds,
        },
      );
      _controller.clear();
      if (mounted) {
        setState(() {
          _pendingImage = null;
          _pendingBytes = null;
        });
      }
      ref.invalidate(messagesProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
      _stopTyping();
    } on ApiException catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('${l10n.t('chat_error')}: ${l10n.error(error.code)}')),
      );
    } finally {
      if (mounted) {
        setState(() {
          _sending = false;
          _uploading = false;
        });
      }
    }
  }

  /// Dòng trạng thái dưới tên hội thoại: "đang nhập…" → "Đang hoạt động" → "Truy cập lần cuối…".
  String? _statusText(
    BuildContext context,
    AppLocalizations l10n,
    ConversationDetail? detail,
    String currentUserId,
  ) {
    if (detail == null) {
      return null;
    }

    final typingIds = ref
        .read(chatRealtimeProvider)
        .typingUserIds(widget.conversationId)
      ..remove(currentUserId);

    if (detail.isGroup) {
      if (typingIds.isNotEmpty) {
        final names = detail.members
            .where((member) => typingIds.contains(member.userId))
            .map((member) => member.displayName)
            .where((name) => name.isNotEmpty)
            .toList(growable: false);
        if (names.isNotEmpty) {
          return '${names.join(', ')} ${l10n.t('chat_typing_many')}';
        }
      }

      final online = detail.members.where((member) => member.isOnline).length;
      return '${detail.members.length} ${l10n.t('chat_members')} · '
          '$online ${l10n.t('chat_online_suffix')}';
    }

    ConversationPeer? peer;
    for (final member in detail.members) {
      if (member.userId != currentUserId) {
        peer = member;
        break;
      }
    }
    if (peer == null) {
      return null;
    }

    if (typingIds.contains(peer.userId)) {
      return l10n.t('chat_typing');
    }
    if (peer.isOnline) {
      return l10n.t('chat_online');
    }
    if (peer.lastSeenAt != null) {
      return '${l10n.t('chat_last_seen')} ${_formatMoment(context, peer.lastSeenAt!)}';
    }
    return l10n.t('chat_offline');
  }

  String _formatMoment(BuildContext context, DateTime time) {
    final material = MaterialLocalizations.of(context);
    final now = DateTime.now();
    final sameDay = now.year == time.year && now.month == time.month && now.day == time.day;
    if (sameDay) {
      return material.formatTimeOfDay(
        TimeOfDay.fromDateTime(time),
        alwaysUse24HourFormat: MediaQuery.alwaysUse24HourFormatOf(context),
      );
    }
    return material.formatShortDate(time);
  }

  /// Nhãn "Đã gửi"/"Đã đọc"/"Đã xem bởi N" cho tin CUỐI của chính mình (NC GĐ 7).
  String? _readLabel(
    AppLocalizations l10n,
    ConversationDetail? detail,
    String currentUserId,
    ChatMessage? lastOwnMessage,
  ) {
    if (detail == null || lastOwnMessage == null) {
      return null;
    }

    if (detail.isGroup) {
      final seen = detail.members
          .where((member) =>
              member.userId != currentUserId &&
              member.lastReadAt != null &&
              !member.lastReadAt!.isBefore(lastOwnMessage.createdAt))
          .length;
      return seen > 0 ? '${l10n.t('chat_seen_by')} $seen' : l10n.t('chat_sent');
    }

    ConversationPeer? peer;
    for (final member in detail.members) {
      if (member.userId != currentUserId) {
        peer = member;
        break;
      }
    }
    final lastReadAt = peer?.lastReadAt;
    final read = lastReadAt != null && !lastReadAt.isBefore(lastOwnMessage.createdAt);
    return read ? l10n.t('chat_read') : l10n.t('chat_sent');
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final session = ref.watch(sessionControllerProvider);
    final currentUserId = session.user?.id ?? '';
    final detail = ref.watch(conversationDetailProvider(widget.conversationId));
    final messages = ref.watch(messagesProvider(widget.conversationId));

    final detailValue = detail.asData?.value;

    // Tin mới (hoặc mở khung chat) → đánh dấu đã đọc; server chỉ tiến mốc nên gọi lại vô hại.
    ref.listen(messagesProvider(widget.conversationId), (previous, next) {
      final history = next.asData?.value;
      if (history != null && history.messages.isNotEmpty) {
        _markRead(history.messages.last.id);
      }
    });

    final title = detailValue?.displayTitle(currentUserId) ?? l10n.t('chats_title');
    final statusText = _statusText(context, l10n, detailValue, currentUserId);

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(title),
            if (statusText != null)
              Text(
                statusText,
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w400,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: l10n.t('call_audio'),
            onPressed: () => ref
                .read(callControllerProvider.notifier)
                .start(widget.conversationId, video: false),
            icon: const Icon(Icons.call),
          ),
          IconButton(
            tooltip: l10n.t('call_video'),
            onPressed: () => ref
                .read(callControllerProvider.notifier)
                .start(widget.conversationId, video: true),
            icon: const Icon(Icons.videocam),
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: messages.when(
              skipLoadingOnRefresh: true,
              data: (history) {
                final merged = <String, ChatMessage>{
                  for (final message in [..._olderMessages, ...history.messages])
                    message.id: message,
                }.values.toList(growable: false)
                  ..sort((first, second) => first.createdAt.compareTo(second.createdAt));
                final canLoad = _olderMessages.isEmpty ? history.hasMore : _olderHasMore;

                ChatMessage? lastOwnCandidate;
                for (final message in merged) {
                  if (message.senderId == currentUserId) {
                    lastOwnCandidate = message;
                  }
                }
                final lastOwnMessage = lastOwnCandidate;
                final readLabel = _readLabel(
                  l10n,
                  detailValue,
                  currentUserId,
                  lastOwnMessage,
                );

                return merged.isEmpty
                    ? EmptyState(
                        message: l10n.t('chat_no_messages'),
                        icon: Icons.forum_outlined,
                      )
                    : _MessageList(
                        messages: merged,
                        currentUserId: currentUserId,
                        memberNames: detailValue == null
                            ? const <String, String>{}
                            : {
                                for (final member in detailValue.members)
                                  member.userId: member.displayName,
                              },
                        showSenderName: detailValue?.isGroup ?? false,
                        lastOwnMessageId: lastOwnMessage?.id,
                        readLabel: readLabel,
                        hasMore: canLoad,
                        loadingOlder: _loadingOlder,
                        onLoadOlder: () => _loadOlder(history),
                      );
              },
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
              child: Column(
                children: [
                  if (_uploading)
                    Align(
                      alignment: Alignment.centerLeft,
                      child: Padding(
                        padding: const EdgeInsets.only(bottom: 4),
                        child: Text(
                          l10n.t('chat_uploading'),
                          style: TextStyle(
                            fontSize: 11,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                    ),
                  if (_pendingBytes != null)
                    Align(
                      alignment: Alignment.centerLeft,
                      child: Padding(
                        padding: const EdgeInsets.only(bottom: 6),
                        child: Stack(
                          children: [
                            ClipRRect(
                              borderRadius: BorderRadius.circular(10),
                              child: Image.memory(
                                _pendingBytes!,
                                width: 64,
                                height: 64,
                                fit: BoxFit.cover,
                              ),
                            ),
                            Positioned(
                              top: 0,
                              right: 0,
                              child: GestureDetector(
                                onTap: _sending
                                    ? null
                                    : () => setState(() {
                                          _pendingImage = null;
                                          _pendingBytes = null;
                                        }),
                                child: Container(
                                  padding: const EdgeInsets.all(2),
                                  decoration: const BoxDecoration(
                                    color: Colors.black54,
                                    shape: BoxShape.circle,
                                  ),
                                  child: const Icon(Icons.close, size: 14, color: Colors.white),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      IconButton(
                        onPressed: _sending ? null : _pickImage,
                        icon: const Icon(Icons.image_outlined),
                        tooltip: l10n.t('chat_add_image'),
                      ),
                      Expanded(
                        child: TextField(
                          controller: _controller,
                          minLines: 1,
                          maxLines: 4,
                          textInputAction: TextInputAction.send,
                          onChanged: (_) => _notifyTyping(),
                          onSubmitted: (_) => _send(),
                          decoration: InputDecoration(
                            hintText: l10n.t('chat_placeholder'),
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
                        tooltip: l10n.t('chat_send'),
                      ),
                    ],
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
    required this.hasMore,
    required this.loadingOlder,
    required this.onLoadOlder,
    this.lastOwnMessageId,
    this.readLabel,
  });

  final List<ChatMessage> messages;
  final String currentUserId;
  final Map<String, String> memberNames;
  final bool showSenderName;
  final bool hasMore;
  final bool loadingOlder;
  final VoidCallback onLoadOlder;
  final String? lastOwnMessageId;
  final String? readLabel;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final reversed = messages.reversed.toList(growable: false);
    final itemCount = reversed.length + (hasMore ? 1 : 0);

    return ListView.builder(
      reverse: true,
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
      itemCount: itemCount,
      itemBuilder: (context, index) {
        if (index == reversed.length) {
          return Center(
            child: loadingOlder
                ? const Padding(
                    padding: EdgeInsets.all(8),
                    child: SizedBox(
                      height: 18,
                      width: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  )
                : TextButton(
                    onPressed: onLoadOlder,
                    child: Text(l10n.t('chat_load_older')),
                  ),
          );
        }

        final message = reversed[index];
        return _MessageBubble(
          message: message,
          isMine: message.senderId == currentUserId,
          senderName: showSenderName ? memberNames[message.senderId] ?? '' : '',
          statusLabel: message.id == lastOwnMessageId ? readLabel : null,
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
    this.statusLabel,
  });

  final ChatMessage message;
  final bool isMine;
  final String senderName;
  final String? statusLabel;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
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
              Text(
                message.type == 'Call' ? l10n.t('chat_call') : message.content!,
                style: TextStyle(color: textColor, fontSize: 15),
              ),
            const SizedBox(height: 3),
            Text(
              time,
              style: TextStyle(
                fontSize: 10,
                color: textColor.withValues(alpha: 0.7),
              ),
            ),
            if (statusLabel != null)
              Text(
                statusLabel!,
                style: TextStyle(
                  fontSize: 10,
                  color: textColor.withValues(alpha: 0.8),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
