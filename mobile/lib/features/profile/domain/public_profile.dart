import '../../friends/domain/friends_models.dart';

/// Hồ sơ công khai của một người dùng (`GET /api/users/{id}`).
class PublicProfile {
  const PublicProfile({
    required this.id,
    required this.userName,
    required this.displayName,
    required this.followerCount,
    required this.followingCount,
    required this.friendCount,
    required this.relationship,
    required this.createdAt,
    this.bio,
    this.avatarUrl,
    this.coverUrl,
    this.location,
  });

  final String id;
  final String userName;
  final String displayName;
  final String? bio;
  final String? avatarUrl;
  final String? coverUrl;
  final String? location;
  final int followerCount;
  final int followingCount;
  final int friendCount;
  final Relationship relationship;
  final DateTime createdAt;

  factory PublicProfile.fromJson(Map<String, dynamic> json) => PublicProfile(
        id: (json['id'] ?? '').toString(),
        userName: (json['userName'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        bio: json['bio']?.toString(),
        avatarUrl: json['avatarUrl']?.toString(),
        coverUrl: json['coverUrl']?.toString(),
        location: json['location']?.toString(),
        followerCount: _intValue(json['followerCount']),
        followingCount: _intValue(json['followingCount']),
        friendCount: _intValue(json['friendCount']),
        relationship: Relationship.fromJson(
          (json['relationship'] as Map?)?.cast<String, dynamic>() ?? const {},
        ),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
      );

  static int _intValue(dynamic value) =>
      value is int ? value : int.tryParse(value?.toString() ?? '') ?? 0;
}
