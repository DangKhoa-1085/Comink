import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/core/config/app_config.dart';

void main() {
  test('resolveMediaUrl giữ nguyên URL tuyệt đối của Cloudinary hoặc HTTP', () {
    expect(
      AppConfig.resolveMediaUrl(
        'https://res.cloudinary.com/demo/image/upload/v1/post.jpg',
        baseUrl: 'http://127.0.0.1:5117',
      ),
      'https://res.cloudinary.com/demo/image/upload/v1/post.jpg',
    );
    expect(
      AppConfig.resolveMediaUrl(
        'http://192.168.1.3:5117/uploads/a.jpg',
        baseUrl: 'http://127.0.0.1:5117',
      ),
      'http://192.168.1.3:5117/uploads/a.jpg',
    );
  });

  test('resolveMediaUrl ghép đường dẫn tương đối /uploads với baseUrl', () {
    expect(
      AppConfig.resolveMediaUrl(
        '/uploads/post/abc.jpg',
        baseUrl: 'http://127.0.0.1:5117/',
      ),
      'http://127.0.0.1:5117/uploads/post/abc.jpg',
    );
    expect(
      AppConfig.resolveMediaUrl(
        'uploads/message/xyz.png',
        baseUrl: 'http://10.0.2.2:5117',
      ),
      'http://10.0.2.2:5117/uploads/message/xyz.png',
    );
  });

  test('resolveMediaUrl xử lý chuỗi rỗng và khoảng trắng', () {
    expect(AppConfig.resolveMediaUrl('   ', baseUrl: 'http://127.0.0.1:5117'), '');
  });

  test('resolveLiveKitUrl đổi host loopback sang host của apiBaseUrl', () {
    expect(
      AppConfig.resolveLiveKitUrl(
        'ws://localhost:7880',
        baseUrl: 'http://192.168.1.3:5117',
      ),
      'ws://192.168.1.3:7880',
    );
    expect(
      AppConfig.resolveLiveKitUrl(
        'ws://localhost:7880',
        baseUrl: 'http://10.0.2.2:5117',
      ),
      'ws://10.0.2.2:7880',
    );
    expect(
      AppConfig.resolveLiveKitUrl(
        'ws://localhost:7880',
        baseUrl: 'http://127.0.0.1:5117',
      ),
      'ws://127.0.0.1:7880',
    );
    expect(
      AppConfig.resolveLiveKitUrl(
        'ws://localhost:7880',
        baseUrl: 'http://localhost:5117',
      ),
      'ws://127.0.0.1:7880',
    );
  });

  test('resolveLiveKitUrl giữ nguyên URL không phải loopback', () {
    expect(
      AppConfig.resolveLiveKitUrl(
        'wss://comink.livekit.cloud',
        baseUrl: 'http://127.0.0.1:5117',
      ),
      'wss://comink.livekit.cloud',
    );
    expect(
      AppConfig.resolveLiveKitUrl(
        'ws://192.168.1.3:7880',
        baseUrl: 'http://127.0.0.1:5117',
      ),
      'ws://192.168.1.3:7880',
    );
  });
}
