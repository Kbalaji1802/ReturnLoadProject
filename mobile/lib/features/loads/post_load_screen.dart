import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/dio_client.dart';

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
  final _origin = TextEditingController(text: 'Chennai');
  final _dest = TextEditingController(text: 'Coimbatore');
  final _weight = TextEditingController(text: '5000');
  final _price = TextEditingController(text: '15000');
  int _cargo = 0;
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

  Future<void> _submit() async {
    setState(() { _busy = true; _msg = null; });
    final now = DateTime.now().toUtc();
    try {
      await ref.read(dioProvider).post<dynamic>('loads', data: {
        // Chennai / Coimbatore coordinates as sensible defaults for the demo.
        'originLat': 13.0827, 'originLng': 80.2707, 'originAddress': _origin.text.trim(),
        'destinationLat': 11.0168, 'destinationLng': 76.9558, 'destinationAddress': _dest.text.trim(),
        'pickupStart': now.add(const Duration(hours: 2)).toIso8601String(),
        'pickupEnd': now.add(const Duration(hours: 8)).toIso8601String(),
        'cargoType': _cargo,
        'weightKg': double.tryParse(_weight.text) ?? 0,
        'offeredPriceInr': double.tryParse(_price.text),
      });
      setState(() => _msg = 'Load posted. Drivers can now see and accept it.');
    } on DioException catch (e) {
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Post a load'), leading: BackButton(onPressed: () => context.go('/dashboard'))),
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
