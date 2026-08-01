import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../services/location_service.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';

/// Ranked, compatible loads for the driver (GET /loads/matched, best-first). The driver can set
/// their current location so proximity drives the ranking (M5).
class LoadsTab extends ConsumerStatefulWidget {
  const LoadsTab({super.key});
  @override
  ConsumerState<LoadsTab> createState() => _LoadsTabState();
}

class _LoadsTabState extends ConsumerState<LoadsTab> {
  List<dynamic>? _loads;
  String? _error;
  double? _lat;
  double? _lng;
  String? _locationLabel;

  @override
  void initState() {
    super.initState();
    _init();
  }

  /// Best-effort: use the device's current location so proximity ranks automatically (M6).
  /// The driver can still override it via the location bar.
  Future<void> _init() async {
    try {
      final loc = await ref.read(locationServiceProvider).current();
      if (loc != null && mounted) {
        _lat = loc.latitude;
        _lng = loc.longitude;
        _locationLabel = 'Current location';
      }
    } catch (_) {/* fall back to no location / manual */}
    await _fetch();
  }

  Future<void> _fetch() async {
    setState(() => _error = null);
    try {
      final params = <String, dynamic>{};
      if (_lat != null && _lng != null) { params['lat'] = _lat; params['lng'] = _lng; }
      final res = await ref.read(dioProvider).get<dynamic>('loads/matched', queryParameters: params);
      if (mounted) setState(() => _loads = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load. Pull to retry.'; _loads = []; });
    }
  }

  Future<void> _setLocation() async {
    final controller = TextEditingController();
    final query = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (c) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(c).viewInsets.bottom),
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text('Your current location', style: Theme.of(c).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              autofocus: true,
              decoration: const InputDecoration(labelText: 'City / town (e.g. Madurai)', border: OutlineInputBorder()),
              onSubmitted: (v) => Navigator.pop(c, v.trim()),
            ),
            const SizedBox(height: 12),
            FilledButton(onPressed: () => Navigator.pop(c, controller.text.trim()), child: const Text('Rank by distance')),
          ]),
        ),
      ),
    );
    if (query == null || query.isEmpty) return;

    try {
      final res = await ref.read(dioProvider).get<dynamic>('geo/search', queryParameters: {'q': query});
      final list = res.data['data'] as List;
      if (list.isEmpty) {
        if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not find that place.')));
        return;
      }
      final place = list.first as Map<String, dynamic>;
      setState(() { _lat = (place['latitude'] as num).toDouble(); _lng = (place['longitude'] as num).toDouble(); _locationLabel = query; });
      await _fetch();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not set your location.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final list = _loads;
    return Scaffold(
      appBar: AppBar(title: const Text('Loads for you')),
      body: Column(
        children: [
          Material(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            child: InkWell(
              onTap: _setLocation,
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: Row(children: [
                  const Icon(Icons.my_location, size: 18, color: AppColors.primary),
                  const SizedBox(width: 8),
                  Expanded(child: Text(
                    _locationLabel == null ? 'Set your location to rank by distance' : 'Ranking near: $_locationLabel',
                    style: Theme.of(context).textTheme.bodyMedium,
                  )),
                  const Icon(Icons.edit_location_alt, size: 18),
                ]),
              ),
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _fetch,
              child: list == null
                  ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
                  : list.isEmpty
                      ? _empty()
                      : ListView.separated(
                          padding: const EdgeInsets.all(16),
                          itemCount: list.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 14),
                          itemBuilder: (context, i) => _LoadCard(load: list[i] as Map<String, dynamic>),
                        ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 120),
        Icon(Icons.inbox, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No matching loads right now', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Pull down to refresh')),
      ]);
}

class _LoadCard extends ConsumerWidget {
  const _LoadCard({required this.load});
  final Map<String, dynamic> load;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    final stars = (load['stars'] as int?) ?? 0;
    final score = (load['score'] as int?) ?? 0;
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        onTap: () => context.push('/load', extra: load),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Match score header (M5).
              Row(children: [
                for (int i = 0; i < 5; i++)
                  Icon(i < stars ? Icons.star : Icons.star_border, size: 18, color: AppColors.warning),
                const SizedBox(width: 8),
                Text('$score pts', style: text.labelLarge?.copyWith(fontWeight: FontWeight.w700, color: AppColors.primary)),
              ]),
              const SizedBox(height: 10),
              Row(children: [
                const Icon(Icons.trip_origin, size: 16, color: AppColors.primary),
                const SizedBox(width: 6),
                Expanded(child: Text(load['originAddress'] ?? '—', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w600), maxLines: 1, overflow: TextOverflow.ellipsis)),
              ]),
              Padding(padding: const EdgeInsets.only(left: 7), child: Container(width: 2, height: 16, color: Theme.of(context).colorScheme.outlineVariant)),
              Row(children: [
                const Icon(Icons.place, size: 16, color: AppColors.error),
                const SizedBox(width: 6),
                Expanded(child: Text(load['destinationAddress'] ?? '—', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w600), maxLines: 1, overflow: TextOverflow.ellipsis)),
              ]),
              const Divider(height: 24),
              Wrap(spacing: 8, runSpacing: 8, children: [
                _chip(Icons.category, labelOf(cargoType, load['cargoType'])),
                _chip(Icons.scale, '${load['weightKg']} kg'),
                if (load['distanceKm'] != null) _chip(Icons.route, '${load['distanceKm']} km'),
                if (load['offeredPriceInr'] != null) _chip(Icons.payments, '₹${load['offeredPriceInr']}'),
              ]),
              if (load['reason'] != null) ...[
                const SizedBox(height: 10),
                Text(load['reason'], style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
              ],
              const SizedBox(height: 14),
              FilledButton.tonal(
                onPressed: () => context.push('/load', extra: load),
                child: const Text('View & request'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _chip(IconData i, String s) => Chip(avatar: Icon(i, size: 16), label: Text(s), visualDensity: VisualDensity.compact);
}
