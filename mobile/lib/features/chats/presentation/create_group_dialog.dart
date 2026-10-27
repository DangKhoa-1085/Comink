import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../friends/data/friends_providers.dart';
import '../data/chat_providers.dart';

/// Hộp thoại tạo nhóm chat 3–10 người (mời từ danh sách bạn bè).
class CreateGroupDialog extends ConsumerStatefulWidget {
  const CreateGroupDialog({super.key});

  @override
  ConsumerState<CreateGroupDialog> createState() => _CreateGroupDialogState();
}

class _CreateGroupDialogState extends ConsumerState<CreateGroupDialog> {
  final _nameController = TextEditingController();
  final Set<String> _selected = {};
  bool _creating = false;
  String? _errorCode;

  static const int _minMembers = 3;
  static const int _maxMembers = 10;

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _create() async {
    final name = _nameController.text.trim();
    if (_creating) {
      return;
    }
    if (name.isEmpty || _selected.length < _minMembers - 1) {
      setState(() => _errorCode = 'VALIDATION_FAILED');
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _creating = true;
      _errorCode = null;
    });

    try {
      final data = await ref.read(apiClientProvider).post(
        '/api/conversations/group',
        data: {
          'name': name,
          'participantIds': _selected.toList(growable: false),
        },
      );
      ref.invalidate(conversationsProvider);
      if (!mounted || data is! Map) {
        return;
      }
      final id = (data['id'] ?? '').toString();
      Navigator.pop(context, id.isEmpty ? null : id);
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('chats_group_created'))));
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _creating = false;
        _errorCode = error.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final friends = ref.watch(friendsProvider);

    return AlertDialog(
      title: Text(l10n.t('chats_create_group')),
      content: SizedBox(
        width: 420,
        height: 430,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextField(
              controller: _nameController,
              maxLength: 100,
              decoration: InputDecoration(labelText: l10n.t('chats_group_name')),
            ),
            Text(
              l10n.t('chats_group_min'),
              style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: friends.when(
                data: (items) => items.isEmpty
                    ? Center(child: Text(l10n.t('friends_empty')))
                    : ListView.builder(
                        itemCount: items.length,
                        itemBuilder: (context, index) {
                          final friend = items[index].user;
                          final selected = _selected.contains(friend.id);
                          final canSelect = selected || _selected.length < _maxMembers - 1;
                          return CheckboxListTile(
                            value: selected,
                            onChanged: _creating || !canSelect
                                ? null
                                : (value) => setState(() {
                                      if (value == true) {
                                        _selected.add(friend.id);
                                      } else {
                                        _selected.remove(friend.id);
                                      }
                                    }),
                            secondary: AppAvatar(
                              name: friend.displayName,
                              url: friend.avatarUrl,
                              radius: 16,
                            ),
                            title: Text(friend.displayName),
                            subtitle: Text('@${friend.userName}'),
                          );
                        },
                      ),
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (_, _) => Center(child: Text(l10n.t('friends_error'))),
              ),
            ),
            if (_errorCode != null)
              Text(
                _errorCode == 'VALIDATION_FAILED'
                    ? l10n.t('chats_group_min')
                    : l10n.error(_errorCode),
                style: TextStyle(color: Theme.of(context).colorScheme.error, fontSize: 12),
              ),
            Text(
              '${_selected.length + 1}/$_maxMembers',
              style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _creating ? null : () => Navigator.pop(context),
          child: Text(l10n.t('common_cancel')),
        ),
        FilledButton(
          onPressed: _creating ? null : _create,
          child: _creating
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : Text(l10n.t('chats_create')),
        ),
      ],
    );
  }
}
