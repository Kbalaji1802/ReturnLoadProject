import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../shared/theme/app_theme.dart';

/// Live map using OpenStreetMap tiles (flutter_map — no API key required). If a Google
/// Maps key is later configured, a Google map can replace this layer.
class MapScreen extends StatelessWidget {
  const MapScreen({super.key});
  static const routePath = '/map';
  static const routeName = 'map';

  @override
  Widget build(BuildContext context) {
    const chennai = LatLng(13.0827, 80.2707);
    const coimbatore = LatLng(11.0168, 76.9558);
    return Scaffold(
      appBar: AppBar(title: const Text('Map')),
      body: FlutterMap(
        options: const MapOptions(initialCenter: LatLng(12.0, 78.5), initialZoom: 6.5),
        children: [
          TileLayer(
            urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
            userAgentPackageName: 'com.returnload.mobile',
          ),
          const MarkerLayer(markers: [
            Marker(point: chennai, width: 44, height: 44, child: Icon(Icons.place, color: AppColors.error, size: 44)),
            Marker(point: coimbatore, width: 44, height: 44, child: Icon(Icons.trip_origin, color: AppColors.primary, size: 40)),
          ]),
          RichAttributionWidget(attributions: [
            TextSourceAttribution('OpenStreetMap contributors', onTap: () {}),
          ]),
        ],
      ),
    );
  }
}
