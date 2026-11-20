/// Tách văn bản thành các đoạn khớp/không khớp từ khoá tìm kiếm để tô sáng.
///
/// Tìm kiếm ở backend bỏ dấu tiếng Việt (`hop` khớp `họp`), nên phần tô sáng cũng phải
/// so khớp **không dấu, không phân biệt hoa/thường** nhưng vẫn giữ nguyên văn bản gốc.
library;

class HighlightSegment {
  const HighlightSegment(this.text, {this.match = false});

  final String text;
  final bool match;
}

/// Bảng bỏ dấu tiếng Việt (đã hạ chữ thường trước khi tra).
const Map<String, String> _vietnameseWithoutAccent = {
  'à': 'a', 'á': 'a', 'ạ': 'a', 'ả': 'a', 'ã': 'a',
  'â': 'a', 'ầ': 'a', 'ấ': 'a', 'ậ': 'a', 'ẩ': 'a', 'ẫ': 'a',
  'ă': 'a', 'ằ': 'a', 'ắ': 'a', 'ặ': 'a', 'ẳ': 'a', 'ẵ': 'a',
  'è': 'e', 'é': 'e', 'ẹ': 'e', 'ẻ': 'e', 'ẽ': 'e',
  'ê': 'e', 'ề': 'e', 'ế': 'e', 'ệ': 'e', 'ể': 'e', 'ễ': 'e',
  'ì': 'i', 'í': 'i', 'ị': 'i', 'ỉ': 'i', 'ĩ': 'i',
  'ò': 'o', 'ó': 'o', 'ọ': 'o', 'ỏ': 'o', 'õ': 'o',
  'ô': 'o', 'ồ': 'o', 'ố': 'o', 'ộ': 'o', 'ổ': 'o', 'ỗ': 'o',
  'ơ': 'o', 'ờ': 'o', 'ớ': 'o', 'ợ': 'o', 'ở': 'o', 'ỡ': 'o',
  'ù': 'u', 'ú': 'u', 'ụ': 'u', 'ủ': 'u', 'ũ': 'u',
  'ư': 'u', 'ừ': 'u', 'ứ': 'u', 'ự': 'u', 'ử': 'u', 'ữ': 'u',
  'ỳ': 'y', 'ý': 'y', 'ỵ': 'y', 'ỷ': 'y', 'ỹ': 'y',
  'đ': 'd',
};

String _normalizeChar(String char) {
  final lower = char.toLowerCase();
  return _vietnameseWithoutAccent[lower] ?? lower;
}

String _normalize(String value) {
  final buffer = StringBuffer();
  for (var index = 0; index < value.length; index++) {
    buffer.write(_normalizeChar(value[index]));
  }
  return buffer.toString();
}

/// Chia `text` thành các đoạn; đoạn `match = true` là phần khớp `query`.
List<HighlightSegment> splitHighlight(String text, String? query) {
  final trimmed = (query ?? '').trim();
  if (text.isEmpty || trimmed.isEmpty) {
    return [HighlightSegment(text)];
  }

  final normalized = StringBuffer();
  final map = <int>[];
  for (var index = 0; index < text.length; index++) {
    final character = _normalizeChar(text[index]);
    for (var offset = 0; offset < character.length; offset++) {
      normalized.write(character[offset]);
      map.add(index);
    }
  }

  final normalizedQuery = _normalize(trimmed);
  if (normalizedQuery.isEmpty) {
    return [HighlightSegment(text)];
  }

  final haystack = normalized.toString();
  final segments = <HighlightSegment>[];
  var cursor = 0;
  var searchFrom = 0;

  while (searchFrom <= haystack.length - normalizedQuery.length) {
    final found = haystack.indexOf(normalizedQuery, searchFrom);
    if (found < 0) {
      break;
    }

    final start = map[found];
    final endNormalized = found + normalizedQuery.length;
    final end = endNormalized < map.length ? map[endNormalized] : text.length;

    if (start > cursor) {
      segments.add(HighlightSegment(text.substring(cursor, start)));
    }
    segments.add(HighlightSegment(text.substring(start, end), match: true));
    cursor = end;
    searchFrom = endNormalized;
  }

  if (cursor < text.length) {
    segments.add(HighlightSegment(text.substring(cursor)));
  }

  return segments.isEmpty ? [HighlightSegment(text)] : segments;
}
