import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';

/// Return-load intelligence (M9): when the driver says they'll return empty, we rank loads from
/// their destination so they can line up a backhaul before unloading — the core deadhead fix.
class ReturnLoadsScreen extends ConsumerStatefulWidget {
  const ReturnLoadsScreen({super.key, required this.lat, required this.lng, this.place});
  static const routePath = '/return-loads';
  static const routeName = 'return-loads';

  final double lat;
  final double lng;
  final String? place;

  @override
  ConsumerState<ReturnLoadsScreen> createState() => _State();
}

class _State extends ConsumerState<ReturnLoadsScreen> {
  List<dynamic>? _loads;
  String? _error;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('loads/matched', queryParameters: {'lat': widget.lat, 'lng': widget.lng});
      if (mounted) setState(() => _loads = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load return loads.'; _loads = []; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final list = _loads;
    return Scaffold(
      appBar: AppBar(title: const Text('Return loads')),
      body: RefreshIndicator(
        onRefresh: _fetch,
        child: list == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : list.isEmpty
                ? _empty()
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      Card(
                        color: AppColors.success.withValues(alpha: 0.10),
                        child: Padding(
                          padding: const EdgeInsets.all(14),
                          child: Row(children: [
                            const Icon(Icons.autorenew, color: AppColors.success),
                            const SizedBox(width: 10),
                            Expanded(child: Text('Loads near ${widget.place ?? 'your destination'} — grab one before you unload.')),
                          ]),
                        ),
                      ),
                      const SizedBox(height: 12),
                      for (final l in list.cast<Map<String, dynamic>>()) _card(l),
                    ],
                  ),
      ),
    );
  }

  Widget _card(Map<String, dynamic> load) {
    final text = Theme.of(context).textTheme;
    final stars = (load['stars'] as int?) ?? 0;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        onTap: () => context.push('/load', extra: load),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              for (int i = 0; i < 5; i++) Icon(i < stars ? Icons.star : Icons.star_border, size: 16, color: AppColors.warning),
              const SizedBox(width: 8),
              Text('${load['score'] ?? 0} pts', style: text.labelLarge?.copyWith(fontWeight: FontWeight.w700, color: AppColors.primary)),
            ]),
            const SizedBox(height: 8),
            Text('${load['originAddress'] ?? '—'} → ${load['destinationAddress'] ?? '—'}',
                style: text.titleSmall?.copyWith(fontWeight: FontWeight.w600), maxLines: 2, overflow: TextOverflow.ellipsis),
            const SizedBox(height: 8),
            Wrap(spacing: 8, runSpacing: 8, children: [
              Chip(avatar: const Icon(Icons.category, size: 16), label: Text(labelOf(cargoType, load['cargoType'])), visualDensity: VisualDensity.compact),
              Chip(avatar: const Icon(Icons.scale, size: 16), label: Text('${load['weightKg']} kg'), visualDensity: VisualDensity.compact),
              if (load['distanceKm'] != null) Chip(avatar: const Icon(Icons.route, size: 16), label: Text('${load['distanceKm']} km'), visualDensity: VisualDensity.compact),
            ]),
            const SizedBox(height: 12),
            FilledButton.tonal(onPressed: () => context.push('/load', extra: load), child: const Text('View & request')),
          ]),
        ),
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 120),
        Icon(Icons.inbox, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No return loads near your destination yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Pull to refresh — new loads appear here')),
      ]);
}
