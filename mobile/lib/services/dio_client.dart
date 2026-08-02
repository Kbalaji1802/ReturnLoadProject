import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';
import 'auth_repository.dart';

/// The app's single, shared Dio HTTP client, configured with the API base URL
/// and sensible timeouts. Feature repositories depend on this provider instead
/// of constructing their own client, so cross-cutting concerns (auth headers,
/// retry/offline handling per OFFLINE_STRATEGY.md §5) are added in one place.
/// The API base URL with a guaranteed trailing slash, so relative paths (e.g. 'auth/login')
/// join correctly: '.../api/v1' + 'auth/login' would otherwise resolve wrong (404).
String get apiBaseUrl =>
    AppConfig.apiBaseUrl.endsWith('/') ? AppConfig.apiBaseUrl : '${AppConfig.apiBaseUrl}/';

final dioProvider = Provider<Dio>((ref) {
  final Dio dio = Dio(
    BaseOptions(
      baseUrl: apiBaseUrl,
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

      // Access tokens live 15 minutes. Without this, every screen starts failing a quarter of
      // an hour after sign-in and reports it as its own generic error ("Could not load your
      // trips"), which reads as a broken feature rather than an expired session. On a 401 we
      // rotate the token once and replay the original request.
      onError: (DioException error, ErrorInterceptorHandler handler) async {
        final RequestOptions request = error.requestOptions;
        final bool isAuthCall = request.path.contains('auth/login') ||
            request.path.contains('auth/register') ||
            request.path.contains('auth/refresh');

        // `retried` bounds this to a single attempt — a token the server keeps rejecting
        // must surface as a 401, not spin.
        if (error.response?.statusCode != 401 || isAuthCall || request.extra['retried'] == true) {
          return handler.next(error);
        }

        final bool refreshed = await ref.read(authRepositoryProvider).refreshSession();
        if (!refreshed) {
          // The refresh token is gone or rejected: the session is genuinely over. Clearing it
          // lets the router send the user to login instead of leaving them on a dead screen.
          await ref.read(authRepositoryProvider).logout();
          return handler.next(error);
        }

        request.extra['retried'] = true;
        request.headers['Authorization'] = 'Bearer ${ref.read(authTokenProvider)}';
        try {
          handler.resolve(await dio.fetch<dynamic>(request));
        } on DioException catch (retryError) {
          handler.next(retryError);
        }
      },
    ),
  );

  return dio;
});
