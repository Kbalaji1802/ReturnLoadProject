import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/dio_client.dart';

/// Lets a signed-in user create their Driver profile (name, mobile, licence).
/// Backed by POST /drivers/register. Verification (licence approval) is done by Operations.
class DriverProfileScreen extends ConsumerStatefulWidget {
  const DriverProfileScreen({super.key});

  static const routePath = '/driver-profile';
  static const routeName = 'driver-profile';

  @override
  ConsumerState<DriverProfileScreen> createState() => _State();
}

class _State extends ConsumerState<DriverProfileScreen> {
  final _name = TextEditingController();
  final _mobile = TextEditingController();
  final _licence = TextEditingController(text: 'TN01 20200001234');
  bool _busy = false;
  String? _msg;

  @override
  void dispose() {
    _name.dispose();
    _mobile.dispose();
    _licence.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() { _busy = true; _msg = null; });
    try {
      await ref.read(dioProvider).post<dynamic>('drivers/register', data: {
        'fullName': _name.text.trim(),
        'mobile': _mobile.text.trim(),
        'licenceNumber': _licence.text.trim(),
        'carrierId': null,
      });
      setState(() => _msg = 'Driver profile created. Upload your licence for verification.');
    } on DioException catch (e) {
      final data = e.response?.data;
      setState(() => _msg = data is Map && data['errors'] is List && (data['errors'] as List).isNotEmpty
          ? (data['errors'] as List).first['message']?.toString() ?? 'Failed.'
          : 'Failed (${e.response?.statusCode ?? 'no response'}).');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Register as driver'), leading: BackButton(onPressed: () => context.go('/dashboard'))),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 460),
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(controller: _name, decoration: const InputDecoration(labelText: 'Full name', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _mobile, keyboardType: TextInputType.phone, decoration: const InputDecoration(labelText: 'Mobile (10-digit Indian)', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _licence, decoration: const InputDecoration(labelText: 'Driving licence number', border: OutlineInputBorder())),
                const SizedBox(height: 16),
                if (_msg != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_msg!)),
                FilledButton(onPressed: _busy ? null : _submit, child: _busy ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Create driver profile')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
