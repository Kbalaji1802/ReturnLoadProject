import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';
import '../../shared/widgets/status_pill.dart';

/// The booking requests on one of the owner's loads (GET /bookings/for-load/{id}). The owner
/// approves one driver (creates the trip, assigns the load, auto-rejects the rest) or rejects.
class LoadRequestsScreen extends ConsumerStatefulWidget {
  const LoadRequestsScreen({super.key, required this.load});
  static const routePath = '/load-requests';
  static const routeName = 'load-requests';

  final Map<String, dynamic> load;

  @override
  ConsumerState<LoadRequestsScreen> createState() => _LoadRequestsScreenState();
}

class _LoadRequestsScreenState extends ConsumerState<LoadRequestsScreen> {
  List<dynamic>? _requests;
  String? _error;
  bool _busy = false;

  String get _loadId => widget.load['id'] as String;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('bookings/for-load/$_loadId');
      if (mounted) setState(() => _requests = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load requests.'; _requests = []; });
    }
  }

  Future<void> _decide(String requestId, bool approve) async {
    setState(() => _busy = true);
    try {
      await ref.read(dioProvider).post<dynamic>('bookings/requests/$requestId/${approve ? 'accept' : 'reject'}');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(approve ? 'Driver approved — trip created.' : 'Request rejected.'),
        ));
      }
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not complete that action.')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final list = _requests;
    return Scaffold(
      appBar: AppBar(title: const Text('Driver requests')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: list == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : list.isEmpty
                ? _empty()
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      Text('${widget.load['originAddress'] ?? '—'} → ${widget.load['destinationAddress'] ?? '—'}',
                          style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 12),
                      if (_busy) const Padding(padding: EdgeInsets.only(bottom: 12), child: LinearProgressIndicator()),
                      for (final r in list.cast<Map<String, dynamic>>()) _requestCard(r),
                    ],
                  ),
      ),
    );
  }

  Widget _requestCard(Map<String, dynamic> r) {
    final text = Theme.of(context).textTheme;
    final pending = r['status'] == 0;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              const CircleAvatar(child: Icon(Icons.person)),
              const SizedBox(width: 12),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(r['driverName'] ?? 'Driver', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                Text('Vehicle: ${r['vehicleRegistration'] ?? '—'}', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
              ])),
              StatusPill(labelOf(bookingStatus, r['status'])),
            ]),
            const SizedBox(height: 8),
            Row(children: [
              const Icon(Icons.verified, size: 16, color: AppColors.success),
              const SizedBox(width: 4),
              Text('Verified driver & vehicle', style: text.bodySmall),
            ]),
            if (pending) ...[
              const SizedBox(height: 14),
              Row(children: [
                Expanded(child: FilledButton(onPressed: _busy ? null : () => _decide(r['id'] as String, true), child: const Text('Approve'))),
                const SizedBox(width: 10),
                Expanded(child: OutlinedButton(onPressed: _busy ? null : () => _decide(r['id'] as String, false), child: const Text('Reject'))),
              ]),
            ],
          ],
        ),
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 100),
        Icon(Icons.hourglass_empty, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No requests yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Drivers who request this load will appear here')),
      ]);
}
