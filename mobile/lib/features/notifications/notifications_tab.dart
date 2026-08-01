import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';

/// In-app notifications (M7): the user's inbox, read by polling GET /notifications. Tapping an
/// item marks it read; the app bar marks all read.
class NotificationsTab extends ConsumerStatefulWidget {
  const NotificationsTab({super.key});
  @override
  ConsumerState<NotificationsTab> createState() => _NotificationsTabState();
}

class _NotificationsTabState extends ConsumerState<NotificationsTab> {
  List<dynamic>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('notifications');
      if (mounted) setState(() => _items = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load notifications.'; _items = []; });
    }
  }

  Future<void> _markRead(String id) async {
    try {
      await ref.read(dioProvider).post<dynamic>('notifications/$id/read');
      await _load();
    } catch (_) {/* ignore */}
  }

  Future<void> _markAllRead() async {
    try {
      await ref.read(dioProvider).post<dynamic>('notifications/read-all');
      await _load();
    } catch (_) {/* ignore */}
  }

  @override
  Widget build(BuildContext context) {
    final items = _items;
    final hasUnread = items?.any((n) => n['read'] == false) ?? false;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          if (hasUnread) TextButton(onPressed: _markAllRead, child: const Text('Mark all read')),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: items == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : items.isEmpty
                ? _empty()
                : ListView.separated(
                    padding: const EdgeInsets.all(12),
                    itemCount: items.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, i) => _card(items[i] as Map<String, dynamic>),
                  ),
      ),
    );
  }

  Widget _card(Map<String, dynamic> n) {
    final unread = n['read'] == false;
    final text = Theme.of(context).textTheme;
    return Card(
      color: unread ? AppColors.primary.withValues(alpha: 0.06) : null,
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: (unread ? AppColors.primary : Theme.of(context).colorScheme.outline).withValues(alpha: 0.15),
          child: Icon(unread ? Icons.notifications_active : Icons.notifications_none,
              color: unread ? AppColors.primary : Theme.of(context).colorScheme.outline),
        ),
        title: Text(n['subject'] ?? '', style: text.titleSmall?.copyWith(fontWeight: unread ? FontWeight.w700 : FontWeight.w500)),
        subtitle: Text(n['body'] ?? ''),
        trailing: unread ? const Icon(Icons.circle, size: 10, color: AppColors.primary) : null,
        onTap: unread ? () => _markRead(n['id'] as String) : null,
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 120),
        Icon(Icons.notifications_none, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No notifications yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Updates about your loads and trips appear here')),
      ]);
}
