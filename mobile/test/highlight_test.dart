import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/utils/highlight.dart';

void main() {
  test('splitHighlight marks exact Vietnamese word', () {
    final segments = splitHighlight('Bóng khê thôi khầy', 'khầy');

    expect(segments.map((segment) => segment.match).toList(), [false, true]);
    expect(segments.last.text, 'khầy');
  });

  test('splitHighlight matches without accents and ignores case', () {
    final segments = splitHighlight('Cuộc HỌP nhóm lúc 9 giờ', 'hop');

    expect(segments.any((segment) => segment.match), isTrue);
    expect(segments.firstWhere((segment) => segment.match).text, 'HỌP');
  });

  test('splitHighlight marks every occurrence', () {
    final segments = splitHighlight('hop hop HỌP', 'hop');

    final matches = segments.where((segment) => segment.match).toList();
    expect(matches.length, 3);
    expect(matches.map((segment) => segment.text).toList(), ['hop', 'hop', 'HỌP']);
  });

  test('splitHighlight returns single segment for empty query', () {
    final segments = splitHighlight('xin chào', '   ');

    expect(segments, hasLength(1));
    expect(segments.single.match, isFalse);
    expect(segments.single.text, 'xin chào');
  });
}
