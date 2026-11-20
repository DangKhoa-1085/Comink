import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/models/paged_result.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/states.dart';
import '../domain/chat_models.dart';

/// Kết quả người dùng chọn từ trang tìm kiếm: tin + từ khoá đã tìm (để tô sáng).
class MessageSearchSelection {
  const MessageSearchSelection({required this.result, required this.query});

  final MessageSearchResult result;
  final String query;
}

/// Tìm tin nhắn (NC Ưu tiên 2). `conversationId == null` → tìm trong mọi hội thoại
/// người dùng còn là thành viên; backend tự lọc quyền và gõ không dấu vẫn khớp.
class MessageSearchPage extends ConsumerStatefulWidget {
  const MessageSearchPage({super.key, this.conversationId});

  final String? conversationId;

  @override
  ConsumerState<MessageSearchPage> createState() => _MessageSearchPageState();
}

class _MessageSearchPageState extends ConsumerState<MessageSearchPage> {
  final _controller = TextEditingController();
  List<MessageSearchResult> _results = const [];
  String? _cursor;
  bool _hasMore = false;
  bool _loading = false;
  bool _loadingMore = false;
  String? _errorCode;
  String _submitted = '';

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _search({bool append = false}) async {
    final keyword = append ? _submitted : _controller.text.trim();
    if (keyword.length < 2 || _loading || _loadingMore) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    setState(() {
      if (append) {
        _loadingMore = true;
      } else {
        _loading = true;
        _submitted = keyword;
        _results = const [];
        _cursor = null;
        _hasMore = false;
      }
      _errorCode = null;
    });

    final path = widget.conversationId == null
        ? '/api/messages/search'
        : '/api/conversations/${widget.conversationId}/messages/search';

    try {
      final data = await ref.read(apiClientProvider).get(
            path,
            query: {
              'q': keyword,
              'limit': 20,
              if (append && _cursor != null) 'cursor': _cursor,
            },
          );
      final page = PagedResult.fromJson(data, MessageSearchResult.fromJson);
      if (!mounted) {
        return;
      }
      setState(() {
        _results = append ? [..._results, ...page.items] : page.items;
        _cursor = page.nextCursor;
        _hasMore = page.hasMore;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() => _errorCode = l10n.error(error.code));
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
          _loadingMore = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text(
          widget.conversationId == null
              ? l10n.t('chat_search_all_messages')
              : l10n.t('chat_search_in_conversation'),
        ),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(64),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 10),
            child: TextField(
              controller: _controller,
              autofocus: true,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => _search(),
              decoration: InputDecoration(
                hintText: l10n.t('chat_search_placeholder'),
                prefixIcon: const Icon(Icons.search),
                suffixIcon: IconButton(
                  icon: const Icon(Icons.arrow_forward),
                  onPressed: _loading ? null : _search,
                ),
              ),
            ),
          ),
        ),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _errorCode != null
              ? ErrorState(
                  message: _errorCode!,
                  retryLabel: l10n.t('common_retry'),
                  onRetry: () => _search(),
                )
              : _submitted.isEmpty
                  ? EmptyState(
                      message: l10n.t('chat_search_hint'),
                      icon: Icons.manage_search,
                    )
                  : _results.isEmpty
                      ? EmptyState(
                          message: l10n.t('chat_search_no_results'),
                          icon: Icons.search_off,
                        )
                      : ListView.separated(
                          itemCount: _results.length + (_hasMore ? 1 : 0),
                          separatorBuilder: (_, _) => const Divider(height: 1),
                          itemBuilder: (context, index) {
                            if (index == _results.length) {
                              return Padding(
                                padding: const EdgeInsets.all(8),
                                child: Center(
                                  child: _loadingMore
                                      ? const SizedBox(
                                          height: 18,
                                          width: 18,
                                          child: CircularProgressIndicator(strokeWidth: 2),
                                        )
                                      : TextButton(
                                          onPressed: () => _search(append: true),
                                          child: Text(l10n.t('chat_load_older')),
                                        ),
                                ),
                              );
                            }

                            final result = _results[index];
                            final time =
                                MaterialLocalizations.of(context).formatShortDate(result.createdAt);
                            return ListTile(
                              title: Text(
                                result.contentPreview ?? l10n.t('chat_image'),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                              subtitle: Text(
                                '${result.conversationLabel} · ${result.senderDisplayName} · $time',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                              onTap: () => Navigator.of(context).pop(
                                MessageSearchSelection(
                                  result: result,
                                  query: _submitted,
                                ),
                              ),
                            );
                          },
                        ),
    );
  }
}
