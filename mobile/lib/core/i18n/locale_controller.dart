import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/preferences.dart';

/// Ngôn ngữ đang dùng; lưu vào SharedPreferences.
final localeControllerProvider =
    NotifierProvider<LocaleController, Locale>(LocaleController.new);

class LocaleController extends Notifier<Locale> {
  static const String _key = 'comink.language';

  @override
  Locale build() {
    final code = ref.watch(sharedPreferencesProvider).getString(_key);
    return Locale(code == 'en' ? 'en' : 'vi');
  }

  Future<void> setLanguage(String code) async {
    if (code != 'vi' && code != 'en') {
      return;
    }
    await ref.read(sharedPreferencesProvider).setString(_key, code);
    state = Locale(code);
  }
}
