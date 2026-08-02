import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'dio_client.dart';

/// Holds the current access token in memory. The Dio interceptor and the router
/// both read this to authorise requests and gate navigation.
final authTokenProvider = StateProvider<String?>((ref) => null);

/// The refresh token for the current session. Access tokens live 15 minutes; without this
/// the app would simply start failing every request a quarter of an hour after sign-in.
final refreshTokenProvider = StateProvider<String?>((ref) => null);

final _secureStorageProvider = Provider<FlutterSecureStorage>(
  (ref) => const FlutterSecureStorage(),
);

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref),
);

/// The decoded JWT payload (null when signed out / malformed).
Map<String, dynamic>? _decodeJwt(String? token) {
  if (token == null) return null;
  try {
    final parts = token.split('.');
    return jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(parts[1])))) as Map<String, dynamic>;
  } catch (_) {
    return null;
  }
}

/// The signed-in user's email, decoded from the JWT payload (null when signed out).
final currentEmailProvider = Provider<String?>((ref) {
  final token = ref.watch(authTokenProvider);
  return _decodeJwt(token)?['email'] as String?;
});

/// The signed-in user's roles, decoded from the JWT `role` claim (string or array).
final currentRolesProvider = Provider<List<String>>((ref) {
  final claim = _decodeJwt(ref.watch(authTokenProvider))?['role'];
  if (claim == null) return const [];
  return claim is List ? claim.map((r) => r.toString()).toList() : [claim.toString()];
});

/// Whether the signed-in user is a Load Owner (the `Shipper` role). Drives which app the
/// shell shows — a Driver never sees the Post-Load / shipper surfaces (M4.2).
final isLoadOwnerProvider = Provider<bool>((ref) => ref.watch(currentRolesProvider).contains('Shipper'));

/// The two marketplace sides a person can register as (mirrors the API's AccountType enum).
enum AccountType { driver, loadOwner }

/// Authenticates the driver against the ReturnLoad API and persists the token
/// encrypted-at-rest on the device (OFFLINE_STRATEGY.md §3, 01_PROJECT_RULES.md §5).
class AuthRepository {
  AuthRepository(this._ref);

  final Ref _ref;
  static const _tokenKey = 'returnload.driver.token';
  static const _refreshKey = 'returnload.driver.refresh';

  /// De-duplicates concurrent refreshes. Several screens can 401 at once (the trips tab and
  /// the notification poll, say); without this each would rotate the refresh token in turn
  /// and all but one would be left holding a token the server has already invalidated.
  Future<bool>? _inFlightRefresh;

  Future<void> login(String email, String password) =>
      _authenticate('auth/login', {'email': email, 'password': password, 'deviceId': 'driver-mobile'});

  /// Self-service registration; the API returns tokens, so the user is signed in immediately.
  /// [accountType] chooses the marketplace side and the role granted at sign-up (M4.2).
  Future<void> register(String email, String password, String? phone, AccountType accountType) => _authenticate(
        'auth/register',
        {
          'email': email,
          'password': password,
          'phoneNumber': phone,
          'deviceId': 'driver-mobile',
          // The API's AccountType enum is serialised as an integer: Driver=0, LoadOwner=1.
          'accountType': accountType == AccountType.loadOwner ? 1 : 0,
        },
      );

  Future<void> _authenticate(String path, Map<String, dynamic> body) async {
    final Dio dio = _ref.read(dioProvider);
    final Response<dynamic> response = await dio.post<dynamic>(path, data: body);

    // dio usually returns a Map, but decode defensively in case a String slips through on web.
    final dynamic raw = response.data;
    final Map<String, dynamic> envelope =
        raw is String ? jsonDecode(raw) as Map<String, dynamic> : raw as Map<String, dynamic>;
    await _storeTokens(envelope['data'] as Map<String, dynamic>);
  }

  /// Persists a fresh token pair from an auth response body.
  Future<void> _storeTokens(Map<String, dynamic> data) async {
    final String token = data['accessToken'] as String;
    final String? refreshToken = data['refreshToken'] as String?;

    // Set the in-memory tokens first so auth always succeeds once they are issued;
    // persisting is best-effort (the web secure-storage backend can be flaky).
    _ref.read(authTokenProvider.notifier).state = token;
    _ref.read(refreshTokenProvider.notifier).state = refreshToken;
    try {
      final FlutterSecureStorage storage = _ref.read(_secureStorageProvider);
      await storage.write(key: _tokenKey, value: token);
      await storage.write(key: _refreshKey, value: refreshToken);
    } catch (_) {
      // Non-fatal: tokens stay in memory for this session.
    }
  }

  /// Exchanges the stored refresh token for a new pair. Returns false when there is nothing
  /// to refresh with or the server rejects it — the caller should then send the user to login.
  Future<bool> refreshSession() => _inFlightRefresh ??=
      _refreshSession().whenComplete(() => _inFlightRefresh = null);

  Future<bool> _refreshSession() async {
    final String? refreshToken = _ref.read(refreshTokenProvider);
    if (refreshToken == null || refreshToken.isEmpty) {
      return false;
    }

    try {
      // A bare client on purpose: the shared Dio's error interceptor is what calls this, so
      // reusing it would recurse if the refresh itself came back 401.
      final Dio client = Dio(BaseOptions(
        baseUrl: apiBaseUrl,
        headers: const {'Accept': 'application/json'},
      ));
      final Response<dynamic> response = await client.post<dynamic>(
        'auth/refresh',
        data: {'refreshToken': refreshToken, 'deviceId': 'driver-mobile'},
      );

      final dynamic raw = response.data;
      final Map<String, dynamic> envelope =
          raw is String ? jsonDecode(raw) as Map<String, dynamic> : raw as Map<String, dynamic>;
      final Map<String, dynamic>? data = envelope['data'] as Map<String, dynamic>?;
      if (data == null) {
        return false;
      }

      await _storeTokens(data);
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<void> restore() async {
    try {
      final FlutterSecureStorage storage = _ref.read(_secureStorageProvider);
      _ref.read(authTokenProvider.notifier).state = await storage.read(key: _tokenKey);
      _ref.read(refreshTokenProvider.notifier).state = await storage.read(key: _refreshKey);
    } catch (_) {
      // Ignore storage errors on restore.
    }
  }

  Future<void> logout() async {
    try {
      final FlutterSecureStorage storage = _ref.read(_secureStorageProvider);
      await storage.delete(key: _tokenKey);
      await storage.delete(key: _refreshKey);
    } catch (_) {
      // ignore
    }
    _ref.read(authTokenProvider.notifier).state = null;
    _ref.read(refreshTokenProvider.notifier).state = null;
  }
}
