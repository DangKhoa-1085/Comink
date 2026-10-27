import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/storage/preferences.dart';

Future<void> main() async {
  final binding = WidgetsFlutterBinding.ensureInitialized();

  // Bật accessibility tree khi chạy web ở debug: giúp screen reader và công cụ
  // kiểm thử đọc được widget. Bản release không bật để tránh chi phí DOM.
  if (kDebugMode && kIsWeb) {
    binding.ensureSemantics();
  }

  final preferences = await SharedPreferences.getInstance();

  runApp(
    ProviderScope(
      overrides: [sharedPreferencesProvider.overrideWithValue(preferences)],
      child: const CominkApp(),
    ),
  );
}
