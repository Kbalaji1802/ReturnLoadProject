import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/app_providers.dart';
import '../../services/auth_repository.dart';
import '../home/home_tab.dart';
import '../home/load_owner_home_tab.dart';
import '../loads/loads_tab.dart';
import '../trips/trips_tab.dart';
import '../notifications/notifications_tab.dart';
import '../profile/profile_tab.dart';

/// The signed-in shell. Destinations are chosen by role (M4.2): a Driver gets the driver
/// surfaces (home, matched loads, trips); a Load Owner gets a shipper surface (post loads) and
/// never sees driver actions. Notifications + Profile are shared.
class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});
  static const routePath = '/home';
  static const routeName = 'home';

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends ConsumerState<HomeShell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final isLoadOwner = ref.watch(isLoadOwnerProvider);
    final tabs = isLoadOwner ? _loadOwnerTabs : _driverTabs;
    final destinations = isLoadOwner ? _loadOwnerDestinations(ref) : _driverDestinations(ref);
    final index = _index.clamp(0, tabs.length - 1);

    return Scaffold(
      body: IndexedStack(index: index, children: tabs),
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: destinations,
      ),
    );
  }

  static const _driverTabs = [HomeTab(), LoadsTab(), TripsTab(), NotificationsTab(), ProfileTab()];
  static const _loadOwnerTabs = [LoadOwnerHomeTab(), NotificationsTab(), ProfileTab()];

  List<NavigationDestination> _driverDestinations(WidgetRef ref) => [
        NavigationDestination(icon: const Icon(Icons.home_outlined), selectedIcon: const Icon(Icons.home), label: tr(ref, 'home')),
        NavigationDestination(icon: const Icon(Icons.inventory_2_outlined), selectedIcon: const Icon(Icons.inventory_2), label: tr(ref, 'loads')),
        NavigationDestination(icon: const Icon(Icons.local_shipping_outlined), selectedIcon: const Icon(Icons.local_shipping), label: tr(ref, 'trips')),
        NavigationDestination(icon: const Icon(Icons.notifications_outlined), selectedIcon: const Icon(Icons.notifications), label: tr(ref, 'notifications')),
        NavigationDestination(icon: const Icon(Icons.person_outline), selectedIcon: const Icon(Icons.person), label: tr(ref, 'profile')),
      ];

  List<NavigationDestination> _loadOwnerDestinations(WidgetRef ref) => [
        NavigationDestination(icon: const Icon(Icons.home_outlined), selectedIcon: const Icon(Icons.home), label: tr(ref, 'home')),
        NavigationDestination(icon: const Icon(Icons.notifications_outlined), selectedIcon: const Icon(Icons.notifications), label: tr(ref, 'notifications')),
        NavigationDestination(icon: const Icon(Icons.person_outline), selectedIcon: const Icon(Icons.person), label: tr(ref, 'profile')),
      ];
}
