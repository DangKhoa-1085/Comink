import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/preferences.dart';

/// Chế độ giao diện (system/sáng/tối), lưu vào SharedPreferences.
final themeControllerProvider =
    NotifierProvider<ThemeController, ThemeMode>(ThemeController.new);

class ThemeController extends Notifier<ThemeMode> {
  static const String _key = 'comink.themeMode';

  @override
  ThemeMode build() {
    final value = ref.watch(sharedPreferencesProvider).getString(_key);
    return switch (value) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
  }

  Future<void> setMode(ThemeMode mode) async {
    await ref.read(sharedPreferencesProvider).setString(_key, mode.name);
    state = mode;
  }

  Future<void> toggle(Brightness currentBrightness) =>
      setMode(currentBrightness == Brightness.dark ? ThemeMode.light : ThemeMode.dark);
}
