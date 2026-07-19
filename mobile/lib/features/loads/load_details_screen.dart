import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/route_timeline.dart';

/// Load details + the M4.4 request flow: the driver requests the load with one of their
/// verified vehicles; the load owner then approves or rejects. No more instant accept.
class LoadDetailsScreen extends ConsumerStatefulWidget {
  const LoadDetailsScreen({super.key, required this.load});
  static const routePath = '/load';
  static const routeName = 'load-details';

  final Map<String, dynamic> load;

  @override
  ConsumerState<LoadDetailsScreen> createState() => _LoadDetailsScreenState();
}

class _LoadDetailsScreenState extends ConsumerState<LoadDetailsScreen> {
  bool _busy = false;
  bool _requested = false;

  Map<String, dynamic> get load => widget.load;

  @override
  Widget build(BuildContext context) {
    final price = load['offeredPriceInr'];
    final distance = load['distanceKm'];
    final minutes = load['estimatedDurationMinutes'];
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
                  if (distance != null) _row(context, Icons.route, 'Distance', '$distance km'),
                  if (minutes != null) _row(context, Icons.schedule, 'Estimated time', _duration(minutes)),
                  _row(context, Icons.payments, 'Estimated earnings', price != null ? '₹$price' : 'Negotiable'),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),
          if (_requested)
            Card(
              color: AppColors.success.withValues(alpha: 0.12),
              child: const Padding(
                padding: EdgeInsets.all(16),
                child: Row(children: [
                  Icon(Icons.hourglass_top, color: AppColors.success),
                  SizedBox(width: 12),
                  Expanded(child: Text('Request sent. Waiting for the load owner to approve.')),
                ]),
              ),
            )
          else
            FilledButton.icon(
              onPressed: _busy ? null : _request,
              icon: _busy
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.send),
              label: const Text('Request this load'),
            ),
          const SizedBox(height: 10),
          if (!_requested) OutlinedButton(onPressed: () => context.pop(), child: const Text('Back')),
        ],
      ),
    );
  }

  static String _duration(Object? minutes) {
    final m = minutes is int ? minutes : int.tryParse('$minutes') ?? 0;
    final h = m ~/ 60;
    final r = m % 60;
    return h > 0 ? '${h}h ${r}m' : '${r}m';
  }

  Widget _row(BuildContext c, IconData icon, String k, String v) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(children: [
          Icon(icon, size: 20, color: AppColors.primary),
          const SizedBox(width: 12),
          Text(k, style: Theme.of(c).textTheme.bodyMedium?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant)),
          const Spacer(),
          Flexible(child: Text(v, textAlign: TextAlign.right, style: Theme.of(c).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w600))),
        ]),
      );

  Future<void> _request() async {
    setState(() => _busy = true);
    try {
      final vehicleId = await _pickVehicle();
      if (vehicleId == null) {
        if (mounted) setState(() => _busy = false);
        return;
      }

      await ref.read(dioProvider).post<dynamic>('bookings/requests', data: {
        'loadId': load['id'],
        'vehicleId': vehicleId,
      });
      if (mounted) setState(() => _requested = true);
    } on DioException catch (e) {
      final data = e.response?.data;
      final msg = data is Map && data['errors'] is List && (data['errors'] as List).isNotEmpty
          ? (data['errors'] as List).first['message']?.toString() ?? 'Could not send the request.'
          : 'Could not send the request.';
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
    } finally {
      if (mounted && !_requested) setState(() => _busy = false);
    }
  }

  /// Resolves which verified vehicle to request with (auto if one, prompt if several).
  Future<String?> _pickVehicle() async {
    final res = await ref.read(dioProvider).get<dynamic>('vehicles/mine');
    final all = (res.data['data'] as List).cast<Map<String, dynamic>>();
    final verified = all.where((v) => v['status'] == 1).toList(); // Active == verified

    if (verified.isEmpty) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Add a vehicle and get it verified before requesting loads.'),
        ));
      }
      return null;
    }
    if (verified.length == 1) return verified.first['id'] as String;

    if (!mounted) return null;
    return showModalBottomSheet<String>(
      context: context,
      builder: (c) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const Padding(padding: EdgeInsets.all(16), child: Text('Choose a vehicle')),
          for (final v in verified)
            ListTile(
              leading: const Icon(Icons.local_shipping),
              title: Text(v['registrationNumber'] ?? '—'),
              subtitle: Text('${v['maxPayloadKg']} kg'),
              onTap: () => Navigator.pop(c, v['id'] as String),
            ),
        ]),
      ),
    );
  }
}
