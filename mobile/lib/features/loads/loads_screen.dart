import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/dio_client.dart';

/// Fetches available (posted) loads from the API.
final availableLoadsProvider = FutureProvider.autoDispose<List<dynamic>>((ref) async {
  final response = await ref.read(dioProvider).get<dynamic>('loads/available');
  return response.data['data'] as List<dynamic>;
});

/// Lists loads a driver can accept.
class LoadsScreen extends ConsumerWidget {
  const LoadsScreen({super.key});

  static const routePath = '/loads';
  static const routeName = 'loads';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final loads = ref.watch(availableLoadsProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Available loads'), leading: BackButton(onPressed: () => context.go('/dashboard'))),
      body: loads.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => const Center(child: Text('Could not load. Is the API running?')),
        data: (items) => items.isEmpty
            ? const Center(child: Text('No loads posted yet.'))
            : ListView.builder(
                itemCount: items.length,
                itemBuilder: (context, i) {
                  final load = items[i] as Map<String, dynamic>;
                  return Card(
                    child: ListTile(
                      title: Text('${load['originAddress'] ?? '—'} → ${load['destinationAddress'] ?? '—'}'),
                      subtitle: Text('${load['cargoType']} · ${load['weightKg']} kg · ₹${load['offeredPriceInr'] ?? '—'}'),
                      trailing: FilledButton(
                        onPressed: () => _accept(ref, load['id'] as String, context),
                        child: const Text('Accept'),
                      ),
                    ),
                  );
                },
              ),
      ),
    );
  }

  Future<void> _accept(WidgetRef ref, String loadId, BuildContext context) async {
    await ref.read(dioProvider).post<dynamic>('loads/$loadId/accept');
    ref.invalidate(availableLoadsProvider);
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Load accepted.')));
    }
  }
}
