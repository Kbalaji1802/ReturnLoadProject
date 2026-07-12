import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../widgets/section_card.dart';

/// Foundation landing screen for the driver app. It renders the app frame and a
/// short status note; the map, authentication, and return-leg posting flows are
/// delivered by task T-030. Kept as a ConsumerWidget so it can read providers
/// (e.g. the Dio client / auth state) once those flows exist.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  static const String routeName = 'home';
  static const String routePath = '/';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('ReturnLoad')),
      body: const Padding(
        padding: EdgeInsets.all(16),
        child: SectionCard(
          title: 'Driver app foundation',
          child: Text(
            'Map, authentication, and return-leg flows arrive with task T-030. '
            'This scaffold wires Riverpod, GoRouter, Dio, and the app theme.',
          ),
        ),
      ),
    );
  }
}
