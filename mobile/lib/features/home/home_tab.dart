import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/enums.dart';
import '../../services/auth_repository.dart';
import '../../services/dio_client.dart';
import '../../services/location_service.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/status_pill.dart';

/// The driver's dashboard — everything from real APIs (correction-sprint Parts 3, 9, 10): live
/// verification + rating, an availability control, the current trip's distance-remaining + ETA,
/// unread alerts, and available-load count. No hardcoded stats or placeholder trust signals.
class HomeTab extends ConsumerStatefulWidget {
  const HomeTab({super.key});
  @override
  ConsumerState<HomeTab> createState() => _HomeTabState();
}

class _HomeTabState extends ConsumerState<HomeTab> {
  Map<String, dynamic>? _driver;      // drivers/me (status, availability, userProfileId)
  double? _rating;                    // null when the driver has no reviews yet
  int _ratingCount = 0;
  Map<String, dynamic>? _liveTrip;    // active trip's live view (distance/ETA), or null
  int? _availableLoads;
  int _unread = 0;
  bool _loading = true;
  bool _hasProfile = true;
  bool _savingAvailability = false;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    final dio = ref.read(dioProvider);
    setState(() => _loading = true);

    // Driver profile (verification + availability). A brand-new account may not have one yet.
    try {
      final me = await dio.get<dynamic>('drivers/me');
      _driver = me.data['data'] as Map<String, dynamic>;
      _hasProfile = true;
    } on DioException {
      _driver = null;
      _hasProfile = false;
    }

    final userProfileId = _driver?['userProfileId'];
    // Rating.
    if (userProfileId != null) {
      try {
        final r = await dio.get<dynamic>('reviews/summary/$userProfileId');
        final data = r.data['data'] as Map<String, dynamic>;
        _ratingCount = (data['count'] as num?)?.toInt() ?? 0;
        _rating = _ratingCount > 0 ? (data['average'] as num?)?.toDouble() : null;
      } catch (_) {/* leave null */}
    }

    // Available loads.
    try {
      final res = await dio.get<dynamic>('loads/matched');
      _availableLoads = (res.data['data'] as List).length;
    } catch (_) {
      _availableLoads = 0;
    }

    // Unread notifications.
    try {
      final res = await dio.get<dynamic>('notifications/unread-count');
      _unread = (res.data['data'] as num?)?.toInt() ?? 0;
    } catch (_) {/* keep 0 */}

    // Current trip → its live distance-remaining + ETA.
    _liveTrip = null;
    try {
      final res = await dio.get<dynamic>('trips/mine');
      final trips = (res.data['data'] as List).cast<Map<String, dynamic>>();
      final active = trips.where((t) => tripIsActive(t['status'])).toList();
      if (active.isNotEmpty) {
        final tripId = active.first['id'];
        try {
          final live = await dio.get<dynamic>('trips/$tripId/tracking/live');
          _liveTrip = live.data['data'] as Map<String, dynamic>;
        } catch (_) {
          _liveTrip = active.first; // fall back to the trip itself (status only)
        }
      }
    } catch (_) {/* no trips */}

    if (mounted) setState(() => _loading = false);
  }

  Future<void> _setAvailability(int value) async {
    setState(() => _savingAvailability = true);
    // Share GPS when going Available so matching (radius) and the owner (distance/ETA) are accurate.
    double? lat, lng;
    if (value == 0) {
      final loc = await ref.read(locationServiceProvider).current();
      lat = loc?.latitude;
      lng = loc?.longitude;
    }
    try {
      final res = await ref.read(dioProvider).put<dynamic>('drivers/me/availability', data: {
        'availability': value,
        'latitude': lat,
        'longitude': lng,
      });
      _driver = res.data['data'] as Map<String, dynamic>;
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('You are now ${driverAvailability[value] ?? '—'}.')));
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update availability.')));
    } finally {
      if (mounted) setState(() => _savingAvailability = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final email = ref.watch(currentEmailProvider) ?? 'Driver';
    final name = email.split('@').first;
    final int? status = (_driver?['status'] as num?)?.toInt();
    final int availability = (_driver?['availability'] as num?)?.toInt() ?? 2; // default Offline

    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _fetch,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _header(context, text, name, status),
              const SizedBox(height: 16),
              if (!_hasProfile) _registerPrompt(context),
              if (_hasProfile) ...[
                _availabilityCard(context, text, availability),
                const SizedBox(height: 16),
                if (_liveTrip != null) ...[
                  _currentTripCard(context, text),
                  const SizedBox(height: 16),
                ],
              ],
              GridView.count(
                crossAxisCount: 2, shrinkWrap: true, physics: const NeverScrollableScrollPhysics(),
                mainAxisSpacing: 12, crossAxisSpacing: 12, childAspectRatio: 1.55,
                children: [
                  _stat(context, Icons.inventory_2, _loading ? '…' : (_availableLoads?.toString() ?? '0'), 'Available loads', AppColors.warning),
                  _stat(context, Icons.notifications, _loading ? '…' : _unread.toString(), 'Unread alerts', AppColors.primary),
                ],
              ),
              const SizedBox(height: 8),
              Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Text('Quick actions', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700))),
              Wrap(spacing: 12, runSpacing: 12, children: [
                _action(context, Icons.description, 'Documents', () => context.push('/documents')),
                _action(context, Icons.local_shipping, 'Vehicle', () => context.push('/vehicle')),
                _action(context, Icons.badge, 'My profile', () => context.push('/driver-profile')),
                _action(context, Icons.map, 'Map', () => context.push('/map')),
              ]),
            ],
          ),
        ),
      ),
    );
  }

  Widget _header(BuildContext context, TextTheme text, String name, int? status) => Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [AppColors.primary, AppColors.navy]),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Text('Welcome back, ', style: text.titleMedium?.copyWith(color: Colors.white70)),
              Expanded(child: Text(name, style: text.titleLarge?.copyWith(color: Colors.white, fontWeight: FontWeight.w700), overflow: TextOverflow.ellipsis)),
            ]),
            const SizedBox(height: 12),
            Row(children: [
              // Real verification status — never a hardcoded "Verified" pill.
              StatusPill(status == null ? 'Unregistered' : (driverStatus[status] ?? '—')),
              const SizedBox(width: 8),
              if (_rating != null) ...[
                const Icon(Icons.star, color: Colors.amberAccent, size: 18),
                Text(' ${_rating!.toStringAsFixed(1)}', style: text.titleSmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w700)),
                Text('  ($_ratingCount)', style: text.bodySmall?.copyWith(color: Colors.white70)),
              ] else if (_hasProfile)
                Text('No ratings yet', style: text.bodySmall?.copyWith(color: Colors.white70)),
            ]),
          ],
        ),
      );

  Widget _availabilityCard(BuildContext context, TextTheme text, int availability) {
    final busy = availability == 1;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            const Icon(Icons.toggle_on, color: AppColors.primary),
            const SizedBox(width: 8),
            Text('Availability', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            const Spacer(),
            if (_savingAvailability) const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)),
          ]),
          const SizedBox(height: 4),
          Text(busy
              ? 'You are on a trip — set automatically to Busy. You will not receive new loads until it ends.'
              : 'Only "Available" drivers receive new loads.', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
          const SizedBox(height: 12),
          if (busy)
            const StatusPill('Busy')
          else
            Wrap(spacing: 8, runSpacing: 8, children: [
              for (final e in driverAvailabilityChoices.entries)
                ChoiceChip(
                  label: Text(e.value),
                  selected: availability == e.key,
                  onSelected: _savingAvailability ? null : (_) => _setAvailability(e.key),
                ),
            ]),
        ]),
      ),
    );
  }

  Widget _currentTripCard(BuildContext context, TextTheme text) {
    final live = _liveTrip!;
    final int status = (live['status'] as num?)?.toInt() ?? 0;
    final remaining = live['distanceRemainingKm'];
    final eta = live['etaMinutes'];
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            const Icon(Icons.local_shipping, color: AppColors.primary),
            const SizedBox(width: 8),
            Text('Current trip', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            const Spacer(),
            StatusPill(labelOf(tripStatus, status)),
          ]),
          const SizedBox(height: 4),
          Text('Manage it in the Trips tab', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
          const SizedBox(height: 12),
          Row(children: [
            _metric(context, Icons.route, 'Remaining', remaining != null ? '$remaining km' : '—'),
            const SizedBox(width: 24),
            _metric(context, Icons.schedule, 'ETA', eta != null ? _fmtEta(eta) : '—'),
          ]),
        ]),
      ),
    );
  }

  static String _fmtEta(Object? minutes) {
    final m = minutes is int ? minutes : int.tryParse('$minutes') ?? 0;
    final h = m ~/ 60;
    final r = m % 60;
    return h > 0 ? '${h}h ${r}m' : '${r}m';
  }

  Widget _metric(BuildContext c, IconData i, String label, String value) => Row(children: [
        Icon(i, size: 20, color: AppColors.primary),
        const SizedBox(width: 8),
        Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(label, style: Theme.of(c).textTheme.bodySmall?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant)),
          Text(value, style: Theme.of(c).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
        ]),
      ]);

  Widget _registerPrompt(BuildContext context) => Card(
        child: ListTile(
          leading: const Icon(Icons.badge_outlined, color: AppColors.primary),
          title: const Text('Complete your driver profile'),
          subtitle: const Text('Register and upload your licence to start receiving loads'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => context.push('/driver-profile'),
        ),
      );

  Widget _stat(BuildContext c, IconData icon, String value, String label, Color color) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(padding: const EdgeInsets.all(8), decoration: BoxDecoration(color: color.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(12)), child: Icon(icon, color: color, size: 22)),
              const Spacer(),
              Text(value, style: Theme.of(c).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
              Text(label, style: Theme.of(c).textTheme.bodySmall?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant), maxLines: 1, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      );

  Widget _action(BuildContext c, IconData icon, String label, VoidCallback onTap) {
    final width = (MediaQuery.sizeOf(c).width - 16 * 2 - 12) / 2;
    return SizedBox(
      width: width.clamp(140, 260),
      child: Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 16),
            child: Row(children: [Icon(icon, color: AppColors.primary), const SizedBox(width: 10), Flexible(child: Text(label, overflow: TextOverflow.ellipsis))]),
          ),
        ),
      ),
    );
  }
}
