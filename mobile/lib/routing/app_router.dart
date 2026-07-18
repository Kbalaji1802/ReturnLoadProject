import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/login_screen.dart';
import '../features/dashboard/dashboard_screen.dart';
import '../features/loads/loads_screen.dart';
import '../features/tracking/tracking_screen.dart';
import '../services/auth_repository.dart';

/// Central GoRouter configuration. Unauthenticated users are redirected to /login;
/// authenticated ones land on the dashboard.
final appRouterProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: DashboardScreen.routePath,
    redirect: (context, state) {
      final bool loggedIn = ref.read(authTokenProvider) != null;
      final bool loggingIn = state.matchedLocation == LoginScreen.routePath;
      if (!loggedIn) {
        return loggingIn ? null : LoginScreen.routePath;
      }
      if (loggingIn) {
        return DashboardScreen.routePath;
      }
      return null;
    },
    routes: [
      GoRoute(path: LoginScreen.routePath, name: LoginScreen.routeName, builder: (context, state) => const LoginScreen()),
      GoRoute(path: DashboardScreen.routePath, name: DashboardScreen.routeName, builder: (context, state) => const DashboardScreen()),
      GoRoute(path: LoadsScreen.routePath, name: LoadsScreen.routeName, builder: (context, state) => const LoadsScreen()),
      GoRoute(path: TrackingScreen.routePath, name: TrackingScreen.routeName, builder: (context, state) => const TrackingScreen()),
    ],
  );
});
