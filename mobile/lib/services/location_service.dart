import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

/// A single device reading, reduced to the fields the app cares about (no plugin types leak out).
class DeviceLocation {
  const DeviceLocation({
    required this.latitude,
    required this.longitude,
    this.speedKph,
    this.headingDegrees,
    this.accuracyMetres,
  });

  final double latitude;
  final double longitude;
  final double? speedKph;
  final double? headingDegrees;
  final double? accuracyMetres;
}

/// Wraps the GPS plugin behind a small app-owned surface (M6). Swapping the plugin — or feeding
/// mock coordinates in a simulator — is a change here only; screens depend on this, not geolocator.
class LocationService {
  Future<bool> ensurePermission() async {
    if (!await Geolocator.isLocationServiceEnabled()) return false;

    LocationPermission permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    return permission == LocationPermission.always || permission == LocationPermission.whileInUse;
  }

  Future<DeviceLocation?> current() async {
    if (!await ensurePermission()) return null;
    final Position p = await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high),
    );
    return DeviceLocation(
      latitude: p.latitude,
      longitude: p.longitude,
      speedKph: p.speed >= 0 ? p.speed * 3.6 : null, // m/s -> km/h
      headingDegrees: p.heading >= 0 ? p.heading % 360 : null,
      accuracyMetres: p.accuracy >= 0 ? p.accuracy : null,
    );
  }
}

final locationServiceProvider = Provider<LocationService>((ref) => LocationService());
