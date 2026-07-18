import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_providers.dart';
import '../../services/auth_repository.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/status_pill.dart';

class HomeTab extends ConsumerStatefulWidget {
  const HomeTab({super.key});
  @override
  ConsumerState<HomeTab> createState() => _HomeTabState();
}

class _HomeTabState extends ConsumerState<HomeTab> {
  int? _availableLoads;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    try {
      final res = await ref.read(dioProvider).get<dynamic>('loads/available');
      final list = (res.data['data'] as List).length;
      if (mounted) setState(() => _availableLoads = list);
    } catch (_) {
      if (mounted) setState(() => _availableLoads = 0);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final email = ref.watch(currentEmailProvider) ?? 'Driver';
    final name = email.split('@').first;

    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _fetch,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              // Gradient greeting header.
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(AppTheme.radius),
                  gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [AppColors.primary, AppColors.navy]),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      Text('${tr(ref, 'good_day')}, ', style: text.titleMedium?.copyWith(color: Colors.white70)),
                      Expanded(child: Text(name, style: text.titleLarge?.copyWith(color: Colors.white, fontWeight: FontWeight.w700), overflow: TextOverflow.ellipsis)),
                    ]),
                    const SizedBox(height: 12),
                    const StatusPill('Verified'),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              // Stat grid.
              GridView.count(
                crossAxisCount: 2, shrinkWrap: true, physics: const NeverScrollableScrollPhysics(),
                mainAxisSpacing: 12, crossAxisSpacing: 12, childAspectRatio: 1.55,
                children: [
                  _stat(context, Icons.payments, "₹0", tr(ref, 'todays_earnings'), AppColors.success),
                  _stat(context, Icons.route, '0', tr(ref, 'todays_trips'), AppColors.primary),
                  _stat(context, Icons.inventory_2, _availableLoads?.toString() ?? '…', tr(ref, 'available_loads'), AppColors.warning),
                  _stat(context, Icons.check_circle, 'Active', tr(ref, 'vehicle_status'), AppColors.navy),
                ],
              ),
              const SizedBox(height: 8),
              Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Text(tr(ref, 'quick_actions'), style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700))),
              Wrap(spacing: 12, runSpacing: 12, children: [
                _action(context, Icons.post_add, 'Post load', () => context.push('/post-load')),
                _action(context, Icons.description, 'Documents', () => context.push('/documents')),
                _action(context, Icons.local_shipping, 'Vehicle', () => context.push('/vehicle')),
                _action(context, Icons.map, 'Map', () => context.push('/map')),
              ]),
            ],
          ),
        ),
      ),
    );
  }

  Widget _stat(BuildContext c, IconData icon, String value, String label, Color color) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(padding: const EdgeInsets.all(8), decoration: BoxDecoration(color: color.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(12)), child: Icon(icon, color: color, size: 22)),
              const Spacer(),
              Text(value, style: Theme.of(c).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800)),
              Text(label, style: Theme.of(c).textTheme.bodySmall?.copyWith(color: Theme.of(c).colorScheme.onSurfaceVariant), maxLines: 1, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      );

  Widget _action(BuildContext c, IconData icon, String label, VoidCallback onTap) {
    final width = (MediaQuery.sizeOf(c).width - 16 * 2 - 12) / 2;
    return SizedBox(
      width: width.clamp(140, 260),
      child: Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 16),
            child: Row(children: [Icon(icon, color: AppColors.primary), const SizedBox(width: 10), Flexible(child: Text(label, overflow: TextOverflow.ellipsis))]),
          ),
        ),
      ),
    );
  }
}
