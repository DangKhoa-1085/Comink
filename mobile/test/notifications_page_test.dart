import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/i18n/app_localizations.dart';
import 'package:comink_mobile/features/notifications/data/notification_providers.dart';
import 'package:comink_mobile/features/notifications/domain/app_notification.dart';
import 'package:comink_mobile/features/notifications/presentation/notifications_page.dart';

Widget _app(AppNotification? notification) => ProviderScope(
      overrides: [
        notificationsProvider.overrideWith(
          (ref) async => notification == null ? [] : [notification],
        ),
      ],
      child: MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: const NotificationsPage(),
      ),
    );

void main() {
  testWidgets('Hộp thư hiện thông báo lời mời kết bạn', (tester) async {
    await tester.pumpWidget(
      _app(
        AppNotification(
          id: 'n1',
          type: 'FriendRequestReceived',
          isRead: false,
          createdAt: DateTime(2026, 10, 27),
          data: const {'type': 'FriendRequest'},
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Thông báo'), findsOneWidget);
    expect(find.text('Lời mời kết bạn'), findsOneWidget);
  });

  testWidgets('Hộp thư rỗng hiện empty state', (tester) async {
    await tester.pumpWidget(_app(null));
    await tester.pumpAndSettle();

    expect(find.text('Chưa có thông báo'), findsOneWidget);
  });
}
