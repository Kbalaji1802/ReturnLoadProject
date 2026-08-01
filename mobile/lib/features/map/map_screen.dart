import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../../services/location_service.dart';
import '../../shared/theme/app_theme.dart';

/// Live map using OpenStreetMap tiles (flutter_map — no API key required). Centres on the driver's
/// real current location (no fabricated demo pins); if location is unavailable it shows the Tamil
/// Nadu region with a prompt to enable GPS.
class MapScreen extends ConsumerStatefulWidget {
  const MapScreen({super.key});
  static const routePath = '/map';
  static const routeName = 'map';

  @override
  ConsumerState<MapScreen> createState() => _MapScreenState();
}

class _MapScreenState extends ConsumerState<MapScreen> {
  LatLng? _me;
  bool _loading = true;
  final _map = MapController();

  static const _tnCentre = LatLng(11.1271, 78.6569);

  @override
  void initState() {
    super.initState();
    _locate();
  }

  Future<void> _locate() async {
    setState(() => _loading = true);
    final loc = await ref.read(locationServiceProvider).current();
    if (!mounted) return;
    setState(() {
      _me = loc != null ? LatLng(loc.latitude, loc.longitude) : null;
      _loading = false;
    });
    if (_me != null) _map.move(_me!, 12);
  }

  @override
  Widget build(BuildContext context) {
    final center = _me ?? _tnCentre;
    return Scaffold(
      appBar: AppBar(title: const Text('Map')),
      floatingActionButton: FloatingActionButton(
        onPressed: _loading ? null : _locate,
        child: const Icon(Icons.my_location),
      ),
      body: Stack(children: [
        FlutterMap(
          mapController: _map,
          options: MapOptions(initialCenter: center, initialZoom: _me != null ? 12 : 6.5),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.returnload.mobile',
            ),
            if (_me != null)
              MarkerLayer(markers: [
                Marker(point: _me!, width: 44, height: 44, child: const Icon(Icons.my_location, color: AppColors.primary, size: 40)),
              ]),
            RichAttributionWidget(attributions: [
              TextSourceAttribution('OpenStreetMap contributors', onTap: () {}),
            ]),
          ],
        ),
        if (!_loading && _me == null)
          Positioned(
            left: 16, right: 16, bottom: 16,
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(14),
                child: Row(children: [
                  const Icon(Icons.gps_off, color: AppColors.warning),
                  const SizedBox(width: 10),
                  Expanded(child: Text('Enable location to see where you are', style: Theme.of(context).textTheme.bodyMedium)),
                  TextButton(onPressed: _locate, child: const Text('Retry')),
                ]),
              ),
            ),
          ),
      ]),
    );
  }
}
