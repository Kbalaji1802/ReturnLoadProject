import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../services/auth_repository.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';

/// The Load Owner's home (correction-sprint Part 9): a real dashboard over their loads — open loads
/// awaiting a driver, running trips, and completed deliveries — plus the post-load action. No
/// placeholder values; every number comes from GET /loads/mine.
class LoadOwnerHomeTab extends ConsumerStatefulWidget {
  const LoadOwnerHomeTab({super.key});

  @override
  ConsumerState<LoadOwnerHomeTab> createState() => _LoadOwnerHomeTabState();
}

class _LoadOwnerHomeTabState extends ConsumerState<LoadOwnerHomeTab> {
  List<Map<String, dynamic>>? _loads;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    try {
      final res = await ref.read(dioProvider).get<dynamic>('loads/mine');
      if (mounted) setState(() => _loads = (res.data['data'] as List).cast<Map<String, dynamic>>());
    } on DioException {
      if (mounted) setState(() => _loads = []);
    }
  }

  int _count(bool Function(int status) test) =>
      _loads?.where((l) => test((l['status'] as num?)?.toInt() ?? -1)).length ?? 0;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final email = ref.watch(currentEmailProvider) ?? 'Load Owner';
    final name = email.split('@').first;

    // LoadStatus: 1 Posted(open), 3 Booked + 4 InTransit (running), 5 Delivered (completed).
    final open = _count((s) => s == 1);
    final running = _count((s) => s == 3 || s == 4);
    final completed = _count((s) => s == 5);
    final loading = _loads == null;

    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _fetch,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(AppTheme.radius),
                  gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [AppColors.primary, AppColors.navy]),
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
              const SizedBox(height: 16),
              Row(children: [
                Expanded(child: _stat(context, Icons.inventory_2, loading ? '…' : '$open', 'Open loads', AppColors.warning)),
                const SizedBox(width: 12),
                Expanded(child: _stat(context, Icons.local_shipping, loading ? '…' : '$running', 'Running', AppColors.primary)),
                const SizedBox(width: 12),
                Expanded(child: _stat(context, Icons.check_circle, loading ? '…' : '$completed', 'Delivered', AppColors.success)),
              ]),
              const SizedBox(height: 16),
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
                  subtitle: const Text('Track loads and choose drivers'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/my-loads'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _stat(BuildContext c, IconData icon, String value, String label, Color color) => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(padding: const EdgeInsets.all(7), decoration: BoxDecoration(color: color.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(10)), child: Icon(icon, color: color, size: 18)),
              const SizedBox(height: 10),
              Text(value, style: Theme.of(c).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
              Text(label, style: Theme.of(c).textTheme.bodySmall?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant), maxLines: 1, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      );
}
