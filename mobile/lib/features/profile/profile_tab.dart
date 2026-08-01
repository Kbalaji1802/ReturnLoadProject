import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_providers.dart';
import '../../core/enums.dart';
import '../../services/auth_repository.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/status_pill.dart';

class ProfileTab extends ConsumerStatefulWidget {
  const ProfileTab({super.key});

  @override
  ConsumerState<ProfileTab> createState() => _ProfileTabState();
}

class _ProfileTabState extends ConsumerState<ProfileTab> {
  int? _status;        // driver verification status (null = no driver profile / owner)
  double? _rating;     // null when no reviews
  int _ratingCount = 0;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    final dio = ref.read(dioProvider);
    try {
      final me = await dio.get<dynamic>('drivers/me');
      final driver = me.data['data'] as Map<String, dynamic>;
      if (mounted) setState(() => _status = (driver['status'] as num?)?.toInt());
      final upid = driver['userProfileId'];
      if (upid != null) {
        final r = await dio.get<dynamic>('reviews/summary/$upid');
        final data = r.data['data'] as Map<String, dynamic>;
        final count = (data['count'] as num?)?.toInt() ?? 0;
        if (mounted) setState(() { _ratingCount = count; _rating = count > 0 ? (data['average'] as num?)?.toDouble() : null; });
      }
    } catch (_) {/* not a driver, or offline — show what we have */}
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final email = ref.watch(currentEmailProvider) ?? 'driver@returnload.test';

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: RefreshIndicator(
        onRefresh: _fetch,
        child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Row(children: [
                const CircleAvatar(radius: 32, backgroundColor: AppColors.primary, child: Icon(Icons.person, size: 34, color: Colors.white)),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(email.split('@').first, style: text.titleLarge?.copyWith(fontWeight: FontWeight.w700), overflow: TextOverflow.ellipsis),
                    Text(email, style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant), overflow: TextOverflow.ellipsis),
                    const SizedBox(height: 8),
                    Row(children: [
                      // Real verification status + rating — no hardcoded "Verified"/4.8.
                      StatusPill(_status == null ? 'Not a driver' : (driverStatus[_status!] ?? '—')),
                      const SizedBox(width: 8),
                      if (_rating != null) ...[
                        const Icon(Icons.star, color: AppColors.warning, size: 18),
                        Text(' ${_rating!.toStringAsFixed(1)}', style: text.titleSmall?.copyWith(fontWeight: FontWeight.w700)),
                        Text(' ($_ratingCount)', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                      ] else
                        Text('No ratings yet', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    ]),
                  ]),
                ),
              ]),
            ),
          ),
          const SizedBox(height: 16),
          Card(child: Column(children: [
            _tile(context, Icons.badge, 'Register as driver', () => context.push('/driver-profile')),
            const Divider(height: 1),
            _tile(context, Icons.local_shipping, 'My vehicle', () => context.push('/vehicle')),
            const Divider(height: 1),
            _tile(context, Icons.description, 'Documents', () => context.push('/documents')),
            const Divider(height: 1),
            _tile(context, Icons.translate, 'Language', () => _language(context, ref)),
            const Divider(height: 1),
            _tile(context, Icons.support_agent, 'Support', () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Support: support@returnload.test')))),
            const Divider(height: 1),
            _tile(context, Icons.settings, 'Settings', () => context.push('/settings')),
          ])),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: () async {
              await ref.read(authRepositoryProvider).logout();
              if (context.mounted) context.go('/login');
            },
            icon: const Icon(Icons.logout, color: AppColors.error),
            label: const Text('Logout', style: TextStyle(color: AppColors.error)),
          ),
        ],
        ),
      ),
    );
  }

  Widget _tile(BuildContext c, IconData icon, String title, VoidCallback onTap) =>
      ListTile(leading: Icon(icon, color: AppColors.primary), title: Text(title), trailing: const Icon(Icons.chevron_right), onTap: onTap);

  void _language(BuildContext context, WidgetRef ref) {
    final current = ref.read(languageProvider);
    void pick(String v) { ref.read(languageProvider.notifier).state = v; Navigator.pop(context); }
    showDialog<void>(
      context: context,
      builder: (_) => SimpleDialog(
        title: const Text('Language'),
        children: [
          ListTile(title: const Text('English'), trailing: current == 'en' ? const Icon(Icons.check, color: AppColors.primary) : null, onTap: () => pick('en')),
          ListTile(title: const Text('தமிழ் (Tamil)'), trailing: current == 'ta' ? const Icon(Icons.check, color: AppColors.primary) : null, onTap: () => pick('ta')),
        ],
      ),
    );
  }
}
