import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_providers.dart';
import '../../services/auth_repository.dart';
import '../../shared/theme/app_theme.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});
  static const routePath = '/settings';
  static const routeName = 'settings';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mode = ref.watch(themeModeProvider);
    final lang = ref.watch(languageProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(child: Column(children: [
            SwitchListTile(
              secondary: const Icon(Icons.dark_mode),
              title: const Text('Dark mode'),
              value: mode == ThemeMode.dark,
              onChanged: (v) => ref.read(themeModeProvider.notifier).state = v ? ThemeMode.dark : ThemeMode.light,
            ),
            const Divider(height: 1),
            ListTile(
              leading: const Icon(Icons.translate),
              title: const Text('Language'),
              trailing: Text(lang == 'ta' ? 'தமிழ்' : 'English'),
              onTap: () => _language(context, ref),
            ),
          ])),
          const SizedBox(height: 14),
          Card(child: Column(children: [
            _tile(context, Icons.privacy_tip, 'Privacy policy'),
            const Divider(height: 1),
            _tile(context, Icons.support_agent, 'Support'),
            const Divider(height: 1),
            ListTile(leading: const Icon(Icons.info_outline), title: const Text('About'), subtitle: const Text('ReturnLoad Driver · v0.4 (MVP)')),
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
    );
  }

  Widget _tile(BuildContext c, IconData icon, String title) => ListTile(
        leading: Icon(icon),
        title: Text(title),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => ScaffoldMessenger.of(c).showSnackBar(SnackBar(content: Text('$title — coming soon'))),
      );

  void _language(BuildContext context, WidgetRef ref) {
    final current = ref.read(languageProvider);
    void pick(String v) { ref.read(languageProvider.notifier).state = v; Navigator.pop(context); }
    showDialog<void>(
      context: context,
      builder: (_) => SimpleDialog(title: const Text('Language'), children: [
        ListTile(title: const Text('English'), trailing: current == 'en' ? const Icon(Icons.check, color: AppColors.primary) : null, onTap: () => pick('en')),
        ListTile(title: const Text('தமிழ் (Tamil)'), trailing: current == 'ta' ? const Icon(Icons.check, color: AppColors.primary) : null, onTap: () => pick('ta')),
      ]),
    );
  }
}
