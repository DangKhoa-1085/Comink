import 'package:comink_mobile/core/network/api_exception.dart';

/// Kết quả phân trang con trỏ của backend.
class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.nextCursor,
    required this.hasMore,
  });

  final List<T> items;
  final String? nextCursor;
  final bool hasMore;

  factory PagedResult.fromJson(
    dynamic data,
    T Function(Map<String, dynamic> json) parse,
  ) {
    if (data is! Map) {
      throw const ApiException(code: 'INTERNAL_ERROR');
    }

    final json = data.cast<String, dynamic>();
    final rawItems = json['items'] as List<dynamic>? ?? const [];
    return PagedResult(
      items: rawItems
          .whereType<Map>()
          .map((item) => parse(item.cast<String, dynamic>()))
          .toList(growable: false),
      nextCursor: json['nextCursor']?.toString(),
      hasMore: json['hasMore'] == true,
    );
  }
}
