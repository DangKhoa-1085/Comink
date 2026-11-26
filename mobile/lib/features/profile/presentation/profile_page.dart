import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/i18n/locale_controller.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/theme/theme_controller.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../../core/widgets/states.dart';
import '../../auth/data/session_controller.dart';
import '../../notifications/data/push_service.dart';
import '../data/profile_providers.dart';
import '../domain/my_profile.dart';

/// Tab Hồ sơ: ảnh bìa xanh nước biển, thống kê, sửa hồ sơ, đổi ngôn ngữ/giao diện, đăng xuất.
class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final profile = ref.watch(myProfileProvider);

    return Scaffold(
      body: profile.when(
        data: (data) => _ProfileContent(profile: data),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: l10n.error(error is ApiException ? error.code : 'INTERNAL_ERROR'),
          retryLabel: l10n.t('common_retry'),
          onRetry: () => ref.invalidate(myProfileProvider),
        ),
      ),
    );
  }
}

class _ProfileContent extends ConsumerWidget {
  const _ProfileContent({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    final localeCode = ref.watch(localeControllerProvider).languageCode;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final joinedDate = MaterialLocalizations.of(context).formatShortDate(profile.createdAt);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(myProfileProvider);
        await ref.read(myProfileProvider.future);
      },
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          Card(
            clipBehavior: Clip.antiAlias,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Container(
                      height: 112,
                      decoration: const BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            Color(0xFF0284C7),
                            Color(0xFF0EA5E9),
                            Color(0xFF06B6D4),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                      ),
                    ),
                    Positioned(
                      right: 12,
                      bottom: 10,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.black.withValues(alpha: 0.25),
                          borderRadius: BorderRadius.circular(99),
                        ),
                        child: Text(
                          '${l10n.t('profile_joined')}: $joinedDate',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 11,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ),
                    Positioned(
                      left: 16,
                      bottom: -36,
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          color: colors.surface,
                          shape: BoxShape.circle,
                        ),
                        child: AppAvatar(
                          name: profile.displayName,
                          url: profile.avatarUrl,
                          radius: 38,
                        ),
                      ),
                    ),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 44, 16, 16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  profile.displayName,
                                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                        fontWeight: FontWeight.w800,
                                      ),
                                ),
                                Text(
                                  '@${profile.userName}',
                                  style: TextStyle(color: colors.onSurfaceVariant),
                                ),
                              ],
                            ),
                          ),
                          FilledButton.tonalIcon(
                            onPressed: () => showDialog<void>(
                              context: context,
                              builder: (_) => _EditProfileDialog(profile: profile),
                            ),
                            icon: const Icon(Icons.edit_outlined, size: 17),
                            label: Text(l10n.t('profile_edit')),
                          ),
                        ],
                      ),
                      if (profile.bio != null && profile.bio!.trim().isNotEmpty) ...[
                        const SizedBox(height: 10),
                        Text(profile.bio!, style: const TextStyle(height: 1.35)),
                      ],
                      if (profile.location != null && profile.location!.trim().isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Icon(Icons.place_outlined, size: 16, color: colors.onSurfaceVariant),
                            const SizedBox(width: 4),
                            Text(
                              profile.location!,
                              style: TextStyle(color: colors.onSurfaceVariant, fontSize: 13),
                            ),
                          ],
                        ),
                      ],
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          Expanded(
                            child: _Stat(
                              label: l10n.t('profile_friends'),
                              value: profile.friendCount,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _Stat(
                              label: l10n.t('profile_followers'),
                              value: profile.followerCount,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _Stat(
                              label: l10n.t('profile_following'),
                              value: profile.followingCount,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _Section(
            title: l10n.t('profile_account_info'),
            children: [
              _InfoRow(icon: Icons.mail_outline, label: l10n.t('profile_email'), value: profile.email),
              _InfoRow(
                icon: Icons.verified_user_outlined,
                label: l10n.t('profile_roles'),
                value: profile.roles.join(', '),
              ),
              _InfoRow(
                icon: Icons.calendar_month_outlined,
                label: l10n.t('profile_joined'),
                value: joinedDate,
              ),
            ],
          ),
          const SizedBox(height: 12),
          _Section(
            title: l10n.t('profile_settings'),
            children: [
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.language),
                title: Text(l10n.t('profile_language')),
                trailing: DropdownButton<String>(
                  value: localeCode,
                  underline: const SizedBox.shrink(),
                  items: [
                    DropdownMenuItem(value: 'vi', child: Text(l10n.t('language_vi'))),
                    DropdownMenuItem(value: 'en', child: Text(l10n.t('language_en'))),
                  ],
                  onChanged: (value) async {
                    if (value == null || value == localeCode) {
                      return;
                    }
                    await _changeLanguage(context, ref, value);
                  },
                ),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                secondary: Icon(isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined),
                title: Text(l10n.t('profile_theme')),
                subtitle: Text(isDark ? l10n.t('profile_theme_dark') : l10n.t('profile_theme_light')),
                value: isDark,
                onChanged: (value) => ref
                    .read(themeControllerProvider.notifier)
                    .setMode(value ? ThemeMode.dark : ThemeMode.light),
              ),
            ],
          ),
          const SizedBox(height: 18),
          OutlinedButton.icon(
            onPressed: () => _confirmLogout(context, ref),
            icon: const Icon(Icons.logout),
            label: Text(l10n.t('profile_logout')),
            style: OutlinedButton.styleFrom(
              foregroundColor: colors.error,
              minimumSize: const Size.fromHeight(46),
            ),
          ),
          const SizedBox(height: 24),
        ],
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

    if (confirmed == true) {
      await ref.read(pushServiceProvider).unregister();
      await ref.read(sessionControllerProvider.notifier).logout();
    }
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});

  final String label;
  final int value;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 6),
      decoration: BoxDecoration(
        color: colors.surfaceContainerLow,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: colors.outlineVariant),
      ),
      child: Column(
        children: [
          Text(
            '$value',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  color: colors.primary,
                ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 11.5, color: colors.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 4),
            ...children,
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      leading: Icon(icon, size: 20, color: colors.primary),
      title: Text(label, style: TextStyle(fontSize: 12, color: colors.onSurfaceVariant)),
      subtitle: Text(value, style: TextStyle(fontSize: 14, color: colors.onSurface)),
    );
  }
}

class _EditProfileDialog extends ConsumerStatefulWidget {
  const _EditProfileDialog({required this.profile});

  final MyProfile profile;

  @override
  ConsumerState<_EditProfileDialog> createState() => _EditProfileDialogState();
}

class _EditProfileDialogState extends ConsumerState<_EditProfileDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _displayNameController;
  late final TextEditingController _bioController;
  late final TextEditingController _locationController;
  bool _saving = false;
  String? _errorCode;

  @override
  void initState() {
    super.initState();
    _displayNameController = TextEditingController(text: widget.profile.displayName);
    _bioController = TextEditingController(text: widget.profile.bio ?? '');
    _locationController = TextEditingController(text: widget.profile.location ?? '');
  }

  @override
  void dispose() {
    _displayNameController.dispose();
    _bioController.dispose();
    _locationController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_saving || !(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _saving = true;
      _errorCode = null;
    });

    try {
      final data = await ref.read(apiClientProvider).patch(
        '/api/users/me/profile',
        data: {
          'displayName': _displayNameController.text.trim(),
          'bio': _bioController.text.trim(),
          'location': _locationController.text.trim(),
        },
      );

      if (data is! Map) {
        throw const ApiException(code: 'INTERNAL_ERROR');
      }

      final updated = MyProfile.fromJson(data.cast<String, dynamic>());
      await ref.read(sessionControllerProvider.notifier).updateUser(updated.toAuthUser());
      ref.invalidate(myProfileProvider);

      if (!mounted) {
        return;
      }
      Navigator.pop(context);
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('profile_saved'))));
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _saving = false;
        _errorCode = error.code;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    return AlertDialog(
      title: Text(l10n.t('profile_edit')),
      content: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: _displayNameController,
                maxLength: 64,
                decoration: InputDecoration(labelText: l10n.t('profile_display_name')),
                validator: (value) {
                  final text = value?.trim() ?? '';
                  return (text.isEmpty || text.length > 64)
                      ? l10n.t('validation_display_name')
                      : null;
                },
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _bioController,
                maxLength: 300,
                maxLines: 3,
                decoration: InputDecoration(labelText: l10n.t('profile_bio')),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _locationController,
                maxLength: 100,
                decoration: InputDecoration(labelText: l10n.t('profile_location')),
              ),
              if (_errorCode != null) ...[
                const SizedBox(height: 8),
                Text(
                  l10n.error(_errorCode),
                  style: TextStyle(color: colors.error),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.pop(context),
          child: Text(l10n.t('common_cancel')),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: _saving
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : Text(l10n.t('common_save')),
        ),
      ],
    );
  }
}
