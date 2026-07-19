import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';
import '../../shared/widgets/status_pill.dart';

/// The Load Owner's posted loads (GET /loads/mine), grouped by status. Tapping an open load
/// opens its booking requests so the owner can choose a driver (M4.4 Part 2).
class MyLoadsScreen extends ConsumerStatefulWidget {
  const MyLoadsScreen({super.key});
  static const routePath = '/my-loads';
  static const routeName = 'my-loads';

  @override
  ConsumerState<MyLoadsScreen> createState() => _MyLoadsScreenState();
}

class _MyLoadsScreenState extends ConsumerState<MyLoadsScreen> {
  List<dynamic>? _loads;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('loads/mine');
      if (mounted) setState(() => _loads = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load your loads.'; _loads = []; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final list = _loads;
    return Scaffold(
      appBar: AppBar(title: const Text('My loads')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: list == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : list.isEmpty
                ? _empty()
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: list.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (context, i) => _card(list[i] as Map<String, dynamic>),
                  ),
      ),
    );
  }

  Widget _card(Map<String, dynamic> load) {
    final text = Theme.of(context).textTheme;
    final isOpen = load['status'] == 1; // Posted/Open
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        onTap: isOpen ? () => context.push('/load-requests', extra: load) : null,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(children: [
                Expanded(child: Text('${load['originAddress'] ?? '—'} → ${load['destinationAddress'] ?? '—'}',
                    style: text.titleSmall?.copyWith(fontWeight: FontWeight.w600), maxLines: 2, overflow: TextOverflow.ellipsis)),
                const SizedBox(width: 8),
                StatusPill(labelOf(loadStatus, load['status'])),
              ]),
              const SizedBox(height: 8),
              Wrap(spacing: 8, runSpacing: 8, children: [
                _chip(Icons.category, labelOf(cargoType, load['cargoType'])),
                _chip(Icons.scale, '${load['weightKg']} kg'),
                if (load['distanceKm'] != null) _chip(Icons.route, '${load['distanceKm']} km'),
              ]),
              if (isOpen) ...[
                const SizedBox(height: 12),
                Row(children: [
                  const Icon(Icons.people_outline, size: 18, color: AppColors.primary),
                  const SizedBox(width: 6),
                  Text('View driver requests', style: text.bodyMedium?.copyWith(color: AppColors.primary, fontWeight: FontWeight.w600)),
                  const Spacer(),
                  const Icon(Icons.chevron_right, color: AppColors.primary),
                ]),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _chip(IconData i, String s) => Chip(avatar: Icon(i, size: 16), label: Text(s), visualDensity: VisualDensity.compact);

  Widget _empty() => ListView(children: [
        const SizedBox(height: 100),
        Icon(Icons.inventory_2_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No loads yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Post a load to get started')),
      ]);
}
