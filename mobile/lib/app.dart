import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/config/app_config.dart';
import 'core/i18n/app_localizations.dart';
import 'core/i18n/locale_controller.dart';
import 'core/theme/app_theme.dart';
import 'core/theme/theme_controller.dart';
import 'features/auth/data/session_controller.dart';
import 'features/chats/data/chat_realtime.dart';
import 'router/app_router.dart';

/// Gốc ứng dụng: router, theme, i18n.
class CominkApp extends ConsumerWidget {
  const CominkApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    final locale = ref.watch(localeControllerProvider);
    final themeMode = ref.watch(themeControllerProvider);

    // Kết nối SignalR khi có phiên, ngắt khi đăng xuất.
    ref.listen<SessionState>(sessionControllerProvider, (_, next) {
      final realtime = ref.read(chatRealtimeProvider);
      if (next.isAuthenticated) {
        unawaited(realtime.connect());
      } else {
        unawaited(realtime.disconnect());
      }
    });

    return MaterialApp.router(
      title: AppConfig.appName,
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(Brightness.light),
      darkTheme: buildAppTheme(Brightness.dark),
      themeMode: themeMode,
      locale: locale,
      supportedLocales: AppLocalizations.supportedLocales,
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      routerConfig: router,
    );
  }
}
