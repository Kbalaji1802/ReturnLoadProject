import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../services/location_service.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/rating_dialog.dart';
import '../../shared/widgets/skeleton.dart';
import '../../shared/widgets/status_pill.dart';

/// The driver's trips (GET /trips/mine): the current trip with a lifecycle timeline that
/// advances one legal step at a time, plus completed/cancelled history.
class TripsTab extends ConsumerStatefulWidget {
  const TripsTab({super.key});
  @override
  ConsumerState<TripsTab> createState() => _TripsTabState();
}

class _TripsTabState extends ConsumerState<TripsTab> {
  List<dynamic>? _trips;
  String? _error;
  bool _busy = false;

  // Live location sharing (M6). Foreground periodic updates; adaptive interval + a background
  // service + offline queue are the documented productionization steps.
  bool _sharing = false;
  Timer? _shareTimer;
  DateTime? _lastSentAt;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _shareTimer?.cancel();
    super.dispose();
  }

  Future<void> _toggleShare(String tripId, bool on) async {
    if (!on) {
      _shareTimer?.cancel();
      setState(() => _sharing = false);
      return;
    }

    final ok = await ref.read(locationServiceProvider).ensurePermission();
    if (!ok) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Location permission is required to share your trip.')));
      return;
    }
    setState(() => _sharing = true);
    await _sendLocation(tripId);
    _shareTimer = Timer.periodic(const Duration(seconds: 30), (_) => _sendLocation(tripId));
  }

  Future<void> _sendLocation(String tripId) async {
    final loc = await ref.read(locationServiceProvider).current();
    if (loc == null) return;
    try {
      await ref.read(dioProvider).post<dynamic>('trips/$tripId/location', data: {
        'latitude': loc.latitude,
        'longitude': loc.longitude,
        'capturedAtUtc': DateTime.now().toUtc().toIso8601String(),
        'speedKph': loc.speedKph,
        'headingDegrees': loc.headingDegrees,
        'accuracyMetres': loc.accuracyMetres,
      });
      if (mounted) setState(() => _lastSentAt = DateTime.now());
    } catch (_) {
      // Offline: skip this tick. A local queue that flushes in order is the next step.
    }
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('trips/mine');
      if (mounted) setState(() => _trips = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load your trips.'; _trips = []; });
    }
  }

  Future<void> _advance(String tripId, String target) async {
    setState(() => _busy = true);
    try {
      await ref.read(dioProvider).post<dynamic>('trips/$tripId/status/$target');
      await _load();
    } on DioException catch (e) {
      // Surface the backend's reason (e.g. "Waiting for the load owner to confirm…").
      final msg = _apiMessage(e) ?? 'That action is not allowed right now.';
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String? _apiMessage(DioException e) {
    final data = e.response?.data;
    if (data is Map) {
      final errors = data['errors'];
      if (errors is List && errors.isNotEmpty) {
        return errors.first['message']?.toString();
      }
      if (data['message'] is String) return data['message'] as String;
    }
    return null;
  }

  bool _isActive(Map<String, dynamic> t) => tripIsActive(t['status']);

  @override
  Widget build(BuildContext context) {
    final trips = _trips;
    final active = trips?.cast<Map<String, dynamic>>().where(_isActive).toList() ?? [];
    final history = trips?.cast<Map<String, dynamic>>().where((t) => !_isActive(t)).toList() ?? [];

    return Scaffold(
      appBar: AppBar(title: const Text('My trips')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: trips == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : trips.isEmpty
                ? _empty()
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      if (_busy) const Padding(padding: EdgeInsets.only(bottom: 12), child: LinearProgressIndicator()),
                      if (active.isEmpty)
                        const Card(child: ListTile(
                          leading: Icon(Icons.local_shipping_outlined),
                          title: Text('No active trip'),
                          subtitle: Text('Request a load to start a trip'),
                        ))
                      else
                        for (final t in active) _activeTrip(t),
                      if (history.isNotEmpty) ...[
                        const SizedBox(height: 20),
                        Text('History', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                        const SizedBox(height: 8),
                        for (final t in history) _historyCard(t),
                      ],
                    ],
                  ),
      ),
    );
  }

  Widget _activeTrip(Map<String, dynamic> t) {
    final tripId = t['id'] as String;
    final status = t['status'] as int? ?? 0;
    final next = nextTripStatus(status);
    final nextIsOwnerGate = next != null && ownerConfirmStatuses.contains(next);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Text('Current trip', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
              const Spacer(),
              StatusPill(labelOf(tripStatus, status)),
            ]),
            const SizedBox(height: 16),
            _timeline(status),
            const SizedBox(height: 8),
            // Live location sharing while on the road.
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              secondary: const Icon(Icons.my_location, color: AppColors.primary),
              title: const Text('Share live location'),
              subtitle: Text(_sharing
                  ? (_lastSentAt != null ? 'Sharing with the load owner' : 'Starting…')
                  : 'Let the load owner see your progress'),
              value: _sharing,
              onChanged: (v) => _toggleShare(tripId, v),
            ),
            // Return Availability (Tamil Nadu differentiator): ask near the destination.
            if (status >= 5 && status < 8) _returnAvailability(t),
            const SizedBox(height: 8),
            if (next != null && nextIsOwnerGate)
              // Owner-confirmation gate: the load owner confirms in their app. The driver may
              // proceed only after the confirmation window elapses (the backend enforces it and
              // returns a "waiting for the owner" message until then — surfaced via the snackbar).
              _ownerGate(tripId, next)
            else if (next != null)
              FilledButton(
                onPressed: _busy ? null : () => _advance(tripId, tripStatusName[next] ?? ''),
                child: Text(tripActionLabel[next] ?? 'Next'),
              ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: _busy ? null : () => _advance(tripId, 'Cancelled'),
              child: const Text('Cancel trip', style: TextStyle(color: AppColors.error)),
            ),
          ],
        ),
      ),
    );
  }

  /// Return Availability — nudge the driver to line up a return load before unloading, the core
  /// deadhead-reduction differentiator. (Records intent locally for now; a return-load search
  /// hooks in with the return-leg feature.)
  Widget _returnAvailability(Map<String, dynamic> t) => Container(
        margin: const EdgeInsets.only(top: 4),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(14),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Approaching destination — will you return empty?', style: TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          Row(children: [
            Expanded(child: OutlinedButton(onPressed: () => _findReturnLoads(t), child: const Text('Yes'))),
            const SizedBox(width: 8),
            Expanded(child: OutlinedButton(onPressed: () => _return('Okay, no return load needed'), child: const Text('No'))),
            const SizedBox(width: 8),
            Expanded(child: OutlinedButton(onPressed: () => _return('Not sure'), child: const Text('Not sure'))),
          ]),
        ]),
      );

  void _findReturnLoads(Map<String, dynamic> t) {
    final lat = (t['destinationLat'] as num?)?.toDouble();
    final lng = (t['destinationLng'] as num?)?.toDouble();
    if (lat == null || lng == null) {
      _return('We\'ll look for return loads near your destination.');
      return;
    }
    context.push('/return-loads', extra: {'lat': lat, 'lng': lng, 'place': t['destinationAddress']});
  }

  void _return(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  /// A note + "Proceed anyway" for an owner-confirmation gate on the driver's side (Part 5).
  Widget _ownerGate(String tripId, int gateStatus) {
    final label = gateStatus == 10 ? 'pickup' : 'delivery';
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: AppColors.warning.withValues(alpha: 0.10), borderRadius: BorderRadius.circular(14)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          const Icon(Icons.hourglass_top, size: 18, color: AppColors.warning),
          const SizedBox(width: 8),
          Expanded(child: Text('Waiting for the load owner to confirm $label', style: const TextStyle(fontWeight: FontWeight.w600))),
        ]),
        const SizedBox(height: 6),
        Text('If they are unavailable, you can proceed once the confirmation window passes.',
            style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: 10),
        OutlinedButton(
          onPressed: _busy ? null : () => _advance(tripId, tripStatusName[gateStatus] ?? ''),
          child: Text('Proceed with $label'),
        ),
      ]),
    );
  }

  /// Vertical lifecycle timeline in true order; steps up to and including the current one are done.
  Widget _timeline(int status) {
    final currentPos = tripLifecyclePosition(status);
    return Column(
      children: [
        for (int pos = 0; pos < tripLifecycleOrder.length; pos++)
          Builder(builder: (context) {
            final stepStatus = tripLifecycleOrder[pos];
            final done = currentPos >= 0 && pos <= currentPos;
            final isCurrent = pos == currentPos;
            return Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Column(children: [
                Icon(
                  done ? Icons.check_circle : Icons.radio_button_unchecked,
                  size: 20,
                  color: done ? AppColors.success : Theme.of(context).colorScheme.outline,
                ),
                if (pos < tripLifecycleOrder.length - 1)
                  Container(width: 2, height: 18, color: (currentPos >= 0 && pos < currentPos) ? AppColors.success : Theme.of(context).colorScheme.outlineVariant),
              ]),
              const SizedBox(width: 12),
              Padding(
                padding: const EdgeInsets.only(top: 1),
                child: Text(
                  tripStatus[stepStatus] ?? '',
                  style: TextStyle(
                    fontWeight: isCurrent ? FontWeight.w700 : FontWeight.w400,
                    color: done ? null : Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
            ]);
          }),
      ],
    );
  }

  Widget _historyCard(Map<String, dynamic> t) {
    final completed = (t['status'] as int? ?? 0) == 8;
    return Card(
      child: ListTile(
        leading: Icon(completed ? Icons.check_circle : Icons.cancel, color: completed ? AppColors.success : AppColors.error),
        title: Text(labelOf(tripStatus, t['status'])),
        subtitle: Text('Trip ${(t['id'] as String).substring(0, 8)}…'),
        trailing: completed
            ? TextButton.icon(
                onPressed: () async {
                  final ok = await showRatingDialog(context, ref, t['id'] as String, 'the load owner');
                  if (ok && mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Thanks for your review!')));
                },
                icon: const Icon(Icons.star, size: 18),
                label: const Text('Rate'),
              )
            : null,
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 100),
        Icon(Icons.local_shipping_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No trips yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Request a load to get started')),
      ]);
}
