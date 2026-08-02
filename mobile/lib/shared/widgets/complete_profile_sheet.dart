import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../services/auth_repository.dart';

/// Collects the name and mobile the API needs to create a platform profile.
///
/// Registration now gathers these up front, so this is the backfill path for accounts that
/// signed up before it did. Those accounts are otherwise stuck: every load action answers
/// "Complete your profile" and, until this existed, nothing could complete it.
///
/// Returns true when a profile was created, so the caller can retry what the user was doing.
Future<bool> showCompleteProfileSheet(BuildContext context) async {
  final bool? created = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => const _CompleteProfileForm(),
  );
  return created ?? false;
}

class _CompleteProfileForm extends ConsumerStatefulWidget {
  const _CompleteProfileForm();

  @override
  ConsumerState<_CompleteProfileForm> createState() => _CompleteProfileFormState();
}

class _CompleteProfileFormState extends ConsumerState<_CompleteProfileForm> {
  final _fullName = TextEditingController();
  final _mobile = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _fullName.dispose();
    _mobile.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_fullName.text.trim().isEmpty || _mobile.text.trim().isEmpty) {
      setState(() => _error = 'Full name and mobile are required.');
      return;
    }

    setState(() { _busy = true; _error = null; });
    try {
      await ref.read(authRepositoryProvider).createProfile(_fullName.text.trim(), _mobile.text.trim());
      if (mounted) Navigator.pop(context, true);
    } on DioException catch (e) {
      // Surface the server's reason (e.g. an invalid mobile format) rather than a generic one.
      final data = e.response?.data;
      setState(() => _error = data is Map && data['errors'] is List && (data['errors'] as List).isNotEmpty
          ? (data['errors'] as List).first['message']?.toString() ?? 'Could not save your profile.'
          : 'Could not save your profile.');
    } catch (_) {
      setState(() => _error = 'Could not save your profile.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      // Keep the fields above the keyboard.
      padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.of(context).viewInsets.bottom + 16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Complete your profile', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          const Text('We need your name and mobile number before you can post loads.'),
          const SizedBox(height: 16),
          TextField(
            controller: _fullName,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Full name', border: OutlineInputBorder()),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _mobile,
            keyboardType: TextInputType.phone,
            decoration: const InputDecoration(labelText: 'Mobile', border: OutlineInputBorder()),
          ),
          const SizedBox(height: 12),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(_error!, style: const TextStyle(color: Colors.red)),
            ),
          FilledButton(
            onPressed: _busy ? null : _submit,
            child: _busy
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Save and continue'),
          ),
        ],
      ),
    );
  }
}
