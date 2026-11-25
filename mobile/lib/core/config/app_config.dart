/// Cấu hình chung của app. Base URL lấy từ `--dart-define=API_BASE_URL=...`.
///
/// - Android emulator: mặc định `http://10.0.2.2:5117` (10.0.2.2 là localhost của máy host).
/// - Thiết bị thật: truyền `--dart-define=API_BASE_URL=http://<ip-lan>:5117`.
class AppConfig {
  const AppConfig._();

  static const String appName = 'Comink';

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5117',
  );

  /// Hub SignalR (GĐ 6 — chat real-time). Token truyền qua query `access_token`.
  static String get hubUrl => '$apiBaseUrl/hub/chat';

  // --- Firebase / FCM (GĐ 6) ---
  // Không commit giá trị thật; truyền qua --dart-define khi build/run.
  static const String firebaseProjectId = String.fromEnvironment('FIREBASE_PROJECT_ID');
  static const String firebaseAppId = String.fromEnvironment('FIREBASE_APP_ID');
  static const String firebaseApiKey = String.fromEnvironment('FIREBASE_API_KEY');
  static const String firebaseMessagingSenderId =
      String.fromEnvironment('FIREBASE_MESSAGING_SENDER_ID');
  static const String firebaseStorageBucket =
      String.fromEnvironment('FIREBASE_STORAGE_BUCKET');

  static bool get firebaseConfigured =>
      firebaseProjectId.isNotEmpty &&
      firebaseAppId.isNotEmpty &&
      firebaseApiKey.isNotEmpty &&
      firebaseMessagingSenderId.isNotEmpty;
}
