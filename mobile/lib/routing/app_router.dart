import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/login_screen.dart';
import '../features/auth/register_screen.dart';
import '../features/documents/documents_screen.dart';
import '../features/driver/driver_profile_screen.dart';
import '../features/loads/load_details_screen.dart';
import '../features/loads/post_load_screen.dart';
import '../features/map/map_screen.dart';
import '../features/settings/settings_screen.dart';
import '../features/shell/home_shell.dart';
import '../features/splash/splash_screen.dart';
import '../features/vehicle/vehicle_screen.dart';
import '../services/auth_repository.dart';

/// Central GoRouter. Splash + auth are public; the app requires a token otherwise.
final appRouterProvider = Provider<GoRouter>((ref) {
  const publicPaths = {SplashScreen.routePath, LoginScreen.routePath, RegisterScreen.routePath};

  return GoRouter(
    initialLocation: SplashScreen.routePath,
    redirect: (context, state) {
      final bool loggedIn = ref.read(authTokenProvider) != null;
      final bool onPublic = publicPaths.contains(state.matchedLocation);
      // The splash screen handles its own routing decision.
      if (state.matchedLocation == SplashScreen.routePath) return null;
      if (!loggedIn) return onPublic ? null : LoginScreen.routePath;
      if (state.matchedLocation == LoginScreen.routePath || state.matchedLocation == RegisterScreen.routePath) {
        return HomeShell.routePath;
      }
      return null;
    },
    routes: [
      GoRoute(path: SplashScreen.routePath, name: SplashScreen.routeName, builder: (c, s) => const SplashScreen()),
      GoRoute(path: LoginScreen.routePath, name: LoginScreen.routeName, builder: (c, s) => const LoginScreen()),
      GoRoute(path: RegisterScreen.routePath, name: RegisterScreen.routeName, builder: (c, s) => const RegisterScreen()),
      GoRoute(path: HomeShell.routePath, name: HomeShell.routeName, builder: (c, s) => const HomeShell()),
      GoRoute(
        path: LoadDetailsScreen.routePath,
        name: LoadDetailsScreen.routeName,
        builder: (c, s) => LoadDetailsScreen(load: (s.extra as Map<String, dynamic>?) ?? const {}),
      ),
      GoRoute(path: PostLoadScreen.routePath, name: PostLoadScreen.routeName, builder: (c, s) => const PostLoadScreen()),
      GoRoute(path: DriverProfileScreen.routePath, name: DriverProfileScreen.routeName, builder: (c, s) => const DriverProfileScreen()),
      GoRoute(path: VehicleScreen.routePath, name: VehicleScreen.routeName, builder: (c, s) => const VehicleScreen()),
      GoRoute(path: DocumentsScreen.routePath, name: DocumentsScreen.routeName, builder: (c, s) => const DocumentsScreen()),
      GoRoute(path: MapScreen.routePath, name: MapScreen.routeName, builder: (c, s) => const MapScreen()),
      GoRoute(path: SettingsScreen.routePath, name: SettingsScreen.routeName, builder: (c, s) => const SettingsScreen()),
    ],
  );
});
