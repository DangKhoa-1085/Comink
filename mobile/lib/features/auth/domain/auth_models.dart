/// Người dùng đã đăng nhập (rút gọn từ `AuthResponse.user`).
class AuthUser {
  const AuthUser({
    required this.id,
    required this.userName,
    required this.email,
    required this.displayName,
    required this.roles,
  });

  final String id;
  final String userName;
  final String email;
  final String displayName;
  final List<String> roles;

  bool get isAdmin => roles.contains('Admin');

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: (json['id'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      email: (json['email'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      roles: (json['roles'] as List<dynamic>? ?? const [])
          .map((role) => role.toString())
          .toList(growable: false),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'userName': userName,
        'email': email,
        'displayName': displayName,
        'roles': roles,
      };
}

/// Kết quả `/api/auth/login`, `/register`, `/refresh`.
class AuthResponse {
  const AuthResponse({
    required this.accessToken,
    required this.accessTokenExpiresAt,
    required this.refreshToken,
    required this.refreshTokenExpiresAt,
    required this.user,
  });

  final String accessToken;
  final DateTime accessTokenExpiresAt;
  final String refreshToken;
  final DateTime refreshTokenExpiresAt;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: (json['accessToken'] ?? '').toString(),
      accessTokenExpiresAt: _parseDate(json['accessTokenExpiresAt']),
      refreshToken: (json['refreshToken'] ?? '').toString(),
      refreshTokenExpiresAt: _parseDate(json['refreshTokenExpiresAt']),
      user: AuthUser.fromJson(
        (json['user'] as Map?)?.cast<String, dynamic>() ?? const {},
      ),
    );
  }

  static DateTime _parseDate(dynamic value) =>
      DateTime.tryParse(value?.toString() ?? '') ?? DateTime.now().toUtc();
}
