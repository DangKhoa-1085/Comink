import 'dart:async';

import 'package:dio/dio.dart';

import '../../features/auth/domain/auth_models.dart';
import '../config/app_config.dart';
import '../storage/token_storage.dart';
import 'api_exception.dart';

/// Client HTTP dùng chung cho app.
///
/// - Gắn `Authorization: Bearer <access_token>` vào mọi request.
/// - Khi gặp 401 (trừ endpoint auth), tự gọi `/api/auth/refresh` **một lần** rồi thử lại.
/// - Refresh thất bại → xoá phiên ở secure storage và phát `sessionExpired`.
class ApiClient {
  ApiClient(this._tokenStorage, {String? baseUrl})
      : _baseUrl = baseUrl ?? AppConfig.apiBaseUrl {
    final options = BaseOptions(
      baseUrl: _baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30),
      headers: const {'Content-Type': 'application/json'},
    );

    _dio = Dio(options);
    _refreshDio = Dio(options);

    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (request, handler) {
          final token = _accessToken;
          if (token != null && token.isNotEmpty) {
            request.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(request);
        },
        onError: (error, handler) async {
          final request = error.requestOptions;
          final isAuthEndpoint = request.path.contains('/api/auth/');
          final alreadyRetried = request.extra['comink_retried'] == true;

          if (error.response?.statusCode == 401 && !isAuthEndpoint && !alreadyRetried) {
            final token = await _refreshAccessToken();
            if (token != null) {
              request.extra['comink_retried'] = true;
              request.headers['Authorization'] = 'Bearer $token';
              try {
                final response = await _dio.fetch<dynamic>(request);
                handler.resolve(response);
                return;
              } on DioException {
                // Để lỗi gốc được xử lý bên dưới.
              }
            } else {
              _sessionExpired.add(null);
            }
          }

          handler.next(error);
        },
      ),
    );
  }

  final TokenStorage _tokenStorage;
  final String _baseUrl;
  late final Dio _dio;
  late final Dio _refreshDio;
  final StreamController<void> _sessionExpired = StreamController<void>.broadcast();

  String? _accessToken;
  Future<String?>? _refreshing;

  /// Access token hiện tại (chỉ trong bộ nhớ) — dùng cho SignalR.
  String? get accessToken => _accessToken;

  /// Phát sự kiện khi phiên không thể khôi phục (refresh token hết hiệu lực/bị thu hồi).
  Stream<void> get sessionExpired => _sessionExpired.stream;

  void setAccessToken(String? token) => _accessToken = token;

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) =>
      _send(() => _dio.get<dynamic>(path, queryParameters: query));

  Future<dynamic> post(String path, {Object? data, Map<String, dynamic>? query}) =>
      _send(() => _dio.post<dynamic>(path, data: data, queryParameters: query));

  Future<dynamic> patch(String path, {Object? data}) =>
      _send(() => _dio.patch<dynamic>(path, data: data));

  Future<dynamic> delete(String path, {Object? data}) =>
      _send(() => _dio.delete<dynamic>(path, data: data));

  Future<dynamic> _send(Future<Response<dynamic>> Function() action) async {
    try {
      final response = await action();
      return response.data;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String?> _refreshAccessToken() {
    return _refreshing ??= _performRefresh().whenComplete(() => _refreshing = null);
  }

  Future<String?> _performRefresh() async {
    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken == null || refreshToken.isEmpty) {
      return null;
    }

    try {
      final response = await _refreshDio.post<dynamic>(
        '/api/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      final data = response.data;
      if (data is! Map) {
        await _tokenStorage.clear();
        return null;
      }

      final auth = AuthResponse.fromJson(data.cast<String, dynamic>());
      _accessToken = auth.accessToken;
      await _tokenStorage.saveSession(auth);
      return auth.accessToken;
    } on DioException {
      await _tokenStorage.clear();
      return null;
    }
  }

  void close() {
    _dio.close(force: true);
    _refreshDio.close(force: true);
    _sessionExpired.close();
  }
}
