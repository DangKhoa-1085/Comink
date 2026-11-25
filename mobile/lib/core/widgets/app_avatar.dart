import 'package:flutter/material.dart';

/// Avatar dùng chung: ảnh nếu có URL, ngược lại hiển thị chữ cái đầu.
class AppAvatar extends StatelessWidget {
  const AppAvatar({
    super.key,
    required this.name,
    this.url,
    this.radius = 20,
  });

  final String name;
  final String? url;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final trimmedUrl = url?.trim();
    if (trimmedUrl != null && trimmedUrl.isNotEmpty) {
      return CircleAvatar(
        radius: radius,
        backgroundColor: Theme.of(context).colorScheme.surfaceContainerHighest,
        backgroundImage: NetworkImage(trimmedUrl),
      );
    }

    return CircleAvatar(
      radius: radius,
      backgroundColor: _colorFor(name),
      child: Text(
        _initials(name),
        style: TextStyle(
          color: Colors.white,
          fontSize: radius * 0.72,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }

  static String _initials(String value) {
    final parts = value.trim().split(RegExp(r'\s+')).where((part) => part.isNotEmpty);
    if (parts.isEmpty) {
      return '?';
    }
    final letters = parts.take(2).map((part) => part.substring(0, 1)).join();
    return letters.toUpperCase();
  }

  static Color _colorFor(String value) {
    final palette = [
      const Color(0xFF4F46E5),
      const Color(0xFF0EA5E9),
      const Color(0xFF10B981),
      const Color(0xFFF59E0B),
      const Color(0xFFEC4899),
      const Color(0xFF8B5CF6),
    ];
    final code = value.codeUnits.fold<int>(0, (sum, unit) => sum + unit);
    return palette[code % palette.length];
  }
}
