import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/auth_repository.dart';
import '../../shared/theme/app_theme.dart';

/// The Load Owner's home. A shipper posts cargo and gets it matched to a truck's return leg —
/// they never see driver surfaces (accept load, trips). Posting a load is the primary action.
class LoadOwnerHomeTab extends ConsumerWidget {
  const LoadOwnerHomeTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    final email = ref.watch(currentEmailProvider) ?? 'Load Owner';
    final name = email.split('@').first;

    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(AppTheme.radius),
                gradient: const LinearGradient(
                  begin: Alignment.topLeft, end: Alignment.bottomRight,
                  colors: [AppColors.primary, AppColors.navy],
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Welcome, $name', style: text.titleLarge?.copyWith(color: Colors.white, fontWeight: FontWeight.w700), overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 6),
                  Text('Post a load and we match it to a truck heading your way.', style: text.bodyMedium?.copyWith(color: Colors.white70)),
                ],
              ),
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: () => context.push('/post-load'),
              icon: const Icon(Icons.post_add),
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
              label: const Text('Post a load'),
            ),
            const SizedBox(height: 12),
            Card(
              child: ListTile(
                leading: const Icon(Icons.inventory_2_outlined, color: AppColors.primary),
                title: const Text('My loads'),
                subtitle: const Text('Track loads you have posted'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Your posted-loads list is coming soon.')),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
