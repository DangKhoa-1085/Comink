/// Quan hệ giữa người xem và một người dùng khác.
class Relationship {
  const Relationship({
    required this.friendship,
    required this.following,
    required this.followedBy,
  });

  final String friendship;
  final bool following;
  final bool followedBy;

  factory Relationship.fromJson(Map<String, dynamic> json) => Relationship(
        friendship: (json['friendship'] ?? 'None').toString(),
        following: json['following'] == true,
        followedBy: json['followedBy'] == true,
      );
}

/// Người dùng rút gọn trong tìm kiếm/danh sách.
class UserSummary {
  const UserSummary({
    required this.id,
    required this.userName,
    required this.displayName,
    required this.relationship,
    this.avatarUrl,
    this.bio,
  });

  final String id;
  final String userName;
  final String displayName;
  final String? avatarUrl;
  final String? bio;
  final Relationship relationship;

  factory UserSummary.fromJson(Map<String, dynamic> json) => UserSummary(
        id: (json['id'] ?? '').toString(),
        userName: (json['userName'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        avatarUrl: _nullable(json['avatarUrl']),
        bio: _nullable(json['bio']),
        relationship: Relationship.fromJson(
          (json['relationship'] as Map?)?.cast<String, dynamic>() ?? const {},
        ),
      );

  static String? _nullable(dynamic value) {
    final text = value?.toString().trim();
    return (text == null || text.isEmpty) ? null : text;
  }
}

/// Một dòng bạn bè/lời mời kết bạn.
class FriendshipSummary {
  const FriendshipSummary({
    required this.id,
    required this.user,
    required this.status,
    required this.createdAt,
    this.respondedAt,
  });

  final String id;
  final UserSummary user;
  final String status;
  final DateTime createdAt;
  final DateTime? respondedAt;

  bool get isAccepted => status == 'Accepted';
  bool get isPending => status == 'Pending';

  factory FriendshipSummary.fromJson(Map<String, dynamic> json) => FriendshipSummary(
        id: (json['id'] ?? '').toString(),
        user: UserSummary.fromJson(
          (json['user'] as Map?)?.cast<String, dynamic>() ?? const {},
        ),
        status: (json['status'] ?? 'Pending').toString(),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
        respondedAt: DateTime.tryParse((json['respondedAt'] ?? '').toString())?.toLocal(),
      );
}
