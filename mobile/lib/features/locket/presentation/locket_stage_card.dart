import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/app_config.dart';
import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../data/locket_providers.dart';
import '../domain/locket_models.dart';
import 'locket_studio_page.dart';

/// Thẻ Comink Locket Kiểu A (Locket Live Stage) đặt ngay dưới thanh đăng bài trên Mobile.
class LocketStageCard extends ConsumerStatefulWidget {
  const LocketStageCard({super.key});

  @override
  ConsumerState<LocketStageCard> createState() => _LocketStageCardState();
}

class _LocketStageCardState extends ConsumerState<LocketStageCard> {
  final _replyController = TextEditingController();
  String _selectedAuthorId = '';
  int _activeIndex = 0;
  bool _sendingReply = false;
  bool _reacting = false;

  @override
  void dispose() {
    _replyController.dispose();
    super.dispose();
  }

  Future<void> _openStudio() async {
    await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        fullscreenDialog: true,
        builder: (_) => const LocketStudioPage(),
      ),
    );
  }

  Future<void> _react(LocketMoment moment, String emoji) async {
    if (_reacting) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _reacting = true);
    try {
      await ref.read(apiClientProvider).post(
        '/api/lockets/${moment.id}/reactions',
        data: {'emoji': emoji},
      );
      ref.invalidate(locketsProvider);
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    } finally {
      if (mounted) {
        setState(() => _reacting = false);
      }
    }
  }

  Future<void> _replyToMoment(LocketMoment moment) async {
    final text = _replyController.text.trim();
    if (text.isEmpty || _sendingReply) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final router = GoRouter.of(context);
    setState(() => _sendingReply = true);

    try {
      final data = await ref.read(apiClientProvider).post(
        '/api/lockets/${moment.id}/reply',
        data: {'content': text},
      );
      _replyController.clear();
      String? conversationId;
      if (data is Map && data['conversationId'] != null) {
        conversationId = data['conversationId'].toString();
      }
      if (mounted) {
        messenger.showSnackBar(
          SnackBar(
            content: Text(l10n.t('locket_reply_sent')),
            action: conversationId != null && conversationId.isNotEmpty
                ? SnackBarAction(
                    label: l10n.t('locket_open_chat'),
                    onPressed: () => router.push('/chats/$conversationId'),
                  )
                : null,
          ),
        );
      }
    } on ApiException catch (error) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
      }
    } finally {
      if (mounted) {
        setState(() => _sendingReply = false);
      }
    }
  }

  Future<void> _confirmDelete(LocketMoment moment) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final colors = Theme.of(context).colorScheme;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('locket_delete')),
        content: Text(l10n.t('locket_delete_confirm')),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.t('common_cancel')),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: colors.error,
              foregroundColor: colors.onError,
            ),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.t('locket_delete')),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }

    try {
      await ref.read(apiClientProvider).delete('/api/lockets/${moment.id}');
      ref.invalidate(locketsProvider);
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('locket_deleted'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
      }
    }
  }

  Future<void> _openHistoryGrid(List<LocketMoment> moments) async {
    final l10n = AppLocalizations.of(context);
    final chosenIndex = await showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) => FractionallySizedBox(
        heightFactor: 0.72,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(18, 14, 10, 10),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      l10n.t('locket_history_title'),
                      style: Theme.of(sheetContext).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(sheetContext).pop(),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: moments.isEmpty
                  ? Center(child: Text(l10n.t('locket_empty_title')))
                  : GridView.builder(
                      padding: const EdgeInsets.all(14),
                      itemCount: moments.length,
                      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                        crossAxisCount: 3,
                        mainAxisSpacing: 8,
                        crossAxisSpacing: 8,
                      ),
                      itemBuilder: (context, index) {
                        final item = moments[index];
                        return InkWell(
                          borderRadius: BorderRadius.circular(16),
                          onTap: () => Navigator.of(sheetContext).pop(index),
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(16),
                            child: Stack(
                              fit: StackFit.expand,
                              children: [
                                Image.network(
                                  AppConfig.resolveMediaUrl(item.imageUrl),
                                  fit: BoxFit.cover,
                                  errorBuilder: (_, _, _) => const ColoredBox(color: Colors.black26),
                                ),
                                Positioned(
                                  left: 6,
                                  right: 6,
                                  bottom: 6,
                                  child: Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: Colors.black54,
                                      borderRadius: BorderRadius.circular(8),
                                    ),
                                    child: Text(
                                      item.isMine ? l10n.t('locket_you') : item.author.displayName,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(color: Colors.white, fontSize: 10),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );

    if (chosenIndex != null && mounted) {
      setState(() => _activeIndex = chosenIndex);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final lockets = ref.watch(locketsProvider);

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Container(
                  width: 24,
                  height: 24,
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                      colors: [Color(0xFF0EA5E9), Color(0xFFF59E0B)],
                    ),
                    borderRadius: BorderRadius.circular(7),
                  ),
                  child: const Icon(Icons.auto_awesome, size: 14, color: Colors.white),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    l10n.t('locket_title'),
                    style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF59E0B).withValues(alpha: 0.16),
                    borderRadius: BorderRadius.circular(99),
                  ),
                  child: const Text(
                    '24h',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFFD97706),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            lockets.when(
              loading: () => const SizedBox(
                height: 220,
                child: Center(child: CircularProgressIndicator()),
              ),
              error: (_, _) => Column(
                children: [
                  Text(l10n.t('locket_load_error'), style: TextStyle(color: colors.error)),
                  const SizedBox(height: 8),
                  OutlinedButton(
                    onPressed: () => ref.invalidate(locketsProvider),
                    child: Text(l10n.t('common_retry')),
                  ),
                ],
              ),
              data: (allMoments) {
                // Tập tác giả duy nhất để lọc nhanh khi có từ 2 người trở lên.
                final authors = <String, String>{};
                for (final item in allMoments) {
                  authors.putIfAbsent(
                    item.author.id,
                    () => item.isMine ? l10n.t('locket_you') : item.author.displayName,
                  );
                }
                final filtered = _selectedAuthorId.isEmpty
                    ? allMoments
                    : allMoments.where((item) => item.author.id == _selectedAuthorId).toList();
                final safeIndex = _activeIndex < filtered.length ? _activeIndex : 0;
                final current = filtered.isEmpty ? null : filtered[safeIndex];

                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (authors.length > 1) ...[
                      SizedBox(
                        height: 32,
                        child: ListView(
                          scrollDirection: Axis.horizontal,
                          children: [
                            Padding(
                              padding: const EdgeInsets.only(right: 6),
                              child: ChoiceChip(
                                label: Text(l10n.t('locket_filter_all'),
                                    style: const TextStyle(fontSize: 11.5)),
                                selected: _selectedAuthorId.isEmpty,
                                onSelected: (_) => setState(() {
                                  _selectedAuthorId = '';
                                  _activeIndex = 0;
                                }),
                              ),
                            ),
                            ...authors.entries.map(
                              (entry) => Padding(
                                padding: const EdgeInsets.only(right: 6),
                                child: ChoiceChip(
                                  label: Text(entry.value, style: const TextStyle(fontSize: 11.5)),
                                  selected: _selectedAuthorId == entry.key,
                                  onSelected: (_) => setState(() {
                                    _selectedAuthorId = entry.key;
                                    _activeIndex = 0;
                                  }),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 10),
                    ],

                    // Khung Locket Kiểu A: vuông bo góc lớn (giới hạn chiều rộng để không phình trên màn hình rộng) + caption pill nổi
                    if (current == null)
                      Container(
                        height: 136,
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(24),
                          border: Border.all(
                            color: const Color(0xFFF59E0B).withValues(alpha: 0.4),
                            width: 1.5,
                          ),
                          color: const Color(0xFF0E1320),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: InkWell(
                          onTap: _openStudio,
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                const Icon(Icons.camera_alt_outlined,
                                    size: 32, color: Color(0xFFFBBF24)),
                                const SizedBox(height: 8),
                                Text(
                                  l10n.t('locket_empty_title'),
                                  textAlign: TextAlign.center,
                                  style: const TextStyle(
                                    color: Colors.white,
                                    fontWeight: FontWeight.w700,
                                    fontSize: 13,
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  l10n.t('locket_empty_hint'),
                                  textAlign: TextAlign.center,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(color: Colors.white70, fontSize: 11.5),
                                ),
                              ],
                            ),
                          ),
                        ),
                      )
                    else
                      Center(
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 280),
                          child: AspectRatio(
                            aspectRatio: 1,
                            child: Container(
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(28),
                                border: Border.all(
                                  color: const Color(0xFFF59E0B).withValues(alpha: 0.4),
                                  width: 1.5,
                                ),
                                color: const Color(0xFF0E1320),
                              ),
                              clipBehavior: Clip.antiAlias,
                              child: GestureDetector(
                                onHorizontalDragEnd: filtered.length > 1
                                    ? (details) {
                                        final v = details.primaryVelocity ?? 0;
                                        if (v < -150) {
                                          setState(() =>
                                              _activeIndex = (safeIndex + 1) % filtered.length);
                                        } else if (v > 150) {
                                          setState(() => _activeIndex =
                                              (safeIndex - 1 + filtered.length) % filtered.length);
                                        }
                                      }
                                    : null,
                                child: Stack(
                                  fit: StackFit.expand,
                                  children: [
                                    Image.network(
                                      AppConfig.resolveMediaUrl(current.imageUrl),
                                      fit: BoxFit.cover,
                                      errorBuilder: (_, _, _) => const Center(
                                        child: Icon(Icons.broken_image_outlined,
                                            color: Colors.white54, size: 42),
                                      ),
                                    ),
                                    Positioned(
                                      top: 10,
                                      left: 10,
                                      right: 10,
                                      child: Row(
                                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                        children: [
                                          Flexible(
                                            child: Container(
                                              padding: const EdgeInsets.fromLTRB(4, 3, 10, 3),
                                              decoration: BoxDecoration(
                                                color: Colors.black54,
                                                borderRadius: BorderRadius.circular(99),
                                              ),
                                              child: Row(
                                                mainAxisSize: MainAxisSize.min,
                                                children: [
                                                  AppAvatar(
                                                    name: current.author.displayName,
                                                    url: current.author.avatarUrl,
                                                    radius: 11,
                                                  ),
                                                  const SizedBox(width: 6),
                                                  Flexible(
                                                    child: Text(
                                                      current.isMine
                                                          ? l10n.t('locket_you')
                                                          : current.author.displayName,
                                                      maxLines: 1,
                                                      overflow: TextOverflow.ellipsis,
                                                      style: const TextStyle(
                                                        color: Colors.white,
                                                        fontSize: 11.5,
                                                        fontWeight: FontWeight.w700,
                                                      ),
                                                    ),
                                                  ),
                                                ],
                                              ),
                                            ),
                                          ),
                                          const SizedBox(width: 6),
                                          Row(
                                            mainAxisSize: MainAxisSize.min,
                                            children: [
                                              Container(
                                                padding: const EdgeInsets.symmetric(
                                                    horizontal: 8, vertical: 4),
                                                decoration: BoxDecoration(
                                                  color: Colors.black54,
                                                  borderRadius: BorderRadius.circular(99),
                                                ),
                                                child: Text(
                                                  current.remainingLabel(l10n),
                                                  style: const TextStyle(
                                                    color: Color(0xFFFDE68A),
                                                    fontSize: 10.5,
                                                    fontWeight: FontWeight.w600,
                                                  ),
                                                ),
                                              ),
                                              if (current.isMine) ...[
                                                const SizedBox(width: 4),
                                                Material(
                                                  color: Colors.black54,
                                                  shape: const CircleBorder(),
                                                  child: InkWell(
                                                    customBorder: const CircleBorder(),
                                                    onTap: () => _confirmDelete(current),
                                                    child: const Padding(
                                                      padding: EdgeInsets.all(6),
                                                      child: Icon(
                                                        Icons.delete_outline,
                                                        size: 15,
                                                        color: Color(0xFFFCA5A5),
                                                      ),
                                                    ),
                                                  ),
                                                ),
                                              ],
                                            ],
                                          ),
                                        ],
                                      ),
                                    ),
                                    if (current.caption != null)
                                      Positioned(
                                        left: 14,
                                        right: 14,
                                        bottom: 12,
                                        child: Center(
                                          child: Container(
                                            padding: const EdgeInsets.symmetric(
                                                horizontal: 14, vertical: 6),
                                            decoration: BoxDecoration(
                                              color: Colors.black.withValues(alpha: 0.62),
                                              borderRadius: BorderRadius.circular(99),
                                            ),
                                            child: Text(
                                              current.caption!,
                                              maxLines: 2,
                                              overflow: TextOverflow.ellipsis,
                                              textAlign: TextAlign.center,
                                              style: const TextStyle(
                                                color: Colors.white,
                                                fontSize: 12.5,
                                                fontWeight: FontWeight.w600,
                                              ),
                                            ),
                                          ),
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    const SizedBox(height: 12),

                    // Bộ điều khiển 3 nút kiểu Locket: Lịch sử — Nút chụp vòng vàng — Ảnh tiếp
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceAround,
                      children: [
                        InkWell(
                          borderRadius: BorderRadius.circular(14),
                          onTap: () => _openHistoryGrid(filtered),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                            child: Column(
                              children: [
                                Container(
                                  width: 38,
                                  height: 38,
                                  decoration: BoxDecoration(
                                    color: colors.surfaceContainerHighest,
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: Icon(Icons.grid_view_rounded,
                                      size: 19, color: colors.onSurfaceVariant),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  l10n.t('locket_history', {'count': '${filtered.length}'}),
                                  style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
                                ),
                              ],
                            ),
                          ),
                        ),
                        GestureDetector(
                          onTap: _openStudio,
                          child: Container(
                            width: 56,
                            height: 56,
                            padding: const EdgeInsets.all(3.5),
                            decoration: BoxDecoration(
                              shape: BoxShape.circle,
                              border: Border.all(color: const Color(0xFFF59E0B), width: 3),
                            ),
                            child: Container(
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                color: colors.surfaceContainerHighest,
                              ),
                              child: Icon(Icons.camera_alt_rounded,
                                  size: 24, color: colors.onSurface),
                            ),
                          ),
                        ),
                        InkWell(
                          borderRadius: BorderRadius.circular(14),
                          onTap: filtered.length > 1
                              ? () => setState(() => _activeIndex = (safeIndex + 1) % filtered.length)
                              : null,
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                            child: Column(
                              children: [
                                Container(
                                  width: 38,
                                  height: 38,
                                  decoration: BoxDecoration(
                                    color: colors.surfaceContainerHighest,
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: Icon(
                                    Icons.chevron_right_rounded,
                                    size: 22,
                                    color: filtered.length > 1
                                        ? colors.onSurface
                                        : colors.onSurfaceVariant.withValues(alpha: 0.4),
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  l10n.t('locket_next'),
                                  style: TextStyle(fontSize: 11, color: colors.onSurfaceVariant),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),

                    if (current != null) ...[
                      const SizedBox(height: 10),
                      Wrap(
                        spacing: 6,
                        runSpacing: 6,
                        alignment: WrapAlignment.center,
                        children: locketAllowedReactions.map((emoji) {
                          LocketReactionSummary? summary;
                          for (final item in current.reactions) {
                            if (item.emoji == emoji) {
                              summary = item;
                              break;
                            }
                          }
                          final active = summary?.reactedByMe == true;
                          return InkWell(
                            borderRadius: BorderRadius.circular(99),
                            onTap: _reacting ? null : () => _react(current, emoji),
                            child: Container(
                              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                              decoration: BoxDecoration(
                                color: active
                                    ? colors.primaryContainer
                                    : colors.surfaceContainerHighest,
                                borderRadius: BorderRadius.circular(99),
                                border: active
                                    ? Border.all(color: colors.primary)
                                    : null,
                              ),
                              child: Text(
                                summary != null && summary.count > 0
                                    ? '$emoji ${summary.count}'
                                    : emoji,
                                style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600),
                              ),
                            ),
                          );
                        }).toList(growable: false),
                      ),
                      if (!current.isMine) ...[
                        const SizedBox(height: 10),
                        Row(
                          children: [
                            Expanded(
                              child: TextField(
                                controller: _replyController,
                                maxLength: 1000,
                                decoration: InputDecoration(
                                  counterText: '',
                                  isDense: true,
                                  hintText: l10n.t('locket_reply_placeholder'),
                                  contentPadding: const EdgeInsets.symmetric(
                                      horizontal: 14, vertical: 10),
                                ),
                                onSubmitted: (_) => _replyToMoment(current),
                              ),
                            ),
                            const SizedBox(width: 8),
                            IconButton.filled(
                              onPressed: _sendingReply ? null : () => _replyToMoment(current),
                              icon: _sendingReply
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(
                                          strokeWidth: 2, color: Colors.white),
                                    )
                                  : const Icon(Icons.send_rounded, size: 18),
                            ),
                          ],
                        ),
                      ],
                    ],
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
