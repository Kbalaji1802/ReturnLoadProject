import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';

class LoadsTab extends ConsumerStatefulWidget {
  const LoadsTab({super.key});
  @override
  ConsumerState<LoadsTab> createState() => _LoadsTabState();
}

class _LoadsTabState extends ConsumerState<LoadsTab> {
  List<dynamic>? _loads;
  String? _error;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    setState(() { _error = null; });
    try {
      final res = await ref.read(dioProvider).get<dynamic>('loads/available');
      if (mounted) setState(() => _loads = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load. Pull to retry.'; _loads = []; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Available loads')),
      body: RefreshIndicator(
        onRefresh: _fetch,
        child: _loads == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : _loads!.isEmpty
                ? _empty()
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _loads!.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 14),
                    itemBuilder: (context, i) => _LoadCard(load: _loads![i] as Map<String, dynamic>, onAccepted: _fetch),
                  ),
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 120),
        Icon(Icons.inbox, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No loads available right now', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Pull down to refresh')),
      ]);
}

class _LoadCard extends ConsumerWidget {
  const _LoadCard({required this.load, required this.onAccepted});
  final Map<String, dynamic> load;
  final Future<void> Function() onAccepted;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        onTap: () => context.push('/load', extra: load),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(children: [
                const Icon(Icons.trip_origin, size: 16, color: AppColors.primary),
                const SizedBox(width: 6),
                Expanded(child: Text(load['originAddress'] ?? '—', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w600))),
              ]),
              Padding(padding: const EdgeInsets.only(left: 7), child: Container(width: 2, height: 16, color: Theme.of(context).colorScheme.outlineVariant)),
              Row(children: [
                const Icon(Icons.place, size: 16, color: AppColors.error),
                const SizedBox(width: 6),
                Expanded(child: Text(load['destinationAddress'] ?? '—', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w600))),
              ]),
              const Divider(height: 24),
              Wrap(spacing: 8, runSpacing: 8, children: [
                _chip(Icons.category, labelOf(cargoType, load['cargoType'])),
                _chip(Icons.scale, '${load['weightKg']} kg'),
                if (load['offeredPriceInr'] != null) _chip(Icons.payments, '₹${load['offeredPriceInr']}'),
              ]),
              const SizedBox(height: 14),
              Row(children: [
                Expanded(child: FilledButton(onPressed: () => _accept(context, ref), child: const Text('Accept'))),
                const SizedBox(width: 10),
                OutlinedButton(
                  onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Shipper contact is shared after booking.'))),
                  style: OutlinedButton.styleFrom(minimumSize: const Size(54, 54)),
                  child: const Icon(Icons.call),
                ),
              ]),
            ],
          ),
        ),
      ),
    );
  }

  Widget _chip(IconData i, String s) => Chip(avatar: Icon(i, size: 16), label: Text(s), visualDensity: VisualDensity.compact);

  Future<void> _accept(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(dioProvider).post<dynamic>('loads/${load['id']}/accept');
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Load accepted.')));
      await onAccepted();
    } catch (_) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not accept the load.')));
    }
  }
}
