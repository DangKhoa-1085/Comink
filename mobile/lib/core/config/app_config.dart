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
}
