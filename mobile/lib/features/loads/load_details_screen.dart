import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/route_timeline.dart';

class LoadDetailsScreen extends ConsumerWidget {
  const LoadDetailsScreen({super.key, required this.load});
  static const routePath = '/load';
  static const routeName = 'load-details';

  final Map<String, dynamic> load;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    final price = load['offeredPriceInr'];
    return Scaffold(
      appBar: AppBar(title: const Text('Load details')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: RouteTimeline([
                TimelineStop('Pickup', load['originAddress'] ?? '—'),
                TimelineStop('Drop', load['destinationAddress'] ?? '—'),
              ]),
            ),
          ),
          const SizedBox(height: 14),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                children: [
                  _row(context, Icons.category, 'Cargo', labelOf(cargoType, load['cargoType'])),
                  _row(context, Icons.scale, 'Weight', '${load['weightKg']} kg'),
                  _row(context, Icons.local_shipping, 'Vehicle requirement', 'Compatible with cargo'),
                  _row(context, Icons.payments, 'Estimated earnings', price != null ? '₹$price' : 'Negotiable'),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),
          FilledButton(onPressed: () => _accept(context, ref), child: const Text('Accept load')),
          const SizedBox(height: 10),
          OutlinedButton(onPressed: () => context.pop(), child: const Text('Reject')),
        ],
      ),
    );
  }

  Widget _row(BuildContext c, IconData icon, String k, String v) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(children: [
          Icon(icon, size: 20, color: AppColors.primary),
          const SizedBox(width: 12),
          Text(k, style: Theme.of(c).textTheme.bodyMedium?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant)),
          const Spacer(),
          Text(v, style: Theme.of(c).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w600)),
        ]),
      );

  Future<void> _accept(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(dioProvider).post<dynamic>('loads/${load['id']}/accept');
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Load accepted.')));
        context.pop();
      }
    } catch (_) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not accept the load.')));
    }
  }
}
