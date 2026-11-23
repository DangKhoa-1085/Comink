import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/i18n/app_localizations.dart';
import '../features/auth/data/session_controller.dart';
import '../features/auth/presentation/login_page.dart';
import '../features/auth/presentation/register_page.dart';
import '../features/chats/presentation/chat_page.dart';
import '../features/chats/presentation/chats_page.dart';
import '../features/feed/presentation/feed_page.dart';
import '../features/friends/presentation/friends_page.dart';
import '../features/notifications/presentation/notifications_page.dart';
import '../features/profile/presentation/profile_page.dart';
import '../features/profile/presentation/public_user_page.dart';
import '../features/shell/home_shell.dart';
import '../features/shell/splash_page.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final refreshNotifier = ValueNotifier<int>(0);
  ref.listen<SessionState>(sessionControllerProvider, (_, _) => refreshNotifier.value++);
  ref.onDispose(refreshNotifier.dispose);

  return GoRouter(
    initialLocation: '/feed',
    refreshListenable: refreshNotifier,
    redirect: (context, state) {
      final session = ref.read(sessionControllerProvider);
      final path = state.matchedLocation;
      final isAuthPage = path == '/login' || path == '/register';

      if (session.isUnknown) {
        return path == '/splash' ? null : '/splash';
      }

      if (!session.isAuthenticated) {
        return isAuthPage ? null : '/login';
      }

      if (path == '/splash' || isAuthPage) {
        return '/feed';
      }

      return null;
    },
    routes: [
      GoRoute(
        path: '/splash',
        builder: (context, state) => const SplashPage(),
      ),
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginPage(),
      ),
      GoRoute(
        path: '/register',
        builder: (context, state) => const RegisterPage(),
      ),
      ShellRoute(
        builder: (context, state, child) => HomeShell(
          location: state.matchedLocation,
          child: child,
        ),
        routes: [
          GoRoute(
            path: '/feed',
            builder: (context, state) => const FeedPage(),
          ),
          GoRoute(
            path: '/friends',
            builder: (context, state) => const FriendsPage(),
          ),
          GoRoute(
            path: '/chats',
            builder: (context, state) => const ChatsPage(),
          ),
          GoRoute(
            path: '/calls',
            // Lịch sử cuộc gọi đã được ẩn; link cũ quay về Tin nhắn.
            redirect: (context, state) => '/chats',
          ),
          GoRoute(
            path: '/profile',
            builder: (context, state) => const ProfilePage(),
          ),
        ],
      ),
      GoRoute(
        path: '/chats/:id',
        builder: (context, state) => ChatPage(
          conversationId: state.pathParameters['id'] ?? '',
          focusMessageId: state.uri.queryParameters['focus'],
          focusTerm: state.uri.queryParameters['q'],
        ),
      ),
      GoRoute(
        path: '/users/:id',
        builder: (context, state) => PublicUserPage(
          userId: state.pathParameters['id'] ?? '',
        ),
      ),
      GoRoute(
        path: '/notifications',
        builder: (context, state) => const NotificationsPage(),
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: Center(
        child: Text(AppLocalizations.of(context).t('error_NOT_FOUND')),
      ),
    ),
  );
});
