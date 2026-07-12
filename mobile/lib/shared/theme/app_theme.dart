import 'package:flutter/material.dart';

/// Central Material 3 theme for the driver app, seeded from the ReturnLoad brand
/// colour. Keeping theming in one place stops per-screen style drift.
class AppTheme {
  const AppTheme._();

  static const Color _seed = Color(0xFF0061A4);

  static ThemeData light() {
    return ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(seedColor: _seed),
    );
  }
}
