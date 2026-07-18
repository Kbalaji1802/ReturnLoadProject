import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/route_timeline.dart';
import '../../shared/widgets/status_pill.dart';

/// Current trip view. There is no "my trips" list endpoint yet, so a trip is loaded by id
/// (GET /trips/{id}); its lifecycle can be advanced from here.
class TripsTab extends ConsumerStatefulWidget {
  const TripsTab({super.key});
  @override
  ConsumerState<TripsTab> createState() => _TripsTabState();
}

class _TripsTabState extends ConsumerState<TripsTab> {
  final _id = TextEditingController();
  Map<String, dynamic>? _trip;
  bool _busy = false;

  @override
  void dispose() {
    _id.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _busy = true);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('trips/${_id.text.trim()}');
      setState(() => _trip = res.data['data'] as Map<String, dynamic>);
    } catch (_) {
      setState(() => _trip = null);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Trip not found.')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _advance(String target) async {
    setState(() => _busy = true);
    try {
      await ref.read(dioProvider).post<dynamic>('trips/${_id.text.trim()}/status/$target');
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Transition not allowed right now.')));
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = _trip;
    return Scaffold(
      appBar: AppBar(title: const Text('Trips')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(children: [
            Expanded(child: TextField(controller: _id, decoration: const InputDecoration(labelText: 'Trip id', prefixIcon: Icon(Icons.tag)))),
            const SizedBox(width: 10),
            FilledButton(onPressed: _busy ? null : _load, style: FilledButton.styleFrom(minimumSize: const Size(72, 56)), child: const Icon(Icons.search)),
          ]),
          const SizedBox(height: 16),
          if (_busy) const LinearProgressIndicator(),
          if (t == null && !_busy)
            _empty()
          else if (t != null) ...[
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      Text('Current trip', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                      const Spacer(),
                      StatusPill(labelOf(tripStatus, t['status'])),
                    ]),
                    const SizedBox(height: 16),
                    RouteTimeline([
                      TimelineStop('Started', t['startedAtUtc'] ?? 'Not started', done: t['startedAtUtc'] != null),
                      TimelineStop('Completed', t['completedAtUtc'] ?? 'In progress', done: t['completedAtUtc'] != null),
                    ]),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            Row(children: [
              Expanded(child: OutlinedButton(onPressed: _busy ? null : () => _advance('Started'), child: const Text('Start'))),
              const SizedBox(width: 10),
              Expanded(child: FilledButton(onPressed: _busy ? null : () => _advance('Completed'), child: const Text('Complete trip'))),
            ]),
            const SizedBox(height: 10),
            TextButton(onPressed: _busy ? null : () => _advance('Cancelled'), child: const Text('Cancel trip', style: TextStyle(color: AppColors.error))),
          ],
        ],
      ),
    );
  }

  Widget _empty() => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Column(children: [
          Icon(Icons.local_shipping_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
          const SizedBox(height: 12),
          const Text('No active trip'),
          const SizedBox(height: 4),
          Text('Enter a trip id above to view and update it', textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodySmall),
        ]),
      );
}
