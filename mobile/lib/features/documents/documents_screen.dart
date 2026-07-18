import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/status_pill.dart';

/// Driver documents — upload licence/RC/insurance/permit/fitness from camera or gallery.
/// Uploads target the driver's profile (resolved from the drivers list for this demo);
/// Operations verifies them in the admin console.
class DocumentsScreen extends ConsumerStatefulWidget {
  const DocumentsScreen({super.key});
  static const routePath = '/documents';
  static const routeName = 'documents';

  @override
  ConsumerState<DocumentsScreen> createState() => _DocumentsScreenState();
}

class _DocumentsScreenState extends ConsumerState<DocumentsScreen> {
  // (label, DocumentType int)
  static const _docs = [
    ('Driving Licence', 3),
    ('RC Book', 1),
    ('Insurance', 2),
    ('Permit', 4),
    ('Fitness Certificate', 5),
  ];

  final Map<int, String> _status = {};
  bool _busy = false;

  Future<String?> _driverId() async {
    try {
      final res = await ref.read(dioProvider).get<dynamic>('drivers');
      final list = res.data['data'] as List;
      return list.isEmpty ? null : list.first['id'] as String;
    } catch (_) {
      return null;
    }
  }

  Future<void> _upload(int type, ImageSource source) async {
    final XFile? file = await ImagePicker().pickImage(source: source, imageQuality: 70);
    if (file == null) return;
    setState(() => _busy = true);
    try {
      final id = await _driverId();
      if (id == null) {
        if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Register your driver profile first.')));
        return;
      }
      final bytes = await file.readAsBytes();
      final form = FormData.fromMap({
        'ownerType': 0, // Driver
        'ownerId': id,
        'type': type,
        'file': MultipartFile.fromBytes(bytes, filename: file.name),
      });
      await ref.read(dioProvider).post<dynamic>('documents/upload', data: form);
      setState(() => _status[type] = 'Submitted');
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Uploaded for review.')));
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Upload failed.')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _pick(int type) {
    showModalBottomSheet<void>(
      context: context,
      builder: (_) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          ListTile(leading: const Icon(Icons.photo_camera), title: const Text('Camera'), onTap: () { Navigator.pop(context); _upload(type, ImageSource.camera); }),
          ListTile(leading: const Icon(Icons.photo_library), title: const Text('Gallery'), onTap: () { Navigator.pop(context); _upload(type, ImageSource.gallery); }),
        ]),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Documents')),
      body: Stack(children: [
        ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: _docs.length,
          separatorBuilder: (_, __) => const SizedBox(height: 12),
          itemBuilder: (context, i) {
            final (label, type) = _docs[i];
            final status = _status[type] ?? 'NotSubmitted';
            return Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(children: [
                  Container(padding: const EdgeInsets.all(10), decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)), child: const Icon(Icons.description, color: AppColors.primary)),
                  const SizedBox(width: 14),
                  Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
                    const SizedBox(height: 6),
                    StatusPill(status),
                  ])),
                  FilledButton.tonalIcon(onPressed: _busy ? null : () => _pick(type), icon: const Icon(Icons.upload), label: const Text('Upload')),
                ]),
              ),
            );
          },
        ),
        if (_busy) const Positioned.fill(child: ColoredBox(color: Colors.black26, child: Center(child: CircularProgressIndicator()))),
      ]),
    );
  }
}
