import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/app_providers.dart';
import 'routing/app_router.dart';
import 'shared/theme/app_theme.dart';

/// Root widget. Reads the router + theme mode from Riverpod and wires the app theme.
class ReturnLoadApp extends ConsumerWidget {
  const ReturnLoadApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);
    final mode = ref.watch(themeModeProvider);

    return MaterialApp.router(
      title: 'ReturnLoad Driver',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: mode,
      routerConfig: router,
    );
  }
}
