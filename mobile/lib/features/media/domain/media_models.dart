/// Thông tin backend cấp để tải ảnh lên.
class MediaSignature {
  const MediaSignature({
    required this.provider,
    required this.uploadUrl,
    required this.signature,
    required this.apiKey,
    required this.cloudName,
    required this.timestamp,
    required this.uploadPreset,
    required this.folder,
  });

  final String provider;
  final String uploadUrl;
  final String signature;
  final String apiKey;
  final String cloudName;
  final int timestamp;
  final String uploadPreset;
  final String folder;

  bool get isCloudinary => provider == 'Cloudinary';

  factory MediaSignature.fromJson(Map<String, dynamic> json) => MediaSignature(
        provider: (json['provider'] ?? 'Local').toString(),
        uploadUrl: (json['uploadUrl'] ?? '').toString(),
        signature: (json['signature'] ?? '').toString(),
        apiKey: (json['apiKey'] ?? '').toString(),
        cloudName: (json['cloudName'] ?? '').toString(),
        timestamp: json['timestamp'] is int
            ? json['timestamp'] as int
            : int.tryParse(json['timestamp']?.toString() ?? '') ?? 0,
        uploadPreset: (json['uploadPreset'] ?? '').toString(),
        folder: (json['folder'] ?? '').toString(),
      );
}

class LocalUploadResult {
  const LocalUploadResult({
    required this.provider,
    required this.providerPublicId,
    required this.url,
    required this.mimeType,
    required this.sizeBytes,
  });

  final String provider;
  final String providerPublicId;
  final String url;
  final String mimeType;
  final int sizeBytes;

  factory LocalUploadResult.fromJson(Map<String, dynamic> json) => LocalUploadResult(
        provider: (json['provider'] ?? '').toString(),
        providerPublicId: (json['providerPublicId'] ?? '').toString(),
        url: (json['url'] ?? '').toString(),
        mimeType: (json['mimeType'] ?? '').toString(),
        sizeBytes: json['sizeBytes'] is int
            ? json['sizeBytes'] as int
            : int.tryParse(json['sizeBytes']?.toString() ?? '') ?? 0,
      );
}

/// Tài sản media đã đăng ký trong DB, dùng làm `mediaIds` khi đăng bài/gửi tin.
class MediaAsset {
  const MediaAsset({
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

  factory MediaAsset.fromJson(Map<String, dynamic> json) => MediaAsset(
        id: (json['id'] ?? '').toString(),
        url: (json['url'] ?? '').toString(),
        thumbnailUrl: json['thumbnailUrl']?.toString(),
        width: json['width'] is int ? json['width'] as int : null,
        height: json['height'] is int ? json['height'] as int : null,
      );
}
