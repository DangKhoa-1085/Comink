import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../features/auth/domain/auth_models.dart';

/// Lưu phiên đăng nhập.
///
/// - **Refresh token** và thông tin người dùng nằm trong secure storage
///   (`flutter_secure_storage` — Android dùng Keystore/EncryptedSharedPreferences).
/// - **Access token** chỉ giữ trong bộ nhớ (`ApiClient`), không ghi xuống đĩa.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? secureStorage})
      : _secureStorage = secureStorage ?? const FlutterSecureStorage();

  static const String _refreshKey = 'comink.refreshToken';
  static const String _userKey = 'comink.user';

  final FlutterSecureStorage _secureStorage;

  Future<String?> readRefreshToken() => _secureStorage.read(key: _refreshKey);

  Future<AuthUser?> readUser() async {
    final raw = await _secureStorage.read(key: _userKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }

    try {
      final json = jsonDecode(raw);
      return json is Map<String, dynamic> ? AuthUser.fromJson(json) : null;
    } on FormatException {
      return null;
    }
  }

  Future<void> saveSession(AuthResponse auth) async {
    await _secureStorage.write(key: _refreshKey, value: auth.refreshToken);
    await saveUser(auth.user);
  }

  Future<void> saveUser(AuthUser user) async {
    await _secureStorage.write(key: _userKey, value: jsonEncode(user.toJson()));
  }

  Future<void> clear() async {
    await _secureStorage.delete(key: _refreshKey);
    await _secureStorage.delete(key: _userKey);
  }
}
