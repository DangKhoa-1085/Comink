import 'package:flutter/material.dart';

import '../../core/config/app_config.dart';
import '../../core/i18n/app_localizations.dart';
import '../../core/widgets/comink_logo.dart';

/// Màn hình chờ khôi phục phiên khi mở app.
class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;

    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CominkLogo(size: 72),
            const SizedBox(height: 14),
            Text(
              AppConfig.appName,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: colors.primary,
                  ),
            ),
            const SizedBox(height: 6),
            Text(
              l10n.t('app_tagline'),
              style: TextStyle(color: colors.onSurfaceVariant),
            ),
            const SizedBox(height: 28),
            const SizedBox(
              height: 24,
              width: 24,
              child: CircularProgressIndicator(strokeWidth: 2.5),
            ),
          ],
        ),
      ),
    );
  }
}
