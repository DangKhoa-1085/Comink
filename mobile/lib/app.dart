import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/config/app_config.dart';
import 'core/i18n/app_localizations.dart';
import 'core/i18n/locale_controller.dart';
import 'core/theme/app_theme.dart';
import 'core/theme/theme_controller.dart';
import 'core/ui/app_keys.dart';
import 'features/auth/data/session_controller.dart';
import 'features/calls/data/call_controller.dart';
import 'features/calls/presentation/call_overlay.dart';
import 'features/chats/data/chat_providers.dart';
import 'features/chats/data/chat_realtime.dart';
import 'features/feed/data/feed_providers.dart';
import 'features/friends/data/friends_providers.dart';
import 'features/notifications/data/notification_providers.dart';
import 'features/notifications/data/push_service.dart';
import 'features/profile/data/profile_providers.dart';
import 'router/app_router.dart';

/// Gốc ứng dụng: router, theme, i18n.
class CominkApp extends ConsumerWidget {
  const CominkApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final locale = ref.watch(localeControllerProvider);
    final themeMode = ref.watch(themeControllerProvider);

    // Kết nối SignalR khi có phiên, ngắt khi đăng xuất và xóa cache dữ liệu theo người dùng cũ.
    ref.listen<SessionState>(sessionControllerProvider, (previous, next) {
      if (previous?.user?.id != next.user?.id) {
        ref.invalidate(myProfileProvider);
        ref.invalidate(feedProvider);
        ref.invalidate(friendsProvider);
        ref.invalidate(incomingFriendRequestsProvider);
        ref.invalidate(conversationsProvider);
        ref.invalidate(notificationsProvider);
      }

      final realtime = ref.read(chatRealtimeProvider);
      if (next.isAuthenticated) {
        unawaited(realtime.connect());
        unawaited(ref.read(pushServiceProvider).start());
      } else {
        ref.read(callControllerProvider.notifier).clear();
        unawaited(ref.read(pushServiceProvider).stop());
        unawaited(realtime.disconnect());
      }
    });

    return MaterialApp.router(
      title: AppConfig.appName,
      debugShowCheckedModeBanner: false,
      scaffoldMessengerKey: scaffoldMessengerKey,
      theme: buildAppTheme(Brightness.light),
      darkTheme: buildAppTheme(Brightness.dark),
      themeMode: themeMode,
      locale: locale,
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      routerConfig: router,
      builder: (context, child) => CallOverlay(
        child: child ?? const SizedBox.shrink(),
      ),
    );
  }
}
