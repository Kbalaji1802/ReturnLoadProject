import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/auth_repository.dart';

/// Self-service account creation. Calls /auth/register (which returns tokens), so the
/// new user is signed straight in. Business roles (Driver/Shipper) are granted during
/// onboarding/verification, not at sign-up.
class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  static const routePath = '/register';
  static const routeName = 'register';

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _phone = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() { _busy = true; _error = null; });
    try {
      await ref.read(authRepositoryProvider).register(
            _email.text.trim(),
            _password.text,
            _phone.text.trim().isEmpty ? null : _phone.text.trim(),
          );
      if (mounted) context.go('/home');
    } on DioException catch (e) {
      setState(() {
        final data = e.response?.data;
        _error = data is Map && data['errors'] is List && (data['errors'] as List).isNotEmpty
            ? (data['errors'] as List).first['message']?.toString() ?? 'Registration failed.'
            : 'Registration failed (${e.response?.statusCode ?? 'no response'}).';
      });
    } catch (e) {
      setState(() => _error = 'Unexpected error: $e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Create account')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 400),
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(controller: _email, decoration: const InputDecoration(labelText: 'Email', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _password, obscureText: true, decoration: const InputDecoration(labelText: 'Password (min 12 chars, mixed case, digit, symbol)', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _phone, keyboardType: TextInputType.phone, decoration: const InputDecoration(labelText: 'Mobile (optional)', border: OutlineInputBorder())),
                const SizedBox(height: 16),
                if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_error!, style: const TextStyle(color: Colors.red))),
                FilledButton(
                  onPressed: _busy ? null : _submit,
                  child: _busy ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Create account'),
                ),
                const SizedBox(height: 8),
                TextButton(onPressed: _busy ? null : () => context.go('/login'), child: const Text('I already have an account')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
