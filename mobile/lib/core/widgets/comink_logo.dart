import 'dart:math' as math;
import 'package:flutter/material.dart';

/// Biểu tượng thương hiệu Comink (Phương án A — Chat-Link C):
/// chữ C bong bóng chat trắng + chấm trực tuyến xanh ngọc trên nền Xanh nước biển.
class CominkLogo extends StatelessWidget {
  const CominkLogo({super.key, this.size = 56});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: _CominkLogoPainter()),
    );
  }
}

class _CominkLogoPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final scale = math.min(w, h) / 100.0;

    final rect = Rect.fromLTWH(2 * scale, 2 * scale, 96 * scale, 96 * scale);
    final rrect = RRect.fromRectAndRadius(rect, Radius.circular(24 * scale));

    final bgPaint = Paint()
      ..shader = const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        stops: [0.0, 0.52, 1.0],
        colors: [
          Color(0xFF0369A1),
          Color(0xFF0284C7),
          Color(0xFF06B6D4),
        ],
      ).createShader(rect);

    canvas.drawRRect(rrect, bgPaint);

    final cPaint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.stroke
      ..strokeWidth = 11.5 * scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    final arcRect = Rect.fromCenter(
      center: Offset(49.5 * scale, 51.5 * scale),
      width: 55 * scale,
      height: 56 * scale,
    );

    final path = Path()
      ..addArc(arcRect, -0.66, -3.14)
      ..lineTo(23.5 * scale, 78.0 * scale)
      ..lineTo(36.5 * scale, 76.2 * scale)
      ..arcTo(arcRect, 2.04, -1.38, false);

    canvas.drawPath(path, cPaint);

    final dotPaint = Paint()..color = const Color(0xFF34D399);
    canvas.drawCircle(Offset(66 * scale, 51 * scale), 8.2 * scale, dotPaint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
