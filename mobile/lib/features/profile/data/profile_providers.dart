import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../domain/my_profile.dart';

/// Hồ sơ của người dùng hiện tại; làm mới bằng `ref.invalidate(myProfileProvider)`.
final myProfileProvider = FutureProvider.autoDispose<MyProfile>((ref) async {
  final api = ref.watch(apiClientProvider);
  final data = await api.get('/api/users/me');
  if (data is! Map) {
    throw const ApiException(code: 'INTERNAL_ERROR');
  }
  return MyProfile.fromJson(data.cast<String, dynamic>());
});
