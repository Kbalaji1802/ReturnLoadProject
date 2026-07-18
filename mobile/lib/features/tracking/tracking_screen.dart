import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/dio_client.dart';

/// Trip tracking. The live map is intentionally NOT rendered because no Maps API key is
/// configured (see the sprint report). Instead this shows the tracking <b>architecture</b>:
/// the trip's real recorded tracking points fetched from the API, plus a clear banner. When
/// a Maps key is provided, a map widget replaces the banner and plots these same points.
class TrackingScreen extends ConsumerStatefulWidget {
  const TrackingScreen({super.key});

  static const routePath = '/tracking';
  static const routeName = 'tracking';

  @override
  ConsumerState<TrackingScreen> createState() => _TrackingScreenState();
}

class _TrackingScreenState extends ConsumerState<TrackingScreen> {
  final _tripId = TextEditingController();
  List<dynamic>? _points;
  String? _status;

  Future<void> _load() async {
    try {
      final dio = ref.read(dioProvider);
      final trip = await dio.get<dynamic>('trips/${_tripId.text.trim()}');
      final tracking = await dio.get<dynamic>('trips/${_tripId.text.trim()}/tracking');
      setState(() {
        _status = trip.data['data']['status'] as String?;
        _points = tracking.data['data'] as List<dynamic>;
      });
    } catch (_) {
      setState(() {
        _status = 'Trip not found';
        _points = <dynamic>[];
      });
    }
  }

  @override
  void dispose() {
    _tripId.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Trip tracking'), leading: BackButton(onPressed: () => context.go('/dashboard'))),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Card(
              color: Color(0xFFFFF3CD),
              child: Padding(
                padding: EdgeInsets.all(12),
                child: Text('Maps integration pending API key. Showing recorded tracking points.'),
              ),
            ),
            const SizedBox(height: 12),
            TextField(controller: _tripId, decoration: const InputDecoration(labelText: 'Trip id')),
            const SizedBox(height: 8),
            FilledButton(onPressed: _load, child: const Text('Load trip')),
            const SizedBox(height: 12),
            if (_status != null) Text('Status: $_status', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Expanded(
              child: _points == null
                  ? const Center(child: Text('Enter a trip id to view its path.'))
                  : _points!.isEmpty
                      ? const Center(child: Text('No tracking points recorded yet.'))
                      : ListView.builder(
                          itemCount: _points!.length,
                          itemBuilder: (context, i) {
                            final p = _points![i] as Map<String, dynamic>;
                            return ListTile(
                              leading: const Icon(Icons.my_location),
                              title: Text('(${p['latitude']}, ${p['longitude']})'),
                              subtitle: Text('${p['type']} · ${p['capturedAtUtc']}'),
                            );
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }
}
