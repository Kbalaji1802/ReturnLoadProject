import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';
import '../../shared/widgets/status_pill.dart';

/// The driver's fleet: lists their vehicles (with verification status) and lets them add one.
/// Adding a vehicle creates an owner-operator carrier server-side if needed (M4.4).
class VehicleScreen extends ConsumerStatefulWidget {
  const VehicleScreen({super.key});
  static const routePath = '/vehicle';
  static const routeName = 'vehicle';

  @override
  ConsumerState<VehicleScreen> createState() => _VehicleScreenState();
}

class _VehicleScreenState extends ConsumerState<VehicleScreen> {
  List<dynamic>? _vehicles;
  String? _error;

  static const _types = {
    0: 'Open body', 1: 'Closed container', 2: 'Flatbed', 3: 'Reefer',
    4: 'Tanker', 5: 'Tipper', 6: 'Light commercial', 7: 'Trailer', 99: 'Other',
  };

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final res = await ref.read(dioProvider).get<dynamic>('vehicles/mine');
      if (mounted) setState(() => _vehicles = res.data['data'] as List);
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load your vehicles.'; _vehicles = []; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final list = _vehicles;
    return Scaffold(
      appBar: AppBar(title: const Text('My vehicles')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _addVehicle,
        icon: const Icon(Icons.add),
        label: const Text('Add vehicle'),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: list == null
            ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
            : list.isEmpty
                ? _empty()
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 90),
                    itemCount: list.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (context, i) => _card(list[i] as Map<String, dynamic>),
                  ),
      ),
    );
  }

  Widget _card(Map<String, dynamic> v) {
    final text = Theme.of(context).textTheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(children: [
          Container(padding: const EdgeInsets.all(12), decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.14), borderRadius: BorderRadius.circular(14)), child: const Icon(Icons.local_shipping, color: AppColors.primary, size: 26)),
          const SizedBox(width: 14),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(v['registrationNumber'] ?? '—', style: text.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            const SizedBox(height: 2),
            Text('${labelOf(_types, v['type'])} · ${v['maxPayloadKg']} kg', style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
          ])),
          StatusPill(labelOf(vehicleStatus, v['status'])),
        ]),
      ),
    );
  }

  Widget _empty() => ListView(children: [
        const SizedBox(height: 100),
        Icon(Icons.local_shipping_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error ?? 'No vehicles yet', style: Theme.of(context).textTheme.titleMedium)),
        const SizedBox(height: 4),
        const Center(child: Text('Add your truck to start requesting loads')),
      ]);

  Future<void> _addVehicle() async {
    final reg = TextEditingController();
    final payload = TextEditingController(text: '10000');
    int type = 0;
    final formKey = GlobalKey<FormState>();

    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(sheetContext).viewInsets.bottom),
        child: StatefulBuilder(
          builder: (c, setSheet) => Padding(
            padding: const EdgeInsets.all(20),
            child: Form(
              key: formKey,
              child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                Text('Add vehicle', style: Theme.of(c).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
                const SizedBox(height: 16),
                TextFormField(
                  controller: reg,
                  textCapitalization: TextCapitalization.characters,
                  decoration: const InputDecoration(labelText: 'Registration number (e.g. TN01AB1234)', border: OutlineInputBorder()),
                  validator: (v) => (v == null || v.trim().length < 6) ? 'Enter a valid registration' : null,
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<int>(
                  initialValue: type,
                  decoration: const InputDecoration(labelText: 'Vehicle type', border: OutlineInputBorder()),
                  items: _types.entries.map((e) => DropdownMenuItem(value: e.key, child: Text(e.value))).toList(),
                  onChanged: (v) => setSheet(() => type = v ?? 0),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: payload,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Max payload (kg)', border: OutlineInputBorder()),
                  validator: (v) => (double.tryParse(v ?? '') ?? 0) <= 0 ? 'Enter the payload capacity' : null,
                ),
                const SizedBox(height: 20),
                FilledButton(
                  onPressed: () { if (formKey.currentState!.validate()) Navigator.pop(sheetContext, true); },
                  child: const Text('Add vehicle'),
                ),
                const SizedBox(height: 8),
              ]),
            ),
          ),
        ),
      ),
    );

    if (ok != true) return;
    try {
      await ref.read(dioProvider).post<dynamic>('vehicles/mine', data: {
        'registrationNumber': reg.text.trim().toUpperCase(),
        'type': type,
        'maxPayloadKg': double.tryParse(payload.text) ?? 0,
        'volumeCubicMetres': null,
      });
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Vehicle added — pending review.')));
      await _load();
    } on DioException catch (e) {
      final msg = e.response?.statusCode == 400 ? 'Register your driver profile first.' : 'Could not add the vehicle.';
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
    }
  }
}
