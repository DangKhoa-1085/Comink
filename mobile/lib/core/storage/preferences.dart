import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// `SharedPreferences` được khởi tạo một lần trong `main()` và override vào ProviderScope.
/// Dùng cho các cài đặt đơn giản (ngôn ngữ, theme); token nằm trong secure storage.
final sharedPreferencesProvider = Provider<SharedPreferences>(
  (ref) => throw UnimplementedError(
    'sharedPreferencesProvider phải được override trong ProviderScope (xem main.dart).',
  ),
);
