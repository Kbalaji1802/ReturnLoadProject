import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/enums.dart';
import '../../services/dio_client.dart';
import '../../shared/theme/app_theme.dart';
import '../../shared/widgets/skeleton.dart';
import '../../shared/widgets/status_pill.dart';

/// Driver documents (correction-sprint Part 8). Loads each document's REAL verification status from
/// the API; when Operations rejects one, the driver sees the reason and can upload a replacement
/// (which archives the old one server-side). No local/optimistic status — always the server's truth.
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

  String? _driverProfileId;
  /// Latest Active document per DocumentType int (the current one; replaced docs are archived).
  Map<int, Map<String, dynamic>> _byType = {};
  bool _loading = true;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    final dio = ref.read(dioProvider);
    try {
      final me = await dio.get<dynamic>('drivers/me');
      _driverProfileId = (me.data['data'] as Map<String, dynamic>)['id'] as String;
    } on DioException {
      if (mounted) setState(() { _loading = false; _error = 'Register your driver profile first.'; });
      return;
    }

    try {
      final res = await dio.get<dynamic>('documents', queryParameters: {'ownerType': 0, 'ownerId': _driverProfileId});
      final list = (res.data['data'] as List).cast<Map<String, dynamic>>();
      final map = <int, Map<String, dynamic>>{};
      for (final d in list) {
        // status 0 == Active (1 == Archived). Keep the current, live document per type.
        if ((d['status'] as num?)?.toInt() == 0) {
          map[(d['type'] as num).toInt()] = d;
        }
      }
      if (mounted) setState(() { _byType = map; _loading = false; });
    } catch (_) {
      if (mounted) setState(() { _loading = false; _error = 'Could not load your documents.'; });
    }
  }

  Future<void> _upload(int type, ImageSource source) async {
    final XFile? file = await ImagePicker().pickImage(source: source, imageQuality: 70);
    if (file == null) return;
    setState(() => _busy = true);
    try {
      final bytes = await file.readAsBytes();
      // The server resolves the owning driver from the auth token; the client never sends an
      // owner id, so a document can only attach to the signed-in driver (M4.2 security fix).
      final form = FormData.fromMap({
        'type': type,
        'file': MultipartFile.fromBytes(bytes, filename: file.name),
      });
      await ref.read(dioProvider).post<dynamic>('documents/driver-upload', data: form);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Uploaded for review.')));
      await _load(); // refresh from the server — never track status locally
    } on DioException catch (e) {
      final message = e.response?.statusCode == 404 ? 'Register your driver profile first.' : 'Upload failed.';
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
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
        RefreshIndicator(
          onRefresh: _load,
          child: _loading
              ? const Padding(padding: EdgeInsets.all(16), child: SkeletonList())
              : _error != null
                  ? _errorState()
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _docs.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 12),
                      itemBuilder: (context, i) => _docCard(_docs[i].$1, _docs[i].$2),
                    ),
        ),
        if (_busy) const Positioned.fill(child: ColoredBox(color: Colors.black26, child: Center(child: CircularProgressIndicator()))),
      ]),
    );
  }

  Widget _docCard(String label, int type) {
    final doc = _byType[type];
    final int vStatus = (doc?['verificationStatus'] as num?)?.toInt() ?? 0; // 0 NotSubmitted
    final rejected = vStatus == 4;
    final submitted = doc != null && (vStatus == 1 || vStatus == 2);
    final verified = vStatus == 3;
    final reason = doc?['rejectionReason'] as String?;

    final String buttonLabel = doc == null
        ? 'Upload'
        : rejected
            ? 'Re-upload corrected copy'
            : verified
                ? 'Replace'
                : 'Replace';

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(children: [
              Container(padding: const EdgeInsets.all(10), decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)), child: const Icon(Icons.description, color: AppColors.primary)),
              const SizedBox(width: 14),
              Expanded(child: Text(label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16))),
              const SizedBox(width: 8),
              StatusPill(labelOf(verificationStatus, doc == null ? 0 : vStatus)),
            ]),
            if (rejected && reason != null && reason.isNotEmpty) ...[
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: AppColors.error.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(12)),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const Icon(Icons.error_outline, size: 18, color: AppColors.error),
                  const SizedBox(width: 8),
                  Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    const Text('Rejected by our team', style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.error)),
                    const SizedBox(height: 2),
                    Text(reason, style: Theme.of(context).textTheme.bodySmall),
                  ])),
                ]),
              ),
            ],
            const SizedBox(height: 14),
            if (submitted && !rejected)
              const Text('Awaiting review — you can replace it if needed.', style: TextStyle(fontSize: 12))
            else if (verified)
              const Text('Verified ✓', style: TextStyle(fontSize: 12, color: AppColors.success)),
            const SizedBox(height: 8),
            FilledButton.tonalIcon(
              onPressed: _busy ? null : () => _pick(type),
              icon: Icon(rejected ? Icons.refresh : Icons.upload),
              label: Text(buttonLabel),
            ),
          ],
        ),
      ),
    );
  }

  Widget _errorState() => ListView(children: [
        const SizedBox(height: 100),
        Icon(Icons.description_outlined, size: 64, color: Theme.of(context).colorScheme.outline),
        const SizedBox(height: 12),
        Center(child: Text(_error!, style: Theme.of(context).textTheme.titleMedium)),
      ]);
}
