import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';
import 'auth_repository.dart';

/// The app's single, shared Dio HTTP client, configured with the API base URL
/// and sensible timeouts. Feature repositories depend on this provider instead
/// of constructing their own client, so cross-cutting concerns (auth headers,
/// retry/offline handling per OFFLINE_STRATEGY.md §5) are added in one place.
final dioProvider = Provider<Dio>((ref) {
  final Dio dio = Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
      headers: const {'Accept': 'application/json'},
    ),
  );

  // Attach the bearer token (when present) to every request except login.
  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) {
        final String? token = ref.read(authTokenProvider);
        if (token != null && !options.path.contains('auth/login')) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
    ),
  );

  return dio;
});
