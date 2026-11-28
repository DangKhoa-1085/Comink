import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/network/providers.dart';
import '../../../core/widgets/app_avatar.dart';
import '../../auth/data/session_controller.dart';
import '../../friends/data/friends_providers.dart';
import '../../media/data/media_service.dart';
import '../../profile/data/profile_providers.dart';
import '../data/locket_providers.dart';

enum _LocketFilterId { none, warm, vintage, bw, vivid, cool }

const _filterMatrices = <_LocketFilterId, List<double>>{
  _LocketFilterId.none: [
    1, 0, 0, 0, 0,
    0, 1, 0, 0, 0,
    0, 0, 1, 0, 0,
    0, 0, 0, 1, 0,
  ],
  _LocketFilterId.warm: [
    1.12, 0.06, 0, 0, 10,
    0, 1.02, 0, 0, 4,
    0, 0, 0.88, 0, 0,
    0, 0, 0, 1, 0,
  ],
  _LocketFilterId.vintage: [
    0.393, 0.769, 0.189, 0, -8,
    0.349, 0.686, 0.168, 0, -8,
    0.272, 0.534, 0.131, 0, -4,
    0, 0, 0, 1, 0,
  ],
  _LocketFilterId.bw: [
    0.33, 0.59, 0.11, 0, 0,
    0.33, 0.59, 0.11, 0, 0,
    0.33, 0.59, 0.11, 0, 0,
    0, 0, 0, 1, 0,
  ],
  _LocketFilterId.vivid: [
    1.25, -0.12, -0.12, 0, 0,
    -0.12, 1.25, -0.12, 0, 0,
    -0.12, -0.12, 1.25, 0, 0,
    0, 0, 0, 1, 0,
  ],
  _LocketFilterId.cool: [
    0.92, 0, 0.06, 0, 0,
    0, 1.02, 0.04, 0, 0,
    0, 0.08, 1.14, 0, 8,
    0, 0, 0, 1, 0,
  ],
};

/// Màn hình chụp & chỉnh sửa Comink Locket nền tối tối giản (theo phong cách app Locket).
class LocketStudioPage extends ConsumerStatefulWidget {
  const LocketStudioPage({super.key});

  @override
  ConsumerState<LocketStudioPage> createState() => _LocketStudioPageState();
}

class _LocketStudioPageState extends ConsumerState<LocketStudioPage> {
  final GlobalKey _cropBoundaryKey = GlobalKey();
  final _captionController = TextEditingController();
  final ImagePicker _picker = ImagePicker();

  Uint8List? _imageBytes;
  CameraDevice _cameraDevice = CameraDevice.rear;
  double _zoom = 1.0;
  int _rotationSteps = 0;
  bool _flippedX = false;
  Offset _offset = Offset.zero;
  _LocketFilterId _filter = _LocketFilterId.none;

  String _audienceType = 'AllFriends';
  final Set<String> _selectedFriendIds = {};
  bool _publishing = false;

  @override
  void dispose() {
    _captionController.dispose();
    super.dispose();
  }

  void _resetTransforms() {
    setState(() {
      _zoom = 1.0;
      _rotationSteps = 0;
      _flippedX = false;
      _offset = Offset.zero;
      _filter = _LocketFilterId.none;
    });
  }

  Future<void> _pickImage(ImageSource source) async {
    if (_publishing) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);

    try {
      final picked = await _picker.pickImage(
        source: source,
        preferredCameraDevice: _cameraDevice,
        imageQuality: 90,
        maxWidth: 1600,
      );
      if (picked == null || !mounted) {
        return;
      }
      final bytes = await picked.readAsBytes();
      if (!mounted) {
        return;
      }
      setState(() {
        _imageBytes = bytes;
        _zoom = 1.0;
        _rotationSteps = 0;
        _flippedX = false;
        _offset = Offset.zero;
        _filter = _LocketFilterId.none;
      });
    } catch (_) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.error('MEDIA_UPLOAD_FAILED'))));
      }
    }
  }

  Future<void> _openAudiencePicker() async {
    final l10n = AppLocalizations.of(context);
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (sheetContext) => Consumer(
        builder: (context, ref, _) {
          final friends = ref.watch(friendsProvider);
          return StatefulBuilder(
            builder: (context, setModalState) => SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      l10n.t('locket_pick_friends_title'),
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      children: [
                        ChoiceChip(
                          label: Text(l10n.t('locket_audience_all')),
                          selected: _audienceType == 'AllFriends',
                          onSelected: (_) {
                            setState(() => _audienceType = 'AllFriends');
                            setModalState(() {});
                          },
                        ),
                        ChoiceChip(
                          label: Text(
                            l10n.t(
                              'locket_audience_selected',
                              {'count': '${_selectedFriendIds.length}'},
                            ),
                          ),
                          selected: _audienceType == 'SelectedFriends',
                          onSelected: (_) {
                            setState(() => _audienceType = 'SelectedFriends');
                            setModalState(() {});
                          },
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    if (_audienceType == 'SelectedFriends')
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxHeight: 260),
                        child: friends.when(
                          loading: () => const Center(child: CircularProgressIndicator()),
                          error: (_, _) => Center(child: Text(l10n.t('friends_error'))),
                          data: (items) {
                            if (items.isEmpty) {
                              return Padding(
                                padding: const EdgeInsets.symmetric(vertical: 12),
                                child: Text(l10n.t('locket_no_friends')),
                              );
                            }
                            return ListView.builder(
                              shrinkWrap: true,
                              itemCount: items.length,
                              itemBuilder: (context, index) {
                                final friend = items[index].user;
                                final selected = _selectedFriendIds.contains(friend.id);
                                return CheckboxListTile(
                                  dense: true,
                                  value: selected,
                                  onChanged: (checked) {
                                    setState(() {
                                      if (checked == true) {
                                        _selectedFriendIds.add(friend.id);
                                      } else {
                                        _selectedFriendIds.remove(friend.id);
                                      }
                                    });
                                    setModalState(() {});
                                  },
                                  secondary: AppAvatar(
                                    name: friend.displayName,
                                    url: friend.avatarUrl,
                                    radius: 16,
                                  ),
                                  title: Text(friend.displayName),
                                  subtitle: Text('@${friend.userName}'),
                                );
                              },
                            );
                          },
                        ),
                      ),
                    const SizedBox(height: 10),
                    FilledButton(
                      onPressed: () => Navigator.of(sheetContext).pop(),
                      child: Text(l10n.t('common_ok')),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Future<void> _publish() async {
    if (_publishing || _imageBytes == null) {
      return;
    }

    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);

    if (_audienceType == 'SelectedFriends' && _selectedFriendIds.isEmpty) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.t('locket_pick_at_least_one'))));
      return;
    }

    setState(() => _publishing = true);

    try {
      // Xuất đúng khung vuông 1:1 đang hiển thị (đã cắt, xoay, lật và lọc màu).
      final boundary =
          _cropBoundaryKey.currentContext?.findRenderObject() as RenderRepaintBoundary?;
      Uint8List bytesToUpload = _imageBytes!;
      String mimeType = 'image/jpeg';
      String fileName = 'locket-${DateTime.now().millisecondsSinceEpoch}.jpg';

      if (boundary != null) {
        final uiImage = await boundary.toImage(pixelRatio: 2.5);
        final byteData = await uiImage.toByteData(format: ui.ImageByteFormat.png);
        if (byteData != null) {
          final pngBytes = byteData.buffer.asUint8List();
          if (pngBytes.length <= MediaService.maxImageBytes) {
            bytesToUpload = pngBytes;
            mimeType = 'image/png';
            fileName = 'locket-${DateTime.now().millisecondsSinceEpoch}.png';
          }
        }
      }

      final asset = await ref.read(mediaServiceProvider).uploadBytes(
            bytesToUpload,
            fileName: fileName,
            mimeType: mimeType,
            context: 'Locket',
          );

      final caption = _captionController.text.trim();
      await ref.read(apiClientProvider).post(
        '/api/lockets',
        data: {
          'mediaAssetId': asset.id,
          if (caption.isNotEmpty) 'caption': caption,
          'audienceType': _audienceType,
          if (_audienceType == 'SelectedFriends')
            'recipientIds': _selectedFriendIds.toList(growable: false),
        },
      );

      ref.invalidate(locketsProvider);
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.t('locket_posted'))));
        navigator.pop(true);
      }
    } on ApiException catch (error) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.error(error.code))));
      }
    } catch (_) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.error('MEDIA_UPLOAD_FAILED'))));
      }
    } finally {
      if (mounted) {
        setState(() => _publishing = false);
      }
    }
  }

  String _filterLabel(AppLocalizations l10n, _LocketFilterId id) => switch (id) {
        _LocketFilterId.none => l10n.t('locket_filter_none'),
        _LocketFilterId.warm => l10n.t('locket_filter_warm'),
        _LocketFilterId.vintage => l10n.t('locket_filter_vintage'),
        _LocketFilterId.bw => l10n.t('locket_filter_bw'),
        _LocketFilterId.vivid => l10n.t('locket_filter_vivid'),
        _LocketFilterId.cool => l10n.t('locket_filter_cool'),
      };

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final user = ref.watch(sessionControllerProvider).user;
    final profile = ref.watch(myProfileProvider).asData?.value;

    final audienceLabel = _audienceType == 'AllFriends'
        ? l10n.t('locket_audience_all')
        : l10n.t('locket_audience_selected', {'count': '${_selectedFriendIds.length}'});

    return Scaffold(
      backgroundColor: const Color(0xFF09090B),
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 20),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight - 30),
              child: IntrinsicHeight(
                child: Column(
                  children: [
                    // Hàng đầu kiểu Locket: Đóng — Pill bạn bè — Avatar
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        IconButton(
                          onPressed: _publishing ? null : () => Navigator.of(context).pop(false),
                          style: IconButton.styleFrom(backgroundColor: Colors.white12),
                          icon: const Icon(Icons.close, color: Colors.white),
                        ),
                        InkWell(
                          borderRadius: BorderRadius.circular(99),
                          onTap: _publishing ? null : _openAudiencePicker,
                          child: Container(
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                            decoration: BoxDecoration(
                              color: Colors.white12,
                              borderRadius: BorderRadius.circular(99),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Icon(Icons.people_alt_outlined, size: 16, color: Colors.white),
                                const SizedBox(width: 6),
                                Text(
                                  audienceLabel,
                                  style: const TextStyle(
                                    color: Colors.white,
                                    fontSize: 12.5,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        AppAvatar(
                          name: profile?.displayName ?? user?.displayName ?? '?',
                          url: profile?.avatarUrl,
                          radius: 17,
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),

                    // Khung vuông bo tròn cực mềm giống app Locket (Hình 2)
                    AspectRatio(
                      aspectRatio: 1,
                      child: Container(
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(34),
                          border: Border.all(
                            color: _imageBytes == null
                                ? Colors.white12
                                : const Color(0xFFF59E0B).withValues(alpha: 0.5),
                            width: 1.5,
                          ),
                          color: const Color(0xFF16161B),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: _imageBytes == null
                            ? InkWell(
                                onTap: () => _pickImage(ImageSource.camera),
                                child: Stack(
                                  children: [
                                    Positioned(
                                      top: 14,
                                      left: 14,
                                      child: Container(
                                        padding: const EdgeInsets.all(7),
                                        decoration: const BoxDecoration(
                                          color: Colors.black54,
                                          shape: BoxShape.circle,
                                        ),
                                        child: const Icon(
                                          Icons.bolt,
                                          size: 16,
                                          color: Color(0xFFFBBF24),
                                        ),
                                      ),
                                    ),
                                    Positioned(
                                      top: 14,
                                      right: 14,
                                      child: Container(
                                        padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
                                        decoration: BoxDecoration(
                                          color: Colors.black54,
                                          borderRadius: BorderRadius.circular(99),
                                        ),
                                        child: const Text(
                                          '1x',
                                          style: TextStyle(
                                            color: Colors.white,
                                            fontSize: 11.5,
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                      ),
                                    ),
                                    Center(
                                      child: Padding(
                                        padding: const EdgeInsets.symmetric(horizontal: 28),
                                        child: Column(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            const Icon(
                                              Icons.camera_alt_outlined,
                                              size: 46,
                                              color: Colors.white70,
                                            ),
                                            const SizedBox(height: 10),
                                            Text(
                                              l10n.t('locket_camera_hint'),
                                              textAlign: TextAlign.center,
                                              style: const TextStyle(
                                                color: Colors.white70,
                                                fontSize: 13,
                                                height: 1.35,
                                              ),
                                            ),
                                            const SizedBox(height: 6),
                                            Text(
                                              l10n.t('locket_subtitle_24h'),
                                              style: const TextStyle(
                                                color: Color(0xFFFBBF24),
                                                fontSize: 11.5,
                                                fontWeight: FontWeight.w600,
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              )
                            : GestureDetector(
                                onPanUpdate: (details) => setState(() => _offset += details.delta),
                                child: Stack(
                                  fit: StackFit.expand,
                                  children: [
                                    RepaintBoundary(
                                      key: _cropBoundaryKey,
                                      child: Container(
                                        color: const Color(0xFF09090B),
                                        child: ColorFiltered(
                                          colorFilter: ColorFilter.matrix(_filterMatrices[_filter]!),
                                          child: Transform.translate(
                                            offset: _offset,
                                            child: Transform.scale(
                                              scale: _zoom,
                                              child: Transform.rotate(
                                                angle: _rotationSteps * (math.pi / 2),
                                                child: Transform.flip(
                                                  flipX: _flippedX,
                                                  child: Image.memory(
                                                    _imageBytes!,
                                                    fit: BoxFit.cover,
                                                  ),
                                                ),
                                              ),
                                            ),
                                          ),
                                        ),
                                      ),
                                    ),
                                    // Lưới 3x3 căn khung (nằm ngoài RepaintBoundary để không dính vào ảnh xuất)
                                    IgnorePointer(
                                      child: CustomPaint(painter: _GridPainter()),
                                    ),
                                    // Thanh caption nổi giống Locket
                                    Positioned(
                                      left: 16,
                                      right: 16,
                                      bottom: 14,
                                      child: TextField(
                                        controller: _captionController,
                                        maxLength: 100,
                                        textAlign: TextAlign.center,
                                        style: const TextStyle(
                                          color: Colors.white,
                                          fontSize: 13,
                                          fontWeight: FontWeight.w600,
                                        ),
                                        decoration: InputDecoration(
                                          counterText: '',
                                          isDense: true,
                                          hintText: l10n.t('locket_caption_hint'),
                                          hintStyle: const TextStyle(color: Colors.white70, fontSize: 12.5),
                                          filled: true,
                                          fillColor: Colors.black.withValues(alpha: 0.58),
                                          contentPadding: const EdgeInsets.symmetric(
                                            horizontal: 16,
                                            vertical: 9,
                                          ),
                                          border: OutlineInputBorder(
                                            borderRadius: BorderRadius.circular(99),
                                            borderSide: BorderSide.none,
                                          ),
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                      ),
                    ),

                    if (_imageBytes != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        l10n.t('locket_drag_crop'),
                        style: const TextStyle(color: Colors.white54, fontSize: 11.5),
                      ),
                      const SizedBox(height: 6),
                      Row(
                        children: [
                          Text(
                            '${l10n.t('locket_zoom')} ${_zoom.toStringAsFixed(1)}x',
                            style: const TextStyle(color: Colors.white70, fontSize: 11.5),
                          ),
                          Expanded(
                            child: Slider(
                              min: 1.0,
                              max: 3.0,
                              value: _zoom,
                              activeColor: const Color(0xFFF59E0B),
                              onChanged: (value) => setState(() => _zoom = value),
                            ),
                          ),
                        ],
                      ),
                      Wrap(
                        spacing: 8,
                        alignment: WrapAlignment.center,
                        children: [
                          _DarkChipButton(
                            icon: Icons.rotate_right,
                            label: l10n.t('locket_rotate'),
                            onTap: () => setState(() => _rotationSteps = (_rotationSteps + 1) % 4),
                          ),
                          _DarkChipButton(
                            icon: Icons.flip,
                            label: l10n.t('locket_flip'),
                            onTap: () => setState(() => _flippedX = !_flippedX),
                          ),
                          _DarkChipButton(
                            icon: Icons.refresh,
                            label: l10n.t('locket_reset'),
                            onTap: _resetTransforms,
                          ),
                          _DarkChipButton(
                            icon: Icons.camera_alt_outlined,
                            label: l10n.t('locket_capture'),
                            onTap: () => _pickImage(ImageSource.camera),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      SizedBox(
                        height: 32,
                        child: ListView.separated(
                          scrollDirection: Axis.horizontal,
                          itemCount: _LocketFilterId.values.length,
                          separatorBuilder: (_, _) => const SizedBox(width: 6),
                          itemBuilder: (context, index) {
                            final preset = _LocketFilterId.values[index];
                            final selected = _filter == preset;
                            return ChoiceChip(
                              selected: selected,
                              label: Text(
                                _filterLabel(l10n, preset),
                                style: TextStyle(
                                  fontSize: 11.5,
                                  color: selected ? Colors.black : Colors.white,
                                ),
                              ),
                              selectedColor: const Color(0xFFFBBF24),
                              backgroundColor: Colors.white10,
                              onSelected: (_) => setState(() => _filter = preset),
                            );
                          },
                        ),
                      ),
                    ],

                    const Spacer(),
                    const SizedBox(height: 16),

                    // Hàng 3 nút đáy kiểu Locket: Thư viện — Nút vòng vàng — Lật camera
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                      children: [
                        IconButton(
                          tooltip: l10n.t('feed_add_image'),
                          onPressed: _publishing ? null : () => _pickImage(ImageSource.gallery),
                          style: IconButton.styleFrom(
                            backgroundColor: Colors.white12,
                            fixedSize: const Size(48, 48),
                          ),
                          icon: const Icon(Icons.photo_library_outlined, color: Colors.white),
                        ),
                        GestureDetector(
                          onTap: _publishing
                              ? null
                              : () {
                                  if (_imageBytes == null) {
                                    _pickImage(ImageSource.camera);
                                  } else {
                                    _publish();
                                  }
                                },
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Container(
                                width: 72,
                                height: 72,
                                padding: const EdgeInsets.all(4),
                                decoration: BoxDecoration(
                                  shape: BoxShape.circle,
                                  border: Border.all(
                                    color: const Color(0xFFF59E0B),
                                    width: 3.5,
                                  ),
                                ),
                                child: Container(
                                  decoration: const BoxDecoration(
                                    shape: BoxShape.circle,
                                    color: Colors.white,
                                  ),
                                  child: _publishing
                                      ? const Center(
                                          child: SizedBox(
                                            width: 24,
                                            height: 24,
                                            child: CircularProgressIndicator(strokeWidth: 2.5),
                                          ),
                                        )
                                      : Icon(
                                          _imageBytes == null ? Icons.camera_alt : Icons.send_rounded,
                                          color: const Color(0xFF09090B),
                                          size: 28,
                                        ),
                                ),
                              ),
                              if (_imageBytes != null) ...[
                                const SizedBox(height: 6),
                                Text(
                                  l10n.t('locket_publish'),
                                  style: const TextStyle(
                                    color: Color(0xFFFBBF24),
                                    fontSize: 12,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                            ],
                          ),
                        ),
                        IconButton(
                          tooltip: 'Switch camera',
                          onPressed: _publishing
                              ? null
                              : () {
                                  setState(() {
                                    _cameraDevice = _cameraDevice == CameraDevice.rear
                                        ? CameraDevice.front
                                        : CameraDevice.rear;
                                  });
                                  if (_imageBytes == null) {
                                    _pickImage(ImageSource.camera);
                                  }
                                },
                          style: IconButton.styleFrom(
                            backgroundColor: Colors.white12,
                            fixedSize: const Size(48, 48),
                          ),
                          icon: const Icon(Icons.cameraswitch_outlined, color: Colors.white),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _DarkChipButton extends StatelessWidget {
  const _DarkChipButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ActionChip(
      avatar: Icon(icon, size: 15, color: Colors.white),
      label: Text(label, style: const TextStyle(color: Colors.white, fontSize: 11.5)),
      backgroundColor: Colors.white12,
      side: BorderSide.none,
      onPressed: onTap,
    );
  }
}

class _GridPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = Colors.white.withValues(alpha: 0.22)
      ..strokeWidth = 0.8;

    for (var i = 1; i <= 2; i++) {
      final dx = size.width * i / 3;
      final dy = size.height * i / 3;
      canvas.drawLine(Offset(dx, 0), Offset(dx, size.height), paint);
      canvas.drawLine(Offset(0, dy), Offset(size.width, dy), paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
