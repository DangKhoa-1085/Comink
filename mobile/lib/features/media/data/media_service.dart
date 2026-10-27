import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../domain/media_models.dart';

/// Tải ảnh lên Cloudinary (nếu có khoá) hoặc backend local (dev), rồi đăng ký media.
class MediaService {
  MediaService(this._api);

  static const int maxImageBytes = 5 * 1024 * 1024;
  static const int maxImagesPerPost = 10;

  final ApiClient _api;
  final Dio _uploadDio = Dio();

  /// Tải tất cả ảnh và trả về danh sách `mediaIds` theo đúng thứ tự.
  Future<List<String>> uploadAll(
    List<XFile> files, {
    required String context,
  }) async {
    final ids = <String>[];
    for (final file in files) {
      final asset = await upload(file, context: context);
      ids.add(asset.id);
    }
    return ids;
  }

  Future<MediaAsset> upload(XFile file, {required String context}) async {
    final mimeType = _mimeTypeFor(file.name);
    if (mimeType == null) {
      throw const ApiException(code: 'MEDIA_UNSUPPORTED_TYPE');
    }

    final bytes = await file.readAsBytes();
    if (bytes.isEmpty || bytes.length > maxImageBytes) {
      throw const ApiException(code: 'MEDIA_TOO_LARGE');
    }

    final signatureData = await _api.post(
      '/api/media/signature',
      data: {'context': context, 'resourceType': 'Image'},
    );
    if (signatureData is! Map) {
      throw const ApiException(code: 'INTERNAL_ERROR');
    }
    final signature = MediaSignature.fromJson(signatureData.cast<String, dynamic>());

    final LocalUploadResult uploaded;
    if (signature.isCloudinary) {
      uploaded = await _uploadToCloudinary(signature, bytes, file.name, mimeType);
    } else {
      final form = FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: file.name),
      });
      final data = await _api.postMultipart(signature.uploadUrl, form);
      if (data is! Map) {
        throw const ApiException(code: 'INTERNAL_ERROR');
      }
      uploaded = LocalUploadResult.fromJson(data.cast<String, dynamic>());
    }

    final registerData = await _api.post(
      '/api/media',
      data: {
        'providerPublicId': uploaded.providerPublicId,
        'url': uploaded.url,
        'thumbnailUrl': null,
        'mimeType': uploaded.mimeType.isEmpty ? mimeType : uploaded.mimeType,
        'sizeBytes': uploaded.sizeBytes,
        'context': context,
      },
    );
    if (registerData is! Map) {
      throw const ApiException(code: 'INTERNAL_ERROR');
    }
    return MediaAsset.fromJson(registerData.cast<String, dynamic>());
  }

  Future<LocalUploadResult> _uploadToCloudinary(
    MediaSignature signature,
    List<int> bytes,
    String fileName,
    String mimeType,
  ) async {
    final form = FormData.fromMap({
      'file': MultipartFile.fromBytes(bytes, filename: fileName),
      'api_key': signature.apiKey,
      'timestamp': signature.timestamp,
      'signature': signature.signature,
      'folder': signature.folder,
      if (signature.uploadPreset.isNotEmpty) 'upload_preset': signature.uploadPreset,
    });

    try {
      final response = await _uploadDio.post<dynamic>(signature.uploadUrl, data: form);
      final data = response.data;
      if (data is! Map) {
        throw const ApiException(code: 'MEDIA_UPLOAD_FAILED');
      }
      final json = data.cast<String, dynamic>();
      return LocalUploadResult(
        provider: 'Cloudinary',
        providerPublicId: (json['public_id'] ?? '').toString(),
        url: (json['secure_url'] ?? '').toString(),
        mimeType: mimeType,
        sizeBytes: json['bytes'] is int
            ? json['bytes'] as int
            : int.tryParse(json['bytes']?.toString() ?? '') ?? bytes.length,
      );
    } on DioException {
      throw const ApiException(code: 'MEDIA_UPLOAD_FAILED');
    }
  }

  static String? _mimeTypeFor(String fileName) {
    final lower = fileName.toLowerCase();
    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) {
      return 'image/jpeg';
    }
    if (lower.endsWith('.png')) {
      return 'image/png';
    }
    if (lower.endsWith('.webp')) {
      return 'image/webp';
    }
    return null;
  }
}

final mediaServiceProvider = Provider<MediaService>((ref) {
  return MediaService(ref.watch(apiClientProvider));
});
