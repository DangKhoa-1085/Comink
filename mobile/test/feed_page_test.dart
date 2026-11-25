import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/i18n/app_localizations.dart';
import 'package:comink_mobile/features/feed/data/feed_providers.dart';
import 'package:comink_mobile/features/feed/domain/post.dart';
import 'package:comink_mobile/features/feed/presentation/feed_page.dart';
import 'package:comink_mobile/features/notifications/data/notification_providers.dart';

Widget _app(Widget home, FeedHistory feed) => ProviderScope(
      overrides: [
        feedProvider.overrideWith((ref) async => feed),
        notificationsProvider.overrideWith((ref) async => []),
      ],
      child: MaterialApp(
        locale: const Locale('vi'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: home,
      ),
    );

Post _post(String id, String content) => Post(
      id: id,
      author: const PostAuthor(id: 'u1', userName: 'an.demo', displayName: 'An Nguyễn'),
      content: content,
      visibility: 'Public',
      media: const [],
      likeCount: 0,
      commentCount: 0,
      shareCount: 0,
      likedByMe: false,
      isMine: true,
      createdAt: DateTime(2026, 10, 27),
    );

void main() {
  testWidgets('Bảng tin rỗng hiện empty state', (tester) async {
    await tester.pumpWidget(
      _app(
        const FeedPage(),
        const FeedHistory(posts: [], nextCursor: null, hasMore: false),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Bảng tin'), findsOneWidget);
    expect(find.textContaining('Chưa có bài viết nào'), findsOneWidget);
  });

  testWidgets('Bảng tin hiện bài viết và nút tải thêm khi còn trang', (tester) async {
    await tester.pumpWidget(
      _app(
        const FeedPage(),
        FeedHistory(
          posts: [_post('p1', 'Nội dung thử')],
          nextCursor: 'next-page',
          hasMore: true,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Nội dung thử'), findsOneWidget);
    expect(find.text('Tải thêm bài viết'), findsOneWidget);
  });
}
