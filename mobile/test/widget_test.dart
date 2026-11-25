import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:comink_mobile/app.dart';
import 'package:comink_mobile/core/network/providers.dart';
import 'package:comink_mobile/core/storage/preferences.dart';
import 'package:comink_mobile/core/storage/token_storage.dart';
import 'package:comink_mobile/features/auth/domain/auth_models.dart';
import 'package:comink_mobile/features/auth/presentation/login_page.dart';

/// Token storage giả để widget test không gọi plugin secure storage.
class _FakeTokenStorage extends TokenStorage {
  @override
  Future<String?> readRefreshToken() async => null;

  @override
  Future<AuthUser?> readUser() async => null;

  @override
  Future<void> saveSession(AuthResponse auth) async {}

  @override
  Future<void> saveUser(AuthUser user) async {}

  @override
  Future<void> clear() async {}
}

void main() {
  testWidgets('app khởi động chưa đăng nhập → chuyển tới trang đăng nhập', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sharedPreferencesProvider.overrideWithValue(preferences),
          tokenStorageProvider.overrideWithValue(_FakeTokenStorage()),
        ],
        child: const CominkApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.text('Đăng nhập'), findsWidgets);

    // Bấm đăng nhập khi để trống → form báo lỗi bắt buộc.
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
    await tester.pump();

    expect(find.text('Vui lòng nhập thông tin'), findsNWidgets(2));
  });
}
