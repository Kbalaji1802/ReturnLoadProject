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

/// Authenticates the driver against the ReturnLoad API and persists the token
/// encrypted-at-rest on the device (OFFLINE_STRATEGY.md §3, 01_PROJECT_RULES.md §5).
class AuthRepository {
  AuthRepository(this._ref);

  final Ref _ref;
  static const _tokenKey = 'returnload.driver.token';

  Future<void> login(String email, String password) async {
    final Dio dio = _ref.read(dioProvider);
    final Response<dynamic> response = await dio.post<dynamic>(
      'auth/login',
      data: {'email': email, 'password': password, 'deviceId': 'driver-mobile'},
    );

    final String token = response.data['data']['accessToken'] as String;
    await _ref.read(_secureStorageProvider).write(key: _tokenKey, value: token);
    _ref.read(authTokenProvider.notifier).state = token;
  }

  Future<void> restore() async {
    final String? token = await _ref.read(_secureStorageProvider).read(key: _tokenKey);
    _ref.read(authTokenProvider.notifier).state = token;
  }

  Future<void> logout() async {
    await _ref.read(_secureStorageProvider).delete(key: _tokenKey);
    _ref.read(authTokenProvider.notifier).state = null;
  }
}
