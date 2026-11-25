import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/storage/token_storage.dart';
import '../domain/auth_models.dart';

enum SessionStatus { unknown, authenticated, unauthenticated }

class SessionState {
  const SessionState({
    required this.status,
    this.user,
    this.loading = false,
    this.errorCode,
    this.fieldErrors = const {},
  });

  const SessionState.unknown() : this(status: SessionStatus.unknown);
  const SessionState.unauthenticated()
      : this(status: SessionStatus.unauthenticated);
  const SessionState.authenticated(AuthUser user)
      : this(status: SessionStatus.authenticated, user: user);

  final SessionStatus status;
  final AuthUser? user;
  final bool loading;
  final String? errorCode;
  final Map<String, List<String>> fieldErrors;

  bool get isAuthenticated => status == SessionStatus.authenticated;
  bool get isUnknown => status == SessionStatus.unknown;
}

final sessionControllerProvider =
    NotifierProvider<SessionController, SessionState>(SessionController.new);

/// Quản lý phiên đăng nhập: khôi phục, đăng nhập/đăng ký, đăng xuất, cập nhật hồ sơ.
class SessionController extends Notifier<SessionState> {
  late final TokenStorage _storage;
  StreamSubscription<void>? _expiredSubscription;

  @override
  SessionState build() {
    _storage = ref.watch(tokenStorageProvider);

    final api = ref.watch(apiClientProvider);
    _expiredSubscription = api.sessionExpired.listen((_) {
      // Phiên không thể khôi phục (refresh token hết hiệu lực/bị thu hồi).
      _storage.clear();
      state = const SessionState.unauthenticated();
    });
    ref.onDispose(() => _expiredSubscription?.cancel());

    Future.microtask(_restoreSession);
    return const SessionState.unknown();
  }

  Future<void> _restoreSession() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken == null || refreshToken.isEmpty) {
      state = const SessionState.unauthenticated();
      return;
    }

    try {
      final data = await ref.read(apiClientProvider).get('/api/users/me');
      final user = _authUserFromProfile(data);
      await _storage.saveUser(user);
      state = SessionState.authenticated(user);
    } on ApiException {
      await _storage.clear();
      state = const SessionState.unauthenticated();
    }
  }

  Future<bool> login({
    required String emailOrUserName,
    required String password,
  }) async {
    return _submitAuth(
      () => ref.read(apiClientProvider).post(
            '/api/auth/login',
            data: {
              'emailOrUserName': emailOrUserName.trim(),
              'password': password,
              'deviceName': _deviceName(),
            },
          ),
    );
  }

  Future<bool> register({
    required String userName,
    required String email,
    required String password,
    required String displayName,
  }) async {
    return _submitAuth(
      () => ref.read(apiClientProvider).post(
            '/api/auth/register',
            data: {
              'userName': userName.trim(),
              'email': email.trim(),
              'password': password,
              'displayName': displayName.trim(),
            },
          ),
    );
  }

  Future<bool> _submitAuth(Future<dynamic> Function() request) async {
    state = SessionState(
      status: state.status,
      user: state.user,
      loading: true,
    );

    try {
      final data = await request();
      if (data is! Map) {
        throw const ApiException(code: 'INTERNAL_ERROR');
      }

      final auth = AuthResponse.fromJson(data.cast<String, dynamic>());
      ref.read(apiClientProvider).setAccessToken(auth.accessToken);
      await _storage.saveSession(auth);
      state = SessionState.authenticated(auth.user);
      return true;
    } on ApiException catch (error) {
      state = SessionState(
        status: state.status,
        user: state.user,
        errorCode: error.code,
        fieldErrors: error.fieldErrors,
      );
      return false;
    }
  }

  Future<void> logout() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        await ref.read(apiClientProvider).post(
          '/api/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      } on ApiException {
        // Đăng xuất ở client vẫn phải thành công dù server lỗi mạng.
      }
    }

    ref.read(apiClientProvider).setAccessToken(null);
    await _storage.clear();
    state = const SessionState.unauthenticated();
  }

  /// Cập nhật người dùng sau khi sửa hồ sơ.
  Future<void> updateUser(AuthUser user) async {
    await _storage.saveUser(user);
    state = SessionState.authenticated(user);
  }

  void clearError() {
    state = SessionState(
      status: state.status,
      user: state.user,
      loading: state.loading,
    );
  }

  static AuthUser _authUserFromProfile(dynamic data) {
    if (data is! Map) {
      throw const ApiException(code: 'INTERNAL_ERROR');
    }

    final json = data.cast<String, dynamic>();
    return AuthUser(
      id: (json['id'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      email: (json['email'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      roles: (json['roles'] as List<dynamic>? ?? const [])
          .map((role) => role.toString())
          .toList(growable: false),
    );
  }

  static String _deviceName() {
    final platform = switch (defaultTargetPlatform) {
      TargetPlatform.android => 'Android',
      TargetPlatform.iOS => 'iOS',
      _ => 'Mobile',
    };
    return 'Comink Mobile ($platform)';
  }
}
