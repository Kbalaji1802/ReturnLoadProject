import 'package:flutter/material.dart';

import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/status_pill.dart';

/// Vehicle card. A per-driver "my vehicle" endpoint doesn't exist yet, so this shows the
/// card layout with a clear note; values populate once a carrier links a vehicle.
class VehicleScreen extends StatelessWidget {
  const VehicleScreen({super.key});
  static const routePath = '/vehicle';
  static const routeName = 'vehicle';

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('My vehicle')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  Container(padding: const EdgeInsets.all(12), decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(14)), child: const Icon(Icons.local_shipping, color: AppColors.primary, size: 28)),
                  const SizedBox(width: 14),
                  Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text('Not linked yet', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                    Text('Registration pending', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                  ])),
                  const StatusPill('Draft'),
                ]),
                const Divider(height: 28),
                _row(context, 'Registration', '—'),
                _row(context, 'Capacity', '—'),
                _row(context, 'Insurance', 'Not verified'),
                _row(context, 'Fitness', 'Not verified'),
                _row(context, 'Permit', 'Not verified'),
              ]),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            color: AppColors.warning.withValues(alpha: 0.10),
            child: const Padding(
              padding: EdgeInsets.all(16),
              child: Row(children: [
                Icon(Icons.info_outline, color: AppColors.warning),
                SizedBox(width: 12),
                Expanded(child: Text('Your vehicle is registered and verified by your carrier. Details appear here once linked.')),
              ]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _row(BuildContext c, String k, String v) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          Text(k, style: TextStyle(color: Theme.of(c).colorScheme.onSurfaceVariant)),
          Text(v, style: const TextStyle(fontWeight: FontWeight.w600)),
        ]),
      );
}
