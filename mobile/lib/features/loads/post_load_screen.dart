import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/dio_client.dart';
import '../../shared/widgets/complete_profile_sheet.dart';

/// Lets a shipper post a load (POST /loads). Requires the Shipper role — sign in as
/// shipper@returnload.test for the demo. Pickup window defaults to a sensible range.
class PostLoadScreen extends ConsumerStatefulWidget {
  const PostLoadScreen({super.key});

  static const routePath = '/post-load';
  static const routeName = 'post-load';

  @override
  ConsumerState<PostLoadScreen> createState() => _State();
}

class _State extends ConsumerState<PostLoadScreen> {
  final _origin = TextEditingController();
  final _dest = TextEditingController();
  final _weight = TextEditingController();
  final _price = TextEditingController();
  int _cargo = 0;
  int _areaType = 1; // Suburban (≈10 km) — a safe default pickup reach (Part 2).
  bool _busy = false;
  String? _msg;

  static const _cargoTypes = {
    0: 'General', 1: 'Perishable', 2: 'Fragile', 3: 'Hazardous', 4: 'Construction', 5: 'Liquid', 6: 'Refrigerated',
  };

  @override
  void dispose() {
    _origin.dispose();
    _dest.dispose();
    _weight.dispose();
    _price.dispose();
    super.dispose();
  }

  /// Resolves a typed place to a real geocoded location via the geo service — no fabricated
  /// coordinates. Returns the first (best) match, or null if nothing was found.
  Future<Map<String, dynamic>?> _geocode(String query) async {
    if (query.isEmpty) return null;
    final res = await ref.read(dioProvider).get<dynamic>('geo/search', queryParameters: {'q': query});
    final list = res.data['data'] as List;
    return list.isEmpty ? null : list.first as Map<String, dynamic>;
  }

  Future<void> _submit() async {
    // Basic validation — no more prefilled demo values, so guard against empty/invalid input.
    if (_origin.text.trim().isEmpty || _dest.text.trim().isEmpty) {
      setState(() => _msg = 'Enter both a pickup and a drop location.');
      return;
    }
    if ((double.tryParse(_weight.text) ?? 0) <= 0) {
      setState(() => _msg = 'Enter the load weight in kg.');
      return;
    }

    setState(() { _busy = true; _msg = null; });
    final now = DateTime.now().toUtc();
    try {
      // Geocode both places so we store real coordinates + structured address (never free text
      // with fabricated coordinates). The server derives distance/ETA from these.
      final origin = await _geocode(_origin.text.trim());
      final dest = await _geocode(_dest.text.trim());
      if (origin == null || dest == null) {
        setState(() => _msg = 'Could not locate that place. Try a more specific name (e.g. "Chennai, Tamil Nadu").');
        return;
      }

      await ref.read(dioProvider).post<dynamic>('loads', data: {
        'originLat': origin['latitude'], 'originLng': origin['longitude'], 'originAddress': origin['displayName'],
        'destinationLat': dest['latitude'], 'destinationLng': dest['longitude'], 'destinationAddress': dest['displayName'],
        'pickupStart': now.add(const Duration(hours: 2)).toIso8601String(),
        'pickupEnd': now.add(const Duration(hours: 8)).toIso8601String(),
        'cargoType': _cargo,
        'weightKg': double.tryParse(_weight.text) ?? 0,
        'offeredPriceInr': double.tryParse(_price.text),
        'pickupAreaType': _areaType,
      });

      // Success: reset the form (so it can't be re-posted with stale values) and take the owner
      // to their loads, with a confirmation snackbar (Part 4 form-UX contract).
      if (!mounted) return;
      _origin.clear();
      _dest.clear();
      _weight.clear();
      _price.clear();
      setState(() { _cargo = 0; _areaType = 1; });
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Load posted — drivers nearby can now see it.')));
      context.go('/my-loads');
      return;
    } on DioException catch (e) {
      // The API gates posting on having a platform profile. Accounts created before
      // registration collected a name have none, so offer the form and retry rather than
      // telling the user to complete something with no way to do it.
      if (e.response?.statusCode == 400 && _isMissingProfile(e) && mounted) {
        if (await showCompleteProfileSheet(context)) {
          if (mounted) await _submit();
          return;
        }
      }

      setState(() {
        if (e.response?.statusCode == 403) {
          _msg = 'Only a Shipper can post loads. Sign in as shipper@returnload.test.';
        } else {
          final data = e.response?.data;
          _msg = data is Map && data['errors'] is List && (data['errors'] as List).isNotEmpty
              ? (data['errors'] as List).first['message']?.toString() ?? 'Failed.'
              : 'Failed (${e.response?.statusCode ?? 'no response'}).';
        }
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Matched on the server's message rather than a code: the API returns a plain
  /// VALIDATION_ERROR for this, shared with every other 400 the endpoint can produce.
  static bool _isMissingProfile(DioException e) {
    final data = e.response?.data;
    if (data is! Map) return false;
    final errors = data['errors'];
    final String message = errors is List && errors.isNotEmpty
        ? (errors.first['message']?.toString() ?? '')
        : (data['message']?.toString() ?? '');
    return message.toLowerCase().contains('complete your profile');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Post a load'), leading: BackButton(onPressed: () => context.go('/home'))),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 460),
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(controller: _origin, decoration: const InputDecoration(labelText: 'Pickup (origin)', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _dest, decoration: const InputDecoration(labelText: 'Drop (destination)', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                DropdownButtonFormField<int>(
                  initialValue: _cargo,
                  decoration: const InputDecoration(labelText: 'Cargo type', border: OutlineInputBorder()),
                  items: _cargoTypes.entries.map((e) => DropdownMenuItem(value: e.key, child: Text(e.value))).toList(),
                  onChanged: (v) => setState(() => _cargo = v ?? 0),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<int>(
                  initialValue: _areaType,
                  decoration: const InputDecoration(
                    labelText: 'Pickup area', border: OutlineInputBorder(),
                    helperText: 'Sets how far drivers can be from the pickup and still see this load',
                  ),
                  items: const [
                    DropdownMenuItem(value: 0, child: Text('Urban (≈5 km reach)')),
                    DropdownMenuItem(value: 1, child: Text('Suburban (≈10 km reach)')),
                    DropdownMenuItem(value: 2, child: Text('Highway (≈25 km reach)')),
                  ],
                  onChanged: (v) => setState(() => _areaType = v ?? 1),
                ),
                const SizedBox(height: 12),
                TextField(controller: _weight, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Weight (kg)', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _price, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Offered price (₹, optional)', border: OutlineInputBorder())),
                const SizedBox(height: 16),
                if (_msg != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_msg!)),
                FilledButton(onPressed: _busy ? null : _submit, child: _busy ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Post load')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
