import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'dio_client.dart';

/// Holds the current access token in memory. The Dio interceptor and the router
/// both read this to authorise requests and gate navigation.
final authTokenProvider = StateProvider<String?>((ref) => null);

final _secureStorageProvider = Provider<FlutterSecureStorage>(
  (ref) => const FlutterSecureStorage(),
);

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref),
);

/// The signed-in user's email, decoded from the JWT payload (null when signed out).
final currentEmailProvider = Provider<String?>((ref) {
  final token = ref.watch(authTokenProvider);
  if (token == null) return null;
  try {
    final parts = token.split('.');
    final payload = jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))));
    return (payload as Map<String, dynamic>)['email'] as String?;
  } catch (_) {
    return null;
  }
});

/// Authenticates the driver against the ReturnLoad API and persists the token
/// encrypted-at-rest on the device (OFFLINE_STRATEGY.md §3, 01_PROJECT_RULES.md §5).
class AuthRepository {
  AuthRepository(this._ref);

  final Ref _ref;
  static const _tokenKey = 'returnload.driver.token';

  Future<void> login(String email, String password) =>
      _authenticate('auth/login', {'email': email, 'password': password, 'deviceId': 'driver-mobile'});

  /// Self-service registration; the API returns tokens, so the user is signed in immediately.
  Future<void> register(String email, String password, String? phone) => _authenticate(
        'auth/register',
        {'email': email, 'password': password, 'phoneNumber': phone, 'deviceId': 'driver-mobile'},
      );

  Future<void> _authenticate(String path, Map<String, dynamic> body) async {
    final Dio dio = _ref.read(dioProvider);
    final Response<dynamic> response = await dio.post<dynamic>(path, data: body);

    // dio usually returns a Map, but decode defensively in case a String slips through on web.
    final dynamic raw = response.data;
    final Map<String, dynamic> envelope =
        raw is String ? jsonDecode(raw) as Map<String, dynamic> : raw as Map<String, dynamic>;
    final String token = (envelope['data'] as Map<String, dynamic>)['accessToken'] as String;

    // Set the in-memory token first so auth always succeeds once the token is issued;
    // persisting it is best-effort (the web secure-storage backend can be flaky).
    _ref.read(authTokenProvider.notifier).state = token;
    try {
      await _ref.read(_secureStorageProvider).write(key: _tokenKey, value: token);
    } catch (_) {
      // Non-fatal: token stays in memory for this session.
    }
  }

  Future<void> restore() async {
    try {
      final String? token = await _ref.read(_secureStorageProvider).read(key: _tokenKey);
      _ref.read(authTokenProvider.notifier).state = token;
    } catch (_) {
      // Ignore storage errors on restore.
    }
  }

  Future<void> logout() async {
    try {
      await _ref.read(_secureStorageProvider).delete(key: _tokenKey);
    } catch (_) {
      // ignore
    }
    _ref.read(authTokenProvider.notifier).state = null;
  }
}
