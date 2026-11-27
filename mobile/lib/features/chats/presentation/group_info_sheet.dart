import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../../friends/data/friends_providers.dart';
import '../../media/data/media_service.dart';
import '../data/chat_providers.dart';
import '../domain/chat_models.dart';

/// Mở bottom sheet thông tin và quản lý nhóm chat; trả về `true` nếu người dùng vừa rời nhóm.
Future<bool?> showGroupInfoSheet(
  BuildContext context, {
  required String conversationId,
}) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (_) => FractionallySizedBox(
      heightFactor: 0.88,
      child: GroupInfoSheet(conversationId: conversationId),
    ),
  );
}

/// Bảng thông tin và quản lý nhóm: ảnh đại diện, đổi tên, thêm/kick thành viên và rời nhóm.
class GroupInfoSheet extends ConsumerStatefulWidget {
  const GroupInfoSheet({
    super.key,
    required this.conversationId,
  });

  final String conversationId;

  @override
  ConsumerState<GroupInfoSheet> createState() => _GroupInfoSheetState();
}

class _GroupInfoSheetState extends ConsumerState<GroupInfoSheet> {
  static const int _maxMembers = 10;

  bool _showAddPanel = false;
  final Set<String> _selectedFriendIds = {};
  bool _uploadingAvatar = false;
  bool _busyAction = false;
  String? _errorCode;

  void _invalidateConversation() {
    ref.invalidate(conversationDetailProvider(widget.conversationId));
    ref.invalidate(conversationsProvider);
  }

  Future<void> _handleAvatarTap({required bool hasExisting}) async {
    if (_uploadingAvatar || _busyAction) {
      return;
    }

    if (!hasExisting) {
      await _pickAndUploadAvatar();
      return;
    }

    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final choice = await showModalBottomSheet<String>(
      context: context,
      useSafeArea: true,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: Icon(Icons.photo_library_outlined, color: colors.primary),
              title: Text(l10n.t('chat_group_change_avatar')),
              onTap: () => Navigator.of(sheetContext).pop('change'),
            ),
            ListTile(
              leading: Icon(Icons.delete_outline, color: colors.error),
              title: Text(
                l10n.t('chat_group_remove_avatar'),
                style: TextStyle(color: colors.error),
              ),
              onTap: () => Navigator.of(sheetContext).pop('remove'),
            ),
          ],
        ),
      ),
    );

    if (!mounted) {
      return;
    }
    if (choice == 'change') {
      await _pickAndUploadAvatar();
    } else if (choice == 'remove') {
      await _updateGroupAvatar('');
    }
  }

  Future<void> _pickAndUploadAvatar() async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);

    XFile? picked;
    try {
      picked = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        imageQuality: 85,
        maxWidth: 1024,
      );
    } catch (_) {
      if (mounted) {
        setState(() => _errorCode = 'MEDIA_UPLOAD_FAILED');
      }
      return;
    }
    if (picked == null || !mounted) {
      return;
    }

    setState(() {
      _uploadingAvatar = true;
      _errorCode = null;
    });

    try {
      final asset = await ref.read(mediaServiceProvider).upload(
            picked,
            context: 'Avatar',
          );
      await ref.read(apiClientProvider).patch(
        '/api/conversations/${widget.conversationId}/group',
        data: {'avatarUrl': asset.url},
      );
      _invalidateConversation();
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('chat_group_avatar_updated'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _errorCode = 'MEDIA_UPLOAD_FAILED');
      }
    } finally {
      if (mounted) {
        setState(() => _uploadingAvatar = false);
      }
    }
  }

  Future<void> _updateGroupAvatar(String url) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);

    setState(() {
      _uploadingAvatar = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).patch(
        '/api/conversations/${widget.conversationId}/group',
        data: {'avatarUrl': url},
      );
      _invalidateConversation();
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('chat_group_avatar_updated'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } finally {
      if (mounted) {
        setState(() => _uploadingAvatar = false);
      }
    }
  }

  Future<void> _promptRename(String currentTitle) async {
    if (_busyAction || _uploadingAvatar) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final controller = TextEditingController(text: currentTitle);

    final nextTitle = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('chat_group_rename')),
        content: TextField(
          controller: controller,
          maxLength: 100,
          autofocus: true,
          decoration: InputDecoration(labelText: l10n.t('chats_group_name')),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: Text(l10n.t('common_cancel')),
          ),
          FilledButton(
            onPressed: () {
              final trimmed = controller.text.trim();
              if (trimmed.isNotEmpty) {
                Navigator.of(dialogContext).pop(trimmed);
              }
            },
            child: Text(l10n.t('common_save')),
          ),
        ],
      ),
    );
    controller.dispose();

    if (nextTitle == null || nextTitle == currentTitle.trim() || !mounted) {
      return;
    }

    setState(() {
      _busyAction = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).patch(
        '/api/conversations/${widget.conversationId}/group',
        data: {'title': nextTitle},
      );
      _invalidateConversation();
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('chat_group_renamed'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } finally {
      if (mounted) {
        setState(() => _busyAction = false);
      }
    }
  }

  Future<void> _addSelectedMembers() async {
    if (_busyAction || _selectedFriendIds.isEmpty) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);

    setState(() {
      _busyAction = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).post(
        '/api/conversations/${widget.conversationId}/members',
        data: {'memberIds': _selectedFriendIds.toList(growable: false)},
      );
      _invalidateConversation();
      if (mounted) {
        setState(() {
          _selectedFriendIds.clear();
          _showAddPanel = false;
        });
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('chat_group_members_added'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } finally {
      if (mounted) {
        setState(() => _busyAction = false);
      }
    }
  }

  Future<void> _confirmKickMember(ConversationPeer member) async {
    if (_busyAction) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final colors = Theme.of(context).colorScheme;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('chat_group_kick')),
        content: Text(l10n.t('chat_group_kick_confirm', {'name': member.displayName})),
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
            child: Text(l10n.t('chat_group_kick')),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) {
      return;
    }

    setState(() {
      _busyAction = true;
      _errorCode = null;
    });

    try {
      await ref
          .read(apiClientProvider)
          .delete('/api/conversations/${widget.conversationId}/members/${member.userId}');
      _invalidateConversation();
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('chat_group_member_removed'))));
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } finally {
      if (mounted) {
        setState(() => _busyAction = false);
      }
    }
  }

  Future<void> _confirmLeaveGroup({
    required bool isOwner,
    required int memberCount,
  }) async {
    if (_busyAction) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final navigator = Navigator.of(context);

    final confirmMessage = isOwner && memberCount > 1
        ? l10n.t('chat_group_leave_owner_confirm')
        : l10n.t('chat_group_leave_confirm');

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('chat_group_leave')),
        content: Text(confirmMessage),
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
            child: Text(l10n.t('chat_group_leave')),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) {
      return;
    }

    setState(() {
      _busyAction = true;
      _errorCode = null;
    });

    try {
      await ref.read(apiClientProvider).post('/api/conversations/${widget.conversationId}/leave');
      _invalidateConversation();
      if (mounted) {
        navigator.pop(true);
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _errorCode = error.code);
      }
    } finally {
      if (mounted) {
        setState(() => _busyAction = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final detail = ref.watch(conversationDetailProvider(widget.conversationId));
    final currentUserId = ref.watch(sessionControllerProvider).user?.id ?? '';

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 14, 12, 10),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  l10n.t('chat_group_info'),
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                ),
              ),
              IconButton(
                tooltip: l10n.t('common_close'),
                onPressed: () => Navigator.of(context).pop(false),
                icon: const Icon(Icons.close),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: detail.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (error, _) => Center(
              child: ErrorState(
                message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
                retryLabel: l10n.t('common_retry'),
                onRetry: () => ref.invalidate(conversationDetailProvider(widget.conversationId)),
              ),
            ),
            data: (group) {
              final title = group.title ?? l10n.t('chat_group');
              final hasAvatar = group.avatarUrl != null && group.avatarUrl!.isNotEmpty;
              final isOwner = group.members.any(
                (member) => member.userId == currentUserId && member.isOwner,
              );
              final existingIds = group.members.map((member) => member.userId).toSet();
              final remainingSlots = (_maxMembers - group.members.length).clamp(0, _maxMembers);

              return ListView(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                children: [
                  Center(
                    child: Stack(
                      clipBehavior: Clip.none,
                      children: [
                        if (hasAvatar)
                          AppAvatar(name: title, url: group.avatarUrl, radius: 36)
                        else
                          CircleAvatar(
                            radius: 36,
                            backgroundColor: colors.primaryContainer,
                            child: Icon(
                              Icons.groups,
                              size: 36,
                              color: colors.onPrimaryContainer,
                            ),
                          ),
                        Positioned(
                          right: -4,
                          bottom: -4,
                          child: Material(
                            color: colors.primary,
                            shape: CircleBorder(
                              side: BorderSide(color: colors.surface, width: 2),
                            ),
                            child: InkWell(
                              customBorder: const CircleBorder(),
                              onTap: _uploadingAvatar || _busyAction
                                  ? null
                                  : () => _handleAvatarTap(hasExisting: hasAvatar),
                              child: Padding(
                                padding: const EdgeInsets.all(6),
                                child: _uploadingAvatar
                                    ? const SizedBox(
                                        width: 14,
                                        height: 14,
                                        child: CircularProgressIndicator(
                                          strokeWidth: 2,
                                          color: Colors.white,
                                        ),
                                      )
                                    : const Icon(
                                        Icons.camera_alt_outlined,
                                        size: 14,
                                        color: Colors.white,
                                      ),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Flexible(
                        child: Text(
                          title,
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                      ),
                      const SizedBox(width: 4),
                      IconButton(
                        tooltip: l10n.t('chat_group_rename'),
                        onPressed: _busyAction ? null : () => _promptRename(title),
                        icon: const Icon(Icons.edit_outlined, size: 18),
                      ),
                    ],
                  ),
                  Center(
                    child: Text(
                      '${group.members.length}/$_maxMembers ${l10n.t('chat_members')}',
                      style: TextStyle(fontSize: 12.5, color: colors.onSurfaceVariant),
                    ),
                  ),
                  if (_errorCode != null) ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      decoration: BoxDecoration(
                        color: colors.errorContainer,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        l10n.error(_errorCode),
                        style: TextStyle(color: colors.onErrorContainer, fontSize: 13),
                      ),
                    ),
                  ],
                  const SizedBox(height: 18),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          '${l10n.t('chat_group_members')} (${group.members.length})',
                          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                      ),
                      FilledButton.tonalIcon(
                        onPressed: remainingSlots == 0 || _busyAction
                            ? null
                            : () => setState(() {
                                  _showAddPanel = !_showAddPanel;
                                  _selectedFriendIds.clear();
                                  _errorCode = null;
                                }),
                        icon: Icon(_showAddPanel ? Icons.close : Icons.person_add_outlined, size: 18),
                        label: Text(
                          _showAddPanel ? l10n.t('common_cancel') : l10n.t('chat_group_add_members'),
                        ),
                      ),
                    ],
                  ),
                  if (remainingSlots == 0)
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        l10n.t('chat_group_full'),
                        style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
                      ),
                    ),
                  if (_showAddPanel)
                    _buildAddMembersPanel(
                      context,
                      l10n: l10n,
                      existingIds: existingIds,
                      remainingSlots: remainingSlots,
                    ),
                  const SizedBox(height: 10),
                  ...group.members.map(
                    (member) => _MemberTile(
                      member: member,
                      isSelf: member.userId == currentUserId,
                      canKick: isOwner && member.userId != currentUserId && !_busyAction,
                      onTap: member.userId == currentUserId
                          ? null
                          : () {
                              final router = GoRouter.of(context);
                              Navigator.of(context).pop(false);
                              router.push('/users/${member.userId}');
                            },
                      onKick: () => _confirmKickMember(member),
                    ),
                  ),
                  const SizedBox(height: 20),
                  OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(
                      foregroundColor: colors.error,
                      side: BorderSide(color: colors.error.withValues(alpha: 0.5)),
                      padding: const EdgeInsets.symmetric(vertical: 12),
                    ),
                    onPressed: _busyAction
                        ? null
                        : () => _confirmLeaveGroup(
                              isOwner: isOwner,
                              memberCount: group.members.length,
                            ),
                    icon: const Icon(Icons.logout),
                    label: Text(l10n.t('chat_group_leave')),
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildAddMembersPanel(
    BuildContext context, {
    required AppLocalizations l10n,
    required Set<String> existingIds,
    required int remainingSlots,
  }) {
    final colors = Theme.of(context).colorScheme;
    final friends = ref.watch(friendsProvider);

    return Container(
      margin: const EdgeInsets.only(top: 10, bottom: 6),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colors.outlineVariant),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            l10n.t('chat_group_slots_left', {'count': '$remainingSlots'}),
            style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant),
          ),
          const SizedBox(height: 8),
          friends.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (_, _) => Text(
              l10n.t('friends_error'),
              style: TextStyle(color: colors.error, fontSize: 13),
            ),
            data: (items) {
              final available = items
                  .where((item) => !existingIds.contains(item.user.id))
                  .toList(growable: false);
              if (available.isEmpty) {
                return Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Text(
                    l10n.t('chat_group_no_more_friends'),
                    style: TextStyle(fontSize: 13, color: colors.onSurfaceVariant),
                  ),
                );
              }

              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxHeight: 220),
                    child: ListView.builder(
                      shrinkWrap: true,
                      itemCount: available.length,
                      itemBuilder: (context, index) {
                        final friend = available[index].user;
                        final selected = _selectedFriendIds.contains(friend.id);
                        final canSelect = selected || _selectedFriendIds.length < remainingSlots;
                        return CheckboxListTile(
                          dense: true,
                          contentPadding: EdgeInsets.zero,
                          value: selected,
                          onChanged: _busyAction || !canSelect
                              ? null
                              : (value) => setState(() {
                                    if (value == true) {
                                      _selectedFriendIds.add(friend.id);
                                    } else {
                                      _selectedFriendIds.remove(friend.id);
                                    }
                                  }),
                          secondary: AppAvatar(
                            name: friend.displayName,
                            url: friend.avatarUrl,
                            radius: 16,
                          ),
                          title: Text(friend.displayName, maxLines: 1, overflow: TextOverflow.ellipsis),
                          subtitle: Text('@${friend.userName}', maxLines: 1, overflow: TextOverflow.ellipsis),
                        );
                      },
                    ),
                  ),
                  const SizedBox(height: 8),
                  FilledButton(
                    onPressed: _busyAction || _selectedFriendIds.isEmpty ? null : _addSelectedMembers,
                    child: _busyAction
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Text('${l10n.t('chat_group_add_submit')} (${_selectedFriendIds.length})'),
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _MemberTile extends StatelessWidget {
  const _MemberTile({
    required this.member,
    required this.isSelf,
    required this.canKick,
    required this.onKick,
    this.onTap,
  });

  final ConversationPeer member;
  final bool isSelf;
  final bool canKick;
  final VoidCallback onKick;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        onTap: onTap,
        leading: Stack(
          clipBehavior: Clip.none,
          children: [
            AppAvatar(name: member.displayName, url: member.avatarUrl, radius: 20),
            if (member.isOnline)
              Positioned(
                right: 0,
                bottom: 0,
                child: Container(
                  width: 11,
                  height: 11,
                  decoration: BoxDecoration(
                    color: const Color(0xFF10B981),
                    shape: BoxShape.circle,
                    border: Border.all(color: colors.surface, width: 1.5),
                  ),
                ),
              ),
          ],
        ),
        title: Row(
          children: [
            Flexible(
              child: Text(
                member.displayName,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
            if (member.isOwner) ...[
              const SizedBox(width: 6),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                decoration: BoxDecoration(
                  color: const Color(0xFFFEF3C7),
                  borderRadius: BorderRadius.circular(99),
                ),
                child: Text(
                  l10n.t('chat_group_owner'),
                  style: const TextStyle(
                    fontSize: 10.5,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF92400E),
                  ),
                ),
              ),
            ],
            if (isSelf) ...[
              const SizedBox(width: 6),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                decoration: BoxDecoration(
                  color: colors.primaryContainer,
                  borderRadius: BorderRadius.circular(99),
                ),
                child: Text(
                  l10n.t('chat_group_you'),
                  style: TextStyle(
                    fontSize: 10.5,
                    fontWeight: FontWeight.w700,
                    color: colors.onPrimaryContainer,
                  ),
                ),
              ),
            ],
          ],
        ),
        subtitle: member.userName != null && member.userName!.isNotEmpty
            ? Text('@${member.userName}', maxLines: 1, overflow: TextOverflow.ellipsis)
            : null,
        trailing: canKick
            ? IconButton(
                tooltip: l10n.t('chat_group_kick'),
                onPressed: onKick,
                color: colors.error,
                icon: const Icon(Icons.person_remove_outlined),
              )
            : null,
      ),
    );
  }
}
