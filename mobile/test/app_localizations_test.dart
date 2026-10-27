import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/i18n/app_localizations.dart';

void main() {
  test('dịch các chuỗi chính sang tiếng Việt và tiếng Anh', () {
    const vi = AppLocalizations(Locale('vi'));
    const en = AppLocalizations(Locale('en'));

    expect(vi.t('auth_login'), 'Đăng nhập');
    expect(en.t('auth_login'), 'Sign in');
    expect(vi.t('nav_feed'), 'Bảng tin');
    expect(en.t('nav_feed'), 'Feed');
    expect(vi.t('common_loading'), 'Đang tải...');
    expect(en.t('common_loading'), 'Loading...');
  });

  test('dịch mã lỗi backend; mã lạ rơi về lỗi chung', () {
    const vi = AppLocalizations(Locale('vi'));
    const en = AppLocalizations(Locale('en'));

    expect(vi.error('AUTH_ACCOUNT_LOCKED'), contains('bị khóa'));
    expect(en.error('AUTH_ACCOUNT_LOCKED'), contains('locked'));
    expect(vi.error('KHONG_TON_TAI'), vi.t('error_INTERNAL_ERROR'));
    expect(en.error(null), en.t('error_INTERNAL_ERROR'));
  });
}
