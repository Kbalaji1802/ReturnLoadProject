import 'package:flutter/material.dart';

import '../../shared/theme/app_theme.dart';

/// Notification feed. Push delivery isn't wired yet (no provider configured), so this
/// shows recent local activity styled as it will appear once notifications are live.
class NotificationsTab extends StatelessWidget {
  const NotificationsTab({super.key});

  static const _items = [
    (_N('Document approved', 'Your driving licence was verified.', '2m', Icons.verified, AppColors.success, true)),
    (_N('New load near you', 'Chennai → Coimbatore · 5,000 kg', '18m', Icons.inventory_2, AppColors.primary, true)),
    (_N('Trip completed', 'Coimbatore → Chennai marked complete.', '3h', Icons.flag, AppColors.navy, false)),
    (_N('Insurance expiring', 'Renew before it lapses to stay matchable.', '1d', Icons.warning_amber, AppColors.warning, false)),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: _items.length,
        separatorBuilder: (_, __) => const SizedBox(height: 12),
        itemBuilder: (context, i) {
          final n = _items[i];
          return Card(
            child: ListTile(
              contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
              leading: CircleAvatar(backgroundColor: n.color.withValues(alpha: 0.15), child: Icon(n.icon, color: n.color)),
              title: Row(children: [
                Expanded(child: Text(n.title, style: const TextStyle(fontWeight: FontWeight.w600))),
                if (n.unread) Container(width: 8, height: 8, decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle)),
              ]),
              subtitle: Text(n.body),
              trailing: Text(n.time, style: Theme.of(context).textTheme.bodySmall),
            ),
          );
        },
      ),
    );
  }
}

class _N {
  const _N(this.title, this.body, this.time, this.icon, this.color, this.unread);
  final String title;
  final String body;
  final String time;
  final IconData icon;
  final Color color;
  final bool unread;
}
