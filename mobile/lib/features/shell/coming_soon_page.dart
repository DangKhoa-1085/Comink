import 'package:flutter/material.dart';

import '../../core/i18n/app_localizations.dart';
import '../../core/widgets/states.dart';

/// Trang tạm cho các tab chưa hoàn thiện trong lúc làm GĐ 6.
class ComingSoonPage extends StatelessWidget {
  const ComingSoonPage({super.key, required this.title, required this.icon});

  final String title;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: EmptyState(
        message: l10n.t('common_coming_soon'),
        icon: icon,
      ),
    );
  }
}
