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

  /// Ghép URL tương đối (ví dụ `/uploads/...` khi dev lưu đĩa cục bộ) với [apiBaseUrl].
  /// URL tuyệt đối (`http://`, `https://`, ví dụ Cloudinary) được giữ nguyên.
  static String resolveMediaUrl(String url, {String? baseUrl}) {
    final trimmed = url.trim();
    if (trimmed.isEmpty) {
      return trimmed;
    }
    final uri = Uri.tryParse(trimmed);
    if (uri != null && uri.hasScheme) {
      return trimmed;
    }
    final origin = (baseUrl ?? apiBaseUrl).trim().replaceAll(RegExp(r'/+$'), '');
    final path = trimmed.startsWith('/') ? trimmed : '/$trimmed';
    return '$origin$path';
  }

  /// Chuẩn hoá URL WebSocket LiveKit theo [apiBaseUrl] khi backend trả `localhost`/`127.0.0.1`.
  /// - Nếu app đang nối backend qua LAN (`192.168.x.x`) hoặc emulator (`10.0.2.2`), đổi host
  ///   LiveKit sang cùng host đó để gọi được mà không bắt buộc cấu hình `LiveKit__PublicUrl`.
  /// - Nếu app nối qua cáp USB (`127.0.0.1`), đổi `localhost` thành `127.0.0.1` để Android luôn
  ///   đi đúng IPv4 khớp với `adb reverse tcp:7880 tcp:7880` (tránh rơi sang IPv6 `::1`).
  /// - Nếu backend đã trả host thật (LAN/cloud), giữ nguyên.
  static String resolveLiveKitUrl(String url, {String? baseUrl}) {
    final trimmed = url.trim();
    final liveKitUri = Uri.tryParse(trimmed);
    if (liveKitUri == null || liveKitUri.host.isEmpty) {
      return trimmed;
    }

    final isLoopback = liveKitUri.host == 'localhost' ||
        liveKitUri.host == '127.0.0.1' ||
        liveKitUri.host == '::1';
    if (!isLoopback) {
      return trimmed;
    }

    final apiUri = Uri.tryParse((baseUrl ?? apiBaseUrl).trim());
    final apiHost = apiUri?.host ?? '';
    if (apiHost.isEmpty) {
      return trimmed;
    }

    final targetHost = apiHost == 'localhost' ? '127.0.0.1' : apiHost;
    return liveKitUri.replace(host: targetHost).toString();
  }
}
