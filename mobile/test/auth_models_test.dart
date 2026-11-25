import 'package:flutter_test/flutter_test.dart';

import 'package:comink_mobile/features/auth/domain/auth_models.dart';

void main() {
  test('AuthResponse parse đúng token và người dùng', () {
    final auth = AuthResponse.fromJson({
      'accessToken': 'access-1',
      'accessTokenExpiresAt': '2026-10-27T12:00:00Z',
      'refreshToken': 'refresh-1',
      'refreshTokenExpiresAt': '2026-11-26T12:00:00Z',
      'user': {
        'id': 'user-1',
        'userName': 'an.demo',
        'email': 'an.demo@comink.local',
        'displayName': 'An Nguyễn',
        'roles': ['User'],
      },
    });

    expect(auth.accessToken, 'access-1');
    expect(auth.refreshToken, 'refresh-1');
    expect(auth.user.userName, 'an.demo');
    expect(auth.user.isAdmin, isFalse);
    expect(auth.user.roles, ['User']);
  });

  test('AuthUser round-trip qua JSON', () {
    const user = AuthUser(
      id: 'user-1',
      userName: 'admin',
      email: 'admin@comink.local',
      displayName: 'Administrator',
      roles: ['Admin', 'User'],
    );

    final parsed = AuthUser.fromJson(user.toJson());

    expect(parsed.id, user.id);
    expect(parsed.displayName, user.displayName);
    expect(parsed.isAdmin, isTrue);
  });
}
