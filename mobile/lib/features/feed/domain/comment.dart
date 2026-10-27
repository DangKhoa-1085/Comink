import 'post.dart';

/// Bình luận của bài viết.
class PostComment {
  const PostComment({
    required this.id,
    required this.postId,
    required this.author,
    required this.content,
    required this.replyCount,
    required this.createdAt,
    this.parentCommentId,
  });

  final String id;
  final String postId;
  final String? parentCommentId;
  final PostAuthor author;
  final String content;
  final int replyCount;
  final DateTime createdAt;

  factory PostComment.fromJson(Map<String, dynamic> json) => PostComment(
        id: (json['id'] ?? '').toString(),
        postId: (json['postId'] ?? '').toString(),
        parentCommentId: json['parentCommentId']?.toString(),
        author: PostAuthor.fromJson(
          (json['author'] as Map?)?.cast<String, dynamic>() ?? const {},
        ),
        content: (json['content'] ?? '').toString(),
        replyCount: json['replyCount'] is int
            ? json['replyCount'] as int
            : int.tryParse(json['replyCount']?.toString() ?? '') ?? 0,
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal() ??
            DateTime.now(),
      );
}
