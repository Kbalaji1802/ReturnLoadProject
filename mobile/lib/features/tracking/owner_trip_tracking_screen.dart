import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/rating_dialog.dart';
import '../../shared/widgets/status_pill.dart';

/// "Where is my truck?" — the load owner's live tracking of their shipment's trip (M6). Resolves
/// the trip for the load, then polls the live position and renders it on an OpenStreetMap map.
class OwnerTripTrackingScreen extends ConsumerStatefulWidget {
  const OwnerTripTrackingScreen({super.key, required this.load});
  static const routePath = '/track';
  static const routeName = 'track';

  final Map<String, dynamic> load;

  @override
  ConsumerState<OwnerTripTrackingScreen> createState() => _State();
}

class _State extends ConsumerState<OwnerTripTrackingScreen> {
  String? _tripId;
  Map<String, dynamic>? _live;
  String? _error;
  bool _busy = false;
  Timer? _timer;
  final _map = MapController();

  @override
  void initState() {
    super.initState();
    _resolveTrip();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _resolveTrip() async {
    try {
      final res = await ref.read(dioProvider).get<dynamic>('trips/for-load/${widget.load['id']}');
      final trip = res.data['data'] as Map<String, dynamic>;
      setState(() => _tripId = trip['id'] as String);
      await _poll();
      _timer = Timer.periodic(const Duration(seconds: 15), (_) => _poll());
    } catch (_) {
      setState(() => _error = 'No trip is running for this load yet.');
    }
  }

  Future<void> _poll() async {
    final id = _tripId;
    if (id == null) return;
    try {
      final res = await ref.read(dioProvider).get<dynamic>('trips/$id/tracking/live');
      if (mounted) setState(() => _live = res.data['data'] as Map<String, dynamic>);
    } catch (_) {/* keep last known */}
  }

  /// The load owner confirms a pickup/delivery gate (Part 5) — advances the trip past the gate.
  Future<void> _confirm(String targetName, String doneMessage) async {
    final id = _tripId;
    if (id == null) return;
    setState(() => _busy = true);
    try {
      await ref.read(dioProvider).post<dynamic>('trips/$id/status/$targetName');
      await _poll();
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(doneMessage)));
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not confirm right now.')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final live = _live;
    final hasLoc = live?['hasLocation'] == true;
    final lat = (live?['latitude'] as num?)?.toDouble();
    final lng = (live?['longitude'] as num?)?.toDouble();
    final center = (lat != null && lng != null) ? LatLng(lat, lng) : const LatLng(11.1271, 78.6569); // TN centre

    return Scaffold(
      appBar: AppBar(title: const Text('Live tracking')),
      body: _error != null
          ? _errorState()
          : Column(children: [
              Expanded(
                child: FlutterMap(
                  mapController: _map,
                  options: MapOptions(initialCenter: center, initialZoom: hasLoc ? 11 : 6.5),
                  children: [
                    TileLayer(urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', userAgentPackageName: 'com.returnload.mobile'),
                    if (hasLoc && lat != null && lng != null)
                      MarkerLayer(markers: [
                        Marker(point: LatLng(lat, lng), width: 46, height: 46, child: const Icon(Icons.local_shipping, color: AppColors.primary, size: 40)),
                      ]),
                    RichAttributionWidget(attributions: [TextSourceAttribution('OpenStreetMap contributors', onTap: () {})]),
                  ],
                ),
              ),
              _panel(live, hasLoc),
            ]),
    );
  }

  Widget _panel(Map<String, dynamic>? live, bool hasLoc) => SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Text('${widget.load['originAddress'] ?? '—'} → ${widget.load['destinationAddress'] ?? '—'}',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700)),
              const Spacer(),
              if (live != null) StatusPill(labelOf(tripStatus, live['status'])),
            ]),
            const SizedBox(height: 12),
            if (!hasLoc)
              Row(children: [
                const Icon(Icons.gps_off, color: AppColors.warning, size: 18),
                const SizedBox(width: 8),
                Expanded(child: Text('Waiting for the driver to share live location…', style: Theme.of(context).textTheme.bodyMedium)),
              ])
            else
              Row(children: [
                _metric(Icons.route, 'Remaining', live!['distanceRemainingKm'] != null ? '${live['distanceRemainingKm']} km' : '—'),
                const SizedBox(width: 20),
                _metric(Icons.schedule, 'ETA', live['etaMinutes'] != null ? _eta(live['etaMinutes']) : '—'),
              ]),
            // Owner-confirmation gates (Part 5): the owner authorises loading at pickup and confirms
            // delivery. These appear only at the exact step that needs the owner's sign-off.
            if (live != null && live['status'] == 3) ...[
              const SizedBox(height: 12),
              if (_busy) const LinearProgressIndicator() else FilledButton.icon(
                onPressed: () => _confirm('PickupConfirmed', 'Pickup confirmed — the driver can load.'),
                icon: const Icon(Icons.inventory),
                label: const Text('Confirm pickup'),
              ),
            ],
            if (live != null && live['status'] == 7) ...[
              const SizedBox(height: 12),
              if (_busy) const LinearProgressIndicator() else FilledButton.icon(
                onPressed: () => _confirm('DeliveryConfirmed', 'Delivery confirmed.'),
                icon: const Icon(Icons.check_circle),
                label: const Text('Confirm delivery'),
              ),
            ],
            if (live != null && live['status'] == 8 && _tripId != null) ...[
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: () async {
                  final ok = await showRatingDialog(context, ref, _tripId!, 'the driver');
                  if (ok && mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Thanks for your review!')));
                },
                icon: const Icon(Icons.star),
                label: const Text('Rate the driver'),
              ),
            ],
          ]),
        ),
      );

  Widget _metric(IconData i, String label, String value) => Row(children: [
        Icon(i, size: 20, color: AppColors.primary),
        const SizedBox(width: 8),
        Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(label, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
          Text(value, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
        ]),
      ]);

  static String _eta(Object? minutes) {
    final m = minutes is int ? minutes : int.tryParse('$minutes') ?? 0;
    final h = m ~/ 60;
    final r = m % 60;
    return h > 0 ? '${h}h ${r}m' : '${r}m';
  }

  Widget _errorState() => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.local_shipping_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(_error!, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
          ]),
        ),
      );
}
