/// Người dùng rút gọn trong bài viết/bình luận.
class PostAuthor {
  const PostAuthor({
    required this.id,
    required this.userName,
    required this.displayName,
    this.avatarUrl,
    this.bio,
  });

  final String id;
  final String userName;
  final String displayName;
  final String? avatarUrl;
  final String? bio;

  factory PostAuthor.fromJson(Map<String, dynamic> json) => PostAuthor(
        id: (json['id'] ?? '').toString(),
        userName: (json['userName'] ?? '').toString(),
        displayName: (json['displayName'] ?? '').toString(),
        avatarUrl: _nullable(json['avatarUrl']),
        bio: _nullable(json['bio']),
      );

  static String? _nullable(dynamic value) {
    final text = value?.toString().trim();
    return (text == null || text.isEmpty) ? null : text;
  }
}

class PostMediaItem {
  const PostMediaItem({
    required this.id,
    required this.url,
    this.thumbnailUrl,
    this.width,
    this.height,
  });

  final String id;
  final String url;
  final String? thumbnailUrl;
  final int? width;
  final int? height;

  factory PostMediaItem.fromJson(Map<String, dynamic> json) => PostMediaItem(
        id: (json['id'] ?? '').toString(),
        url: (json['url'] ?? '').toString(),
        thumbnailUrl: json['thumbnailUrl']?.toString(),
        width: json['width'] is int ? json['width'] as int : null,
        height: json['height'] is int ? json['height'] as int : null,
      );
}

/// Bài viết trên bảng tin.
class Post {
  const Post({
    required this.id,
    required this.author,
    required this.visibility,
    required this.media,
    required this.likeCount,
    required this.commentCount,
    required this.shareCount,
    required this.likedByMe,
    required this.isMine,
    required this.createdAt,
    this.content,
    this.sharedPost,
  });

  final String id;
  final PostAuthor author;
  final String? content;
  final String visibility;
  final List<PostMediaItem> media;
  final Post? sharedPost;
  final int likeCount;
  final int commentCount;
  final int shareCount;
  final bool likedByMe;
  final bool isMine;
  final DateTime createdAt;

  factory Post.fromJson(Map<String, dynamic> json) {
    final mediaJson = json['media'] as List<dynamic>? ?? const [];
    final shared = json['sharedPost'];
    return Post(
      id: (json['id'] ?? '').toString(),
      author: PostAuthor.fromJson((json['author'] as Map?)?.cast<String, dynamic>() ?? const {}),
      content: (json['content']?.toString().trim().isEmpty ?? true) ? null : json['content'].toString(),
      visibility: (json['visibility'] ?? 'Friends').toString(),
      media: mediaJson
          .whereType<Map>()
          .map((item) => PostMediaItem.fromJson(item.cast<String, dynamic>()))
          .toList(growable: false),
      sharedPost: shared is Map ? Post.fromJson(shared.cast<String, dynamic>()) : null,
      likeCount: _intValue(json['likeCount']),
      commentCount: _intValue(json['commentCount']),
      shareCount: _intValue(json['shareCount']),
      likedByMe: json['likedByMe'] == true,
      isMine: json['isMine'] == true,
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
          DateTime.now(),
    );
  }

  static int _intValue(dynamic value) =>
      value is int ? value : int.tryParse(value?.toString() ?? '') ?? 0;
}
