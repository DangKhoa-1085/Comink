import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/i18n/app_localizations.dart';
import '../../core/i18n/locale_controller.dart';
import '../../core/network/api_exception.dart';
import '../../core/network/providers.dart';
import '../../core/theme/theme_controller.dart';
import '../../core/widgets/comink_logo.dart';
import '../auth/data/session_controller.dart';
import '../chats/data/chat_providers.dart';
import '../friends/data/friends_providers.dart';
import '../notifications/data/push_service.dart';
import '../notifications/presentation/notification_bell.dart';
import '../profile/data/profile_providers.dart';

/// Khung chính sau khi đăng nhập: thanh icon trên cùng (logo về Bảng tin + các chức năng + Cài đặt).
class HomeShell extends ConsumerWidget {
  const HomeShell({super.key, required this.child, required this.location});

  final Widget child;
  final String location;

  bool _isActive(String path) => location == path || location.startsWith('$path/');

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    final unreadMessages = ref.watch(conversationsProvider).maybeWhen(
          data: (items) => items.fold<int>(0, (sum, conv) => sum + conv.unreadCount),
          orElse: () => 0,
        );
    final pendingRequests = ref.watch(incomingFriendRequestsProvider).maybeWhen(
          data: (items) => items.length,
          orElse: () => 0,
        );

    return Scaffold(
      body: Column(
        children: [
          Material(
            color: colors.surface,
            child: SafeArea(
              bottom: false,
              child: Container(
                height: 56,
                padding: const EdgeInsets.symmetric(horizontal: 10),
                decoration: BoxDecoration(
                  border: Border(
                    bottom: BorderSide(color: colors.outlineVariant),
                  ),
                ),
                child: Row(
                  children: [
                    // Nhấn vào biểu tượng Comink để quay thẳng về Bảng tin.
                    InkWell(
                      borderRadius: BorderRadius.circular(10),
                      onTap: () => context.go('/feed'),
                      child: Padding(
                        padding: const EdgeInsets.all(4),
                        child: Tooltip(
                          message: l10n.t('nav_feed'),
                          child: const CominkLogo(size: 34),
                        ),
                      ),
                    ),
                    const Spacer(),
                    _TopNavIcon(
                      icon: Icons.chat_bubble_outline,
                      selectedIcon: Icons.chat_bubble,
                      selected: _isActive('/chats'),
                      tooltip: l10n.t('nav_chats'),
                      badgeCount: unreadMessages,
                      onPressed: () => context.go('/chats'),
                    ),
                    _TopNavIcon(
                      icon: Icons.people_outline,
                      selectedIcon: Icons.people,
                      selected: _isActive('/friends'),
                      tooltip: l10n.t('nav_friends'),
                      badgeCount: pendingRequests,
                      onPressed: () => context.go('/friends'),
                    ),
                    const NotificationBell(),
                    _TopNavIcon(
                      icon: Icons.person_outline,
                      selectedIcon: Icons.person,
                      selected: _isActive('/profile'),
                      tooltip: l10n.t('nav_profile'),
                      onPressed: () => context.go('/profile'),
                    ),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: Container(
                        width: 1,
                        height: 22,
                        color: colors.outlineVariant,
                      ),
                    ),
                    IconButton(
                      tooltip: l10n.t('profile_settings'),
                      onPressed: () => showModalBottomSheet<void>(
                        context: context,
                        useSafeArea: true,
                        builder: (_) => const _SettingsSheet(),
                      ),
                      icon: Icon(
                        Icons.settings_outlined,
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          Expanded(child: child),
        ],
      ),
    );
  }
}

class _TopNavIcon extends StatelessWidget {
  const _TopNavIcon({
    required this.icon,
    required this.selectedIcon,
    required this.selected,
    required this.tooltip,
    required this.onPressed,
    this.badgeCount = 0,
  });

  final IconData icon;
  final IconData selectedIcon;
  final bool selected;
  final String tooltip;
  final VoidCallback onPressed;
  final int badgeCount;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final iconWidget = Icon(
      selected ? selectedIcon : icon,
      color: selected ? colors.primary : colors.onSurfaceVariant,
    );

    return IconButton(
      tooltip: tooltip,
      onPressed: onPressed,
      style: IconButton.styleFrom(
        backgroundColor: selected ? colors.primaryContainer : Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      icon: badgeCount > 0
          ? Badge(
              label: Text(badgeCount > 99 ? '99+' : '$badgeCount'),
              child: iconWidget,
            )
          : iconWidget,
    );
  }
}

/// Bảng cài đặt nhanh từ thanh trên cùng: giao diện sáng/tối, ngôn ngữ, hồ sơ và đăng xuất.
class _SettingsSheet extends ConsumerWidget {
  const _SettingsSheet();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final localeCode = ref.watch(localeControllerProvider).languageCode;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                margin: const EdgeInsets.only(bottom: 12),
                decoration: BoxDecoration(
                  color: colors.outlineVariant,
                  borderRadius: BorderRadius.circular(99),
                ),
              ),
            ),
            Text(
              l10n.t('profile_settings'),
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
            ),
            const SizedBox(height: 8),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              secondary: Icon(
                isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined,
                color: colors.primary,
              ),
              title: Text(l10n.t('profile_theme')),
              subtitle: Text(
                isDark ? l10n.t('profile_theme_dark') : l10n.t('profile_theme_light'),
              ),
              value: isDark,
              onChanged: (value) => ref
                  .read(themeControllerProvider.notifier)
                  .setMode(value ? ThemeMode.dark : ThemeMode.light),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.language, color: colors.primary),
              title: Text(l10n.t('profile_language')),
              trailing: SegmentedButton<String>(
                showSelectedIcon: false,
                segments: const [
                  ButtonSegment(value: 'vi', label: Text('VI')),
                  ButtonSegment(value: 'en', label: Text('EN')),
                ],
                selected: {localeCode == 'en' ? 'en' : 'vi'},
                onSelectionChanged: (selection) async {
                  final nextCode = selection.first;
                  if (nextCode != localeCode) {
                    await _changeLanguage(context, ref, nextCode);
                  }
                },
              ),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.manage_accounts_outlined, color: colors.primary),
              title: Text(l10n.t('settings_account')),
              trailing: const Icon(Icons.chevron_right),
              onTap: () {
                Navigator.of(context).pop();
                context.go('/profile');
              },
            ),
            const Divider(),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.logout, color: colors.error),
              title: Text(
                l10n.t('profile_logout'),
                style: TextStyle(color: colors.error, fontWeight: FontWeight.w600),
              ),
              onTap: () => _confirmLogout(context, ref),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _changeLanguage(BuildContext context, WidgetRef ref, String code) async {
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(apiClientProvider).patch(
        '/api/users/me/settings',
        data: {'language': code},
      );
      await ref.read(localeControllerProvider.notifier).setLanguage(code);
      ref.invalidate(myProfileProvider);
    } on ApiException catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
    }
  }

  Future<void> _confirmLogout(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.t('profile_logout')),
        content: Text(l10n.t('auth_logout_confirm')),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: Text(l10n.t('common_cancel')),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(l10n.t('profile_logout')),
          ),
        ],
      ),
    );

    if (confirmed == true && context.mounted) {
      Navigator.of(context).pop();
      await ref.read(pushServiceProvider).unregister();
      await ref.read(sessionControllerProvider.notifier).logout();
    }
  }
}
