import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/app_providers.dart';
import '../home/home_tab.dart';
import '../loads/loads_tab.dart';
import '../trips/trips_tab.dart';
import '../notifications/notifications_tab.dart';
import '../profile/profile_tab.dart';

/// The signed-in shell: five primary destinations via a Material 3 NavigationBar.
class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});
  static const routePath = '/home';
  static const routeName = 'home';

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends ConsumerState<HomeShell> {
  int _index = 0;

  static const _tabs = [HomeTab(), LoadsTab(), TripsTab(), NotificationsTab(), ProfileTab()];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(index: _index, children: _tabs),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: [
          NavigationDestination(icon: const Icon(Icons.home_outlined), selectedIcon: const Icon(Icons.home), label: tr(ref, 'home')),
          NavigationDestination(icon: const Icon(Icons.inventory_2_outlined), selectedIcon: const Icon(Icons.inventory_2), label: tr(ref, 'loads')),
          NavigationDestination(icon: const Icon(Icons.local_shipping_outlined), selectedIcon: const Icon(Icons.local_shipping), label: tr(ref, 'trips')),
          NavigationDestination(icon: const Icon(Icons.notifications_outlined), selectedIcon: const Icon(Icons.notifications), label: tr(ref, 'notifications')),
          NavigationDestination(icon: const Icon(Icons.person_outline), selectedIcon: const Icon(Icons.person), label: tr(ref, 'profile')),
        ],
      ),
    );
  }
}
