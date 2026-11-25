import '../../auth/domain/auth_models.dart';

/// Hồ sơ đầy đủ của chính người dùng (`GET /api/users/me`).
class MyProfile {
  const MyProfile({
    required this.id,
    required this.userName,
    required this.email,
    required this.displayName,
    required this.followerCount,
    required this.followingCount,
    required this.friendCount,
    required this.roles,
    required this.createdAt,
    this.bio,
    this.avatarUrl,
    this.coverUrl,
    this.location,
    this.dateOfBirth,
    this.language = 'vi',
  });

  final String id;
  final String userName;
  final String email;
  final String displayName;
  final String? bio;
  final String? avatarUrl;
  final String? coverUrl;
  final String? location;
  final String? dateOfBirth;
  final int followerCount;
  final int followingCount;
  final int friendCount;
  final String language;
  final List<String> roles;
  final DateTime createdAt;

  factory MyProfile.fromJson(Map<String, dynamic> json) {
    return MyProfile(
      id: (json['id'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      email: (json['email'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      bio: _nullableString(json['bio']),
      avatarUrl: _nullableString(json['avatarUrl']),
      coverUrl: _nullableString(json['coverUrl']),
      location: _nullableString(json['location']),
      dateOfBirth: _nullableString(json['dateOfBirth']),
      followerCount: _intValue(json['followerCount']),
      followingCount: _intValue(json['followingCount']),
      friendCount: _intValue(json['friendCount']),
      language: (json['language'] ?? 'vi').toString(),
      roles: (json['roles'] as List<dynamic>? ?? const [])
          .map((role) => role.toString())
          .toList(growable: false),
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
          DateTime.now(),
    );
  }

  AuthUser toAuthUser() => AuthUser(
        id: id,
        userName: userName,
        email: email,
        displayName: displayName,
        roles: roles,
      );

  static String? _nullableString(dynamic value) {
    final text = value?.toString().trim();
    return (text == null || text.isEmpty) ? null : text;
  }

  static int _intValue(dynamic value) =>
      value is int ? value : int.tryParse(value?.toString() ?? '') ?? 0;
}
