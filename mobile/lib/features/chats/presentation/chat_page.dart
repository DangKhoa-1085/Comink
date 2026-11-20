import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/utils/highlight.dart';
import '../../../core/utils/uuid.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../../calls/data/call_controller.dart';
import '../../media/data/media_service.dart';
import '../data/chat_providers.dart';
import '../data/chat_realtime.dart';
import '../domain/chat_models.dart';
import 'message_search_page.dart';

/// Một cảm xúc hiển thị trong menu tin nhắn — khoá phải khớp `ReactionCatalog` ở backend.
class ChatReactionOption {
  const ChatReactionOption(this.key, this.emoji);

  final String key;
  final String emoji;
}

const chatReactionCatalog = <ChatReactionOption>[
  ChatReactionOption('like', '👍'),
  ChatReactionOption('love', '❤️'),
  ChatReactionOption('haha', '😂'),
  ChatReactionOption('wow', '😮'),
  ChatReactionOption('sad', '😢'),
  ChatReactionOption('angry', '😡'),
];

/// Khung chat: lịch sử tin nhắn + gửi tin văn bản (real-time sẽ bổ sung sau).
class ChatPage extends ConsumerStatefulWidget {
  const ChatPage({
    super.key,
    required this.conversationId,
    this.focusMessageId,
    this.focusTerm,
  });

  final String conversationId;

  /// Tin cần mở ngay (từ kết quả tìm kiếm) — nạp cửa sổ ngữ cảnh quanh tin này.
  final String? focusMessageId;

  /// Từ khoá đã tìm — tô sáng trong nội dung tin khi mở từ kết quả tìm kiếm.
  final String? focusTerm;

  @override
  ConsumerState<ChatPage> createState() => _ChatPageState();
}

class _ChatPageState extends ConsumerState<ChatPage> {
  final _controller = TextEditingController();
  XFile? _pendingImage;
  Uint8List? _pendingBytes;
  bool _sending = false;
  bool _uploading = false;

  // NC Ưu tiên 2: trả lời / sửa / chế độ ngữ cảnh quanh một tin / tô sáng từ khoá tìm kiếm.
  ChatMessage? _replyTo;
  ChatMessage? _editing;
  List<ChatMessage>? _contextMessages;
  String? _highlightTerm;
  String? _highlightMessageId;
  final GlobalKey _targetMessageKey = GlobalKey();

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

    // Mở từ kết quả tìm kiếm: nạp ngữ cảnh quanh tin đích sau khung hình đầu tiên.
    final focus = widget.focusMessageId;
    if (focus != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) {
          unawaited(_loadContext(focus, term: widget.focusTerm));
        }
      });
    }
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

  /// Nạp cửa sổ ngữ cảnh quanh `messageId` rồi cuộn tới tin đó; nếu có `term` thì tô sáng từ khoá.
  Future<void> _loadContext(String messageId, {String? term}) async {
    try {
      final data = await ref.read(apiClientProvider).get(
            '/api/conversations/${widget.conversationId}/messages/$messageId/context',
            query: {'limit': 31},
          );
      if (data is! Map) {
        return;
      }

      final items = (data['items'] as List<dynamic>? ?? const [])
          .whereType<Map>()
          .map((item) => ChatMessage.fromJson(item.cast<String, dynamic>()))
          .toList(growable: false);
      if (mounted) {
        setState(() {
          _contextMessages = items;
          _highlightMessageId = messageId;
          if (term != null && term.trim().isNotEmpty) {
            _highlightTerm = term.trim();
          }
        });
        _scrollToTarget();
      }
    } on ApiException {
      // Tin không còn hoặc mất mạng: giữ khung chat hiện tại.
    }
  }

  /// Cuộn tới tin đang được tô sáng sau khi danh sách đã dựng xong.
  void _scrollToTarget() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final targetContext = _targetMessageKey.currentContext;
      if (targetContext == null) {
        return;
      }

      unawaited(
        Scrollable.ensureVisible(
          targetContext,
          alignment: 0.4,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        ),
      );
    });
  }

  /// Nhảy tới một tin: đã có trong khung thì cuộn + tô sáng, chưa có thì nạp ngữ cảnh.
  Future<void> _jumpToMessage(String messageId, {String? term}) async {
    final history = ref.read(messagesProvider(widget.conversationId)).asData?.value;
    final loadedIds = <String>{
      if (_contextMessages != null) ..._contextMessages!.map((message) => message.id),
      ..._olderMessages.map((message) => message.id),
      if (history != null) ...history.messages.map((message) => message.id),
    };

    if (loadedIds.contains(messageId)) {
      setState(() {
        _highlightMessageId = messageId;
        if (term != null && term.trim().isNotEmpty) {
          _highlightTerm = term.trim();
        }
      });
      _scrollToTarget();
      return;
    }

    await _loadContext(messageId, term: term);
  }

  void _startReply(ChatMessage message) {
    setState(() {
      _editing = null;
      _replyTo = message;
    });
  }

  void _startEdit(ChatMessage message) {
    setState(() {
      _replyTo = null;
      _editing = message;
      _controller.text = message.content ?? '';
    });
  }

  void _clearComposerAction() {
    setState(() {
      _replyTo = null;
      if (_editing != null) {
        _editing = null;
        _controller.clear();
      }
    });
  }

  Future<void> _react(ChatMessage message, String reaction) async {
    final mine = message.reactions.any(
      (item) => item.reaction == reaction && item.reactedByMe,
    );
    try {
      final path =
          '/api/conversations/${widget.conversationId}/messages/${message.id}/reactions';
      if (mine) {
        await ref.read(apiClientProvider).delete('$path/${Uri.encodeComponent(reaction)}');
      } else {
        await ref.read(apiClientProvider).post(path, data: {'reaction': reaction});
      }
      ref.invalidate(messagesProvider(widget.conversationId));
    } on ApiException catch (error) {
      if (mounted) {
        _showError(error.code);
      }
    }
  }

  Future<void> _recall(ChatMessage message) async {
    final l10n = AppLocalizations.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        content: Text(l10n.t('chat_recall_confirm')),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.t('common_cancel')),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.t('chat_recall')),
          ),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }

    try {
      await ref.read(apiClientProvider).delete(
            '/api/conversations/${widget.conversationId}/messages/${message.id}',
          );
      ref.invalidate(messagesProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
      if (_editing?.id == message.id) {
        _clearComposerAction();
      }
      if (_replyTo?.id == message.id) {
        setState(() => _replyTo = null);
      }
    } on ApiException catch (error) {
      if (mounted) {
        _showError(error.code);
      }
    }
  }

  Future<void> _pin(ChatMessage message, bool isPinned) async {
    try {
      final path = '/api/conversations/${widget.conversationId}/pinned/${message.id}';
      if (isPinned) {
        await ref.read(apiClientProvider).delete(path);
      } else {
        await ref.read(apiClientProvider).put(path);
      }
      ref.invalidate(pinnedMessagesProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
    } on ApiException catch (error) {
      if (mounted) {
        _showError(error.code);
      }
    }
  }

  Future<void> _forward(ChatMessage message) async {
    final l10n = AppLocalizations.of(context);
    try {
      final data = await ref.read(apiClientProvider).get(
            '/api/conversations',
            query: {'limit': 50},
          );
      final page = PagedResult.fromJson(data, ConversationSummary.fromJson);
      if (!mounted) {
        return;
      }

      final target = await showModalBottomSheet<ConversationSummary>(
        context: context,
        isScrollControlled: true,
        builder: (sheetContext) => SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.all(16),
                child: Text(
                  l10n.t('chat_forward_to'),
                  style: Theme.of(sheetContext).textTheme.titleMedium,
                ),
              ),
              Flexible(
                child: ListView.builder(
                  shrinkWrap: true,
                  itemCount: page.items.length,
                  itemBuilder: (context, index) {
                    final conversation = page.items[index];
                    return ListTile(
                      leading: const Icon(Icons.chat_bubble_outline),
                      title: Text(conversation.displayTitle),
                      subtitle: Text(conversation.isGroup
                          ? l10n.t('chat_group')
                          : (conversation.peer?.userName ?? '')),
                      onTap: () => Navigator.of(sheetContext).pop(conversation),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      );
      if (target == null) {
        return;
      }

      await ref.read(apiClientProvider).post(
            '/api/conversations/${target.id}/messages/forward',
            data: {
              'sourceMessageId': message.id,
              'clientMessageId': generateUuidV4(),
            },
          );
      if (target.id == widget.conversationId) {
        ref.invalidate(messagesProvider(widget.conversationId));
      }
      ref.invalidate(conversationsProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(l10n.t('chat_forwarded'))),
        );
      }
    } on ApiException catch (error) {
      if (mounted) {
        _showError(error.code);
      }
    }
  }

  Future<void> _mute(String duration) async {
    final l10n = AppLocalizations.of(context);
    try {
      await ref.read(apiClientProvider).put(
            '/api/conversations/${widget.conversationId}/mute',
            data: {'duration': duration},
          );
      ref.invalidate(conversationDetailProvider(widget.conversationId));
      ref.invalidate(conversationsProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              duration == 'Off' ? l10n.t('chat_unmuted') : l10n.t('chat_muted'),
            ),
          ),
        );
      }
    } on ApiException catch (error) {
      if (mounted) {
        _showError(error.code);
      }
    }
  }

  void _showError(String code) {
    final l10n = AppLocalizations.of(context);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(l10n.error(code))),
    );
  }

  Future<void> _openSearch() async {
    final selection = await Navigator.of(context).push<MessageSearchSelection>(
      MaterialPageRoute(
        builder: (_) => MessageSearchPage(conversationId: widget.conversationId),
      ),
    );
    if (selection != null) {
      // Luôn dùng cửa sổ ngữ cảnh cho kết quả tìm kiếm: đảm bảo tin đích được dựng để cuộn tới
      // (ListView thường chỉ dựng item đang nhìn thấy).
      await _loadContext(selection.result.messageId, term: selection.query);
    }
  }

  Future<void> _showPinnedSheet() async {
    final l10n = AppLocalizations.of(context);
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => Consumer(
        builder: (context, ref, _) {
          final pinned = ref.watch(pinnedMessagesProvider(widget.conversationId));
          return SafeArea(
            child: SizedBox(
              height: MediaQuery.sizeOf(context).height * 0.55,
              child: pinned.when(
                data: (value) => value.pinned.isEmpty
                    ? Center(child: Text(l10n.t('chat_no_pinned')))
                    : ListView(
                        children: [
                          Padding(
                            padding: const EdgeInsets.all(16),
                            child: Text(
                              l10n.t('chat_pinned_count', {'count': '${value.pinned.length}'}),
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                          for (final item in value.pinned)
                            ListTile(
                              title: Text(
                                item.message.isDeleted
                                    ? l10n.t('chat_recalled')
                                    : (item.message.content ?? l10n.t('chat_image')),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                              subtitle: Text(
                                '${l10n.t('chat_pinned_by')} ${item.pinnedByDisplayName}',
                              ),
                              trailing: IconButton(
                                icon: const Icon(Icons.push_pin_outlined),
                                tooltip: l10n.t('chat_unpin'),
                                onPressed: () {
                                  unawaited(_pin(item.message, true));
                                  Navigator.of(sheetContext).pop();
                                },
                              ),
                              onTap: () {
                                Navigator.of(sheetContext).pop();
                                unawaited(_jumpToMessage(item.message.id));
                              },
                            ),
                        ],
                      ),
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (_, _) => Center(child: Text(l10n.t('chat_pinned_error'))),
              ),
            ),
          );
        },
      ),
    );
  }

  Future<void> _showMuteSheet() async {
    final l10n = AppLocalizations.of(context);
    final detail = ref.read(conversationDetailProvider(widget.conversationId)).asData?.value;
    const durations = ['Off', 'OneHour', 'EightHours', 'OneWeek', 'Forever'];
    final labels = {
      'Off': l10n.t('chat_mute_off'),
      'OneHour': l10n.t('chat_mute_1h'),
      'EightHours': l10n.t('chat_mute_8h'),
      'OneWeek': l10n.t('chat_mute_1w'),
      'Forever': l10n.t('chat_mute_forever'),
    };

    final choice = await showModalBottomSheet<String>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (detail?.isMuted ?? false)
              ListTile(
                leading: const Icon(Icons.notifications_active_outlined),
                title: Text(l10n.t('chat_unmute')),
                onTap: () => Navigator.of(sheetContext).pop('Off'),
              ),
            for (final duration in durations.where((value) => value != 'Off'))
              ListTile(
                leading: const Icon(Icons.notifications_off_outlined),
                title: Text(labels[duration] ?? duration),
                onTap: () => Navigator.of(sheetContext).pop(duration),
              ),
          ],
        ),
      ),
    );
    if (choice != null) {
      await _mute(choice);
    }
  }

  Future<void> _showMessageActions(ChatMessage message) async {
    final l10n = AppLocalizations.of(context);
    final currentUserId = ref.read(sessionControllerProvider).user?.id ?? '';
    final isMine = message.senderId == currentUserId;
    final pinned = ref.read(pinnedMessagesProvider(widget.conversationId)).asData?.value;
    final isPinned = pinned?.pinned.any((item) => item.message.id == message.id) ?? false;
    final withinWindow =
        DateTime.now().difference(message.createdAt) <= const Duration(hours: 24);
    final editable = isMine &&
        !message.isDeleted &&
        withinWindow &&
        message.type != 'System' &&
        message.type != 'Call';

    await showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (!message.isDeleted)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                // Wrap để 6 cảm xúc luôn hiện đủ, không bị cắt trên màn hình hẹp.
                child: Wrap(
                  alignment: WrapAlignment.center,
                  spacing: 4,
                  runSpacing: 4,
                  children: [
                    for (final reaction in chatReactionCatalog)
                      InkWell(
                        borderRadius: BorderRadius.circular(24),
                        onTap: () {
                          Navigator.of(sheetContext).pop();
                          unawaited(_react(message, reaction.key));
                        },
                        child: Container(
                          width: 44,
                          height: 44,
                          alignment: Alignment.center,
                          child: Text(reaction.emoji, style: const TextStyle(fontSize: 24)),
                        ),
                      ),
                  ],
                ),
              ),
            if (!message.isDeleted)
              ListTile(
                leading: const Icon(Icons.reply),
                title: Text(l10n.t('chat_reply')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  _startReply(message);
                },
              ),
            if (!message.isDeleted)
              ListTile(
                leading: const Icon(Icons.forward),
                title: Text(l10n.t('chat_forward')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  unawaited(_forward(message));
                },
              ),
            if (!message.isDeleted)
              ListTile(
                leading: Icon(isPinned ? Icons.push_pin : Icons.push_pin_outlined),
                title: Text(isPinned ? l10n.t('chat_unpin') : l10n.t('chat_pin')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  unawaited(_pin(message, isPinned));
                },
              ),
            if (!message.isDeleted && message.content != null)
              ListTile(
                leading: const Icon(Icons.copy),
                title: Text(l10n.t('chat_copy')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  unawaited(Clipboard.setData(ClipboardData(text: message.content!)));
                },
              ),
            if (editable)
              ListTile(
                leading: const Icon(Icons.edit_outlined),
                title: Text(l10n.t('chat_edit')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  _startEdit(message);
                },
              ),
            if (editable)
              ListTile(
                leading: const Icon(Icons.undo),
                title: Text(l10n.t('chat_recall')),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  unawaited(_recall(message));
                },
              ),
          ],
        ),
      ),
    );
  }


  Future<void> _submitEdit(String text) async {
    final editing = _editing;
    if (editing == null || text.isEmpty || _sending) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _sending = true);
    try {
      await ref.read(apiClientProvider).patch(
            '/api/conversations/${widget.conversationId}/messages/${editing.id}',
            data: {'content': text},
          );
      _controller.clear();
      if (mounted) {
        setState(() => _editing = null);
      }
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

  Future<void> _send() async {
    final text = _controller.text.trim();

    // Đang sửa: PATCH nội dung, không tạo tin mới (NC Ưu tiên 2).
    if (_editing != null) {
      await _submitEdit(text);
      return;
    }

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
          if (_replyTo != null) 'replyToMessageId': _replyTo!.id,
        },
      );
      _controller.clear();
      if (mounted) {
        setState(() {
          _pendingImage = null;
          _pendingBytes = null;
          _replyTo = null;
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

    // Tiêu đề chỉ còn trạng thái presence; "đang nhập" hiện ngay trên khung nhập.
    if (detail.isGroup) {
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

    if (peer.isOnline) {
      return l10n.t('chat_online');
    }
    if (peer.lastSeenAt != null) {
      return '${l10n.t('chat_last_seen')} ${_formatMoment(context, peer.lastSeenAt!)}';
    }
    return l10n.t('chat_offline');
  }

  /// Dòng "đang nhập" hiển thị ngay trên khung nhập (1–1: tên người kia; nhóm: tối đa 3 tên + N).
  String _typingText(
    AppLocalizations l10n,
    ConversationDetail? detail,
    String currentUserId,
  ) {
    if (detail == null) {
      return '';
    }

    final typingIds = ref
        .read(chatRealtimeProvider)
        .typingUserIds(widget.conversationId)
      ..remove(currentUserId);
    if (typingIds.isEmpty) {
      return '';
    }

    final names = detail.members
        .where((member) => typingIds.contains(member.userId))
        .map((member) => member.displayName)
        .where((name) => name.isNotEmpty)
        .toList(growable: false);

    if (detail.isGroup) {
      if (names.isEmpty) {
        return l10n.t('chat_typing_many');
      }
      // Một người dùng câu số ít ("bob is typing..."), nhiều người dùng số nhiều.
      if (names.length == 1) {
        return '${names.first} ${l10n.t('chat_typing')}';
      }
      final visible = names.length > 3 ? names.sublist(0, 3) : names;
      final more = names.length > 3
          ? ' ${l10n.t('chat_and_more', {'count': '${names.length - 3}'})}'
          : '';
      return '${visible.join(', ')}$more ${l10n.t('chat_typing_many')}';
    }

    ConversationPeer? peer;
    for (final member in detail.members) {
      if (member.userId != currentUserId) {
        peer = member;
        break;
      }
    }
    return '${peer?.displayName ?? ''} ${l10n.t('chat_typing')}'.trim();
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
      final readers = detail.members
          .where((member) =>
              member.userId != currentUserId &&
              member.lastReadAt != null &&
              !member.lastReadAt!.isBefore(lastOwnMessage.createdAt))
          .toList()
        ..sort((first, second) => first.lastReadAt!.compareTo(second.lastReadAt!));

      final names = readers
          .map((member) => member.displayName)
          .where((name) => name.isNotEmpty)
          .toList(growable: false);
      if (names.isEmpty) {
        return l10n.t('chat_sent');
      }

      final visible = names.length > 3 ? names.sublist(0, 3) : names;
      final more = names.length > 3
          ? ' ${l10n.t('chat_and_more', {'count': '${names.length - 3}'})}'
          : '';
      return '${l10n.t('chat_seen_by')} ${visible.join(', ')}$more';
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
    final typingText = _typingText(l10n, detailValue, currentUserId);

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
          PopupMenuButton<String>(
            tooltip: l10n.t('chat_more'),
            onSelected: (value) {
              switch (value) {
                case 'pinned':
                  unawaited(_showPinnedSheet());
                case 'search':
                  unawaited(_openSearch());
                case 'mute':
                  unawaited(_showMuteSheet());
              }
            },
            itemBuilder: (context) => [
              PopupMenuItem(value: 'pinned', child: Text(l10n.t('chat_pinned'))),
              PopupMenuItem(
                value: 'search',
                child: Text(l10n.t('chat_search_in_conversation')),
              ),
              PopupMenuItem(value: 'mute', child: Text(l10n.t('chat_mute'))),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          // Banner ghim (NC Ưu tiên 2): hiện số tin ghim + tin ghim mới nhất.
          Consumer(
            builder: (context, ref, _) {
              final pinned =
                  ref.watch(pinnedMessagesProvider(widget.conversationId)).asData?.value;
              if (pinned == null || pinned.pinned.isEmpty) {
                return const SizedBox.shrink();
              }
              final first = pinned.pinned.first.message;
              return Material(
                color: Theme.of(context).colorScheme.surfaceContainerHighest,
                child: InkWell(
                  onTap: () => unawaited(_showPinnedSheet()),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                    child: Row(
                      children: [
                        const Icon(Icons.push_pin, size: 16),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            '${l10n.t('chat_pinned_count', {'count': '${pinned.pinned.length}'})}: '
                            '${first.isDeleted ? l10n.t('chat_recalled') : (first.content ?? l10n.t('chat_image'))}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 12),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
          if (_contextMessages != null)
            Container(
              color: Colors.amber.withValues(alpha: 0.15),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      l10n.t('chat_context_banner'),
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  TextButton(
                    onPressed: () => setState(() {
                      _contextMessages = null;
                      _highlightTerm = null;
                      _highlightMessageId = null;
                    }),
                    child: Text(l10n.t('chat_context_latest')),
                  ),
                ],
              ),
            ),
          Expanded(
            child: messages.when(
              skipLoadingOnRefresh: true,
              data: (history) {
                final merged = <String, ChatMessage>{
                  for (final message in _contextMessages ?? [..._olderMessages, ...history.messages])
                    message.id: message,
                }.values.toList(growable: false)
                  ..sort((first, second) => first.createdAt.compareTo(second.createdAt));
                final canLoad = _contextMessages == null &&
                    (_olderMessages.isEmpty ? history.hasMore : _olderHasMore);

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
                        pinnedMessageIds: {
                          for (final item in ref
                                  .watch(pinnedMessagesProvider(widget.conversationId))
                                  .asData
                                  ?.value
                                  .pinned ??
                              const <PinnedMessage>[])
                            item.message.id,
                        },
                        onLongPress: (message) => unawaited(_showMessageActions(message)),
                        onReact: (message, reaction) => unawaited(_react(message, reaction)),
                        highlightMessageId: _highlightMessageId,
                        highlightTerm: _highlightTerm,
                        targetMessageKey: _targetMessageKey,
                        contextMode: _contextMessages != null,
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
                  if (_replyTo != null || _editing != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Row(
                        children: [
                          Icon(
                            _editing != null ? Icons.edit_outlined : Icons.reply,
                            size: 16,
                          ),
                          const SizedBox(width: 6),
                          Expanded(
                            child: Text(
                              _editing != null
                                  ? l10n.t('chat_editing')
                                  : '${l10n.t('chat_reply_to')}: '
                                      '${_replyTo?.content ?? l10n.t('chat_image')}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 12),
                            ),
                          ),
                          IconButton(
                            visualDensity: VisualDensity.compact,
                            icon: const Icon(Icons.close, size: 16),
                            onPressed: _clearComposerAction,
                          ),
                        ],
                      ),
                    ),
                  // "Đang nhập" ngay trên khung nhập; giữ chiều cao cố định để không giật layout.
                  SizedBox(
                    height: 20,
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                        child: Text(
                          typingText,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 11,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                    ),
                  ),
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
                        onPressed: _sending || _editing != null ? null : _pickImage,
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
                            hintText: _editing != null
                                ? l10n.t('chat_edit_placeholder')
                                : l10n.t('chat_placeholder'),
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
                            : Icon(_editing != null ? Icons.check : Icons.send),
                        tooltip: _editing != null ? l10n.t('chat_save') : l10n.t('chat_send'),
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
    required this.pinnedMessageIds,
    required this.onLongPress,
    required this.onReact,
    required this.highlightMessageId,
    required this.highlightTerm,
    required this.targetMessageKey,
    required this.contextMode,
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
  final Set<String> pinnedMessageIds;
  final ValueChanged<ChatMessage> onLongPress;
  final void Function(ChatMessage message, String reaction) onReact;
  final String? highlightMessageId;
  final String? highlightTerm;
  final GlobalKey targetMessageKey;
  final bool contextMode;
  final String? lastOwnMessageId;
  final String? readLabel;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final reversed = messages.reversed.toList(growable: false);
    final itemCount = reversed.length + (hasMore ? 1 : 0);

    Widget buildMessage(ChatMessage message) {
      final isTarget = message.id == highlightMessageId;
      return _MessageBubble(
        key: isTarget ? targetMessageKey : null,
        message: message,
        isMine: message.senderId == currentUserId,
        isPinned: pinnedMessageIds.contains(message.id),
        senderName: showSenderName ? memberNames[message.senderId] ?? '' : '',
        statusLabel: message.id == lastOwnMessageId ? readLabel : null,
        highlightTerm: highlightTerm,
        onLongPress: () => onLongPress(message),
        onReact: (reaction) => onReact(message, reaction),
      );
    }

    // Chế độ ngữ cảnh chỉ có ~31 tin: dựng hết để `GlobalKey` của tin đích tồn tại
    // và `Scrollable.ensureVisible` cuộn tới được (ListView.builder chỉ dựng item đang thấy).
    if (contextMode) {
      return ListView(
        reverse: true,
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
        children: [for (final message in reversed) buildMessage(message)],
      );
    }

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

        return buildMessage(reversed[index]);
      },
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({
    super.key,
    required this.message,
    required this.isMine,
    required this.isPinned,
    required this.senderName,
    required this.onLongPress,
    required this.onReact,
    this.statusLabel,
    this.highlightTerm,
  });

  final ChatMessage message;
  final bool isMine;
  final bool isPinned;
  final String senderName;
  final VoidCallback onLongPress;
  final ValueChanged<String> onReact;
  final String? statusLabel;
  final String? highlightTerm;

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
    final maxWidth = MediaQuery.sizeOf(context).width * 0.78;

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Column(
        crossAxisAlignment: isMine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          GestureDetector(
            onLongPress: onLongPress,
            child: Container(
              constraints: BoxConstraints(maxWidth: maxWidth),
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
                  if (message.replyTo != null)
                    _QuoteBlock(
                      quote: message.replyTo!,
                      isMine: isMine,
                    ),
                  if (message.forwardedFrom != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Row(
                        children: [
                          Icon(
                            Icons.forward,
                            size: 12,
                            color: textColor.withValues(alpha: 0.7),
                          ),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(
                              '${l10n.t('chat_forwarded_from')} '
                              '${message.forwardedFrom!.senderDisplayName}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 11,
                                fontStyle: FontStyle.italic,
                                color: textColor.withValues(alpha: 0.75),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  if (message.isDeleted)
                    Text(
                      l10n.t('chat_recalled'),
                      style: TextStyle(
                        color: textColor.withValues(alpha: 0.7),
                        fontStyle: FontStyle.italic,
                        fontSize: 14,
                      ),
                    )
                  else ...[
                    if (message.attachments.isNotEmpty)
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
                    if (message.content != null)
                      message.type == 'Call'
                          ? Text(
                              l10n.t('chat_call'),
                              style: TextStyle(color: textColor, fontSize: 15),
                            )
                          : Text.rich(
                              TextSpan(
                                children: [
                                  for (final segment in splitHighlight(
                                    message.content!,
                                    highlightTerm,
                                  ))
                                    TextSpan(
                                      text: segment.text,
                                      style: segment.match
                                          ? const TextStyle(
                                              backgroundColor: Color(0xFFFFE082),
                                              color: Colors.black,
                                              fontWeight: FontWeight.w600,
                                            )
                                          : null,
                                    ),
                                ],
                              ),
                              style: TextStyle(color: textColor, fontSize: 15),
                            ),
                  ],
                  const SizedBox(height: 3),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (isPinned)
                        Padding(
                          padding: const EdgeInsets.only(right: 4),
                          child: Icon(
                            Icons.push_pin,
                            size: 11,
                            color: textColor.withValues(alpha: 0.7),
                          ),
                        ),
                      Text(
                        message.editedAt != null && !message.isDeleted
                            ? '$time · ${l10n.t('chat_edited')}'
                            : time,
                        style: TextStyle(
                          fontSize: 10,
                          color: textColor.withValues(alpha: 0.7),
                        ),
                      ),
                    ],
                  ),
                  if (statusLabel != null)
                    Text(
                      statusLabel!,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 10,
                        color: textColor.withValues(alpha: 0.8),
                      ),
                    ),
                ],
              ),
            ),
          ),
          if (!message.isDeleted && message.reactions.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(bottom: 2),
              child: Wrap(
                spacing: 4,
                children: [
                  for (final reaction in message.reactions)
                    InkWell(
                      borderRadius: BorderRadius.circular(12),
                      onTap: () => onReact(reaction.reaction),
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(12),
                          color: reaction.reactedByMe
                              ? colors.primaryContainer
                              : colors.surfaceContainerHighest,
                          border: Border.all(
                            color: reaction.reactedByMe ? colors.primary : colors.outlineVariant,
                          ),
                        ),
                        child: Text(
                          '${reaction.emoji} ${reaction.count}',
                          style: const TextStyle(fontSize: 11),
                        ),
                      ),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Khối trích dẫn tin được phản hồi trong bong bóng.
class _QuoteBlock extends StatelessWidget {
  const _QuoteBlock({required this.quote, required this.isMine});

  final MessageQuote quote;
  final bool isMine;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final textColor = isMine ? colors.onPrimaryContainer : colors.onSurface;

    return Container(
      margin: const EdgeInsets.only(bottom: 4),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
          left: BorderSide(color: isMine ? colors.onPrimaryContainer : colors.primary, width: 2),
        ),
        color: textColor.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            quote.senderDisplayName,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w700,
              color: textColor,
            ),
          ),
          Text(
            quote.isDeleted ? l10n.t('chat_recalled') : (quote.contentPreview ?? l10n.t('chat_image')),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 11,
              color: textColor.withValues(alpha: 0.8),
            ),
          ),
        ],
      ),
    );
  }
}
