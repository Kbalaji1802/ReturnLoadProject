import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';

/// The app's single, shared Dio HTTP client, configured with the API base URL
/// and sensible timeouts. Feature repositories depend on this provider instead
/// of constructing their own client, so cross-cutting concerns (auth headers,
/// retry/offline handling per OFFLINE_STRATEGY.md §5) are added in one place.
final dioProvider = Provider<Dio>((ref) {
  return Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
      headers: const {'Accept': 'application/json'},
    ),
  );
});
