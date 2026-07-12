/// Compile-time application configuration. Override per build/flavour with
/// `--dart-define`, e.g. `flutter run --dart-define=API_BASE_URL=https://...`.
/// Nothing sensitive lives here (01_PROJECT_RULES.md §1).
class AppConfig {
  const AppConfig._();

  /// Base URL of the ReturnLoad API. The default targets the API served from
  /// docker-compose as seen from the Android emulator (10.0.2.2 == host loopback).
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080/api/v1',
  );
}
