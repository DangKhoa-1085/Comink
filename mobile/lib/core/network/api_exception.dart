import 'package:dio/dio.dart';

/// Lỗi API đã chuẩn hoá, tương ứng ProblemDetails của backend.
///
/// Client **chỉ dựa vào `code`** để dịch thông báo (AGENTS.md mục 3), không dựa vào câu chữ.
class ApiException implements Exception {
  const ApiException({
    required this.code,
    this.statusCode,
    this.fieldErrors = const {},
  });

  final String code;
  final int? statusCode;

  /// Lỗi validate theo trường (key là camelCase như backend trả về).
  final Map<String, List<String>> fieldErrors;

  factory ApiException.fromDio(DioException error) {
    final response = error.response;
    final data = response?.data;

    var code = 'INTERNAL_ERROR';
    var fields = <String, List<String>>{};

    if (data is Map) {
      final rawCode = data['code'];
      if (rawCode is String && rawCode.isNotEmpty) {
        code = rawCode;
      }

      final rawErrors = data['errors'];
      if (rawErrors is Map) {
        fields = rawErrors.map(
          (key, value) => MapEntry(
            key.toString(),
            value is List
                ? value.map((item) => item.toString()).toList()
                : [value.toString()],
          ),
        );
      }
    }

    if (code == 'INTERNAL_ERROR' && _isNetworkError(error)) {
      code = 'NETWORK_ERROR';
    }

    return ApiException(
      code: code,
      statusCode: response?.statusCode,
      fieldErrors: fields,
    );
  }

  static bool _isNetworkError(DioException error) {
    return error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout ||
        error.type == DioExceptionType.sendTimeout;
  }

  @override
  String toString() => 'ApiException($statusCode, $code)';
}
