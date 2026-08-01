import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../services/dio_client.dart';
import '../theme/app_theme.dart';

/// Post-trip rating dialog (M8). Collects 1–5 stars + an optional comment and submits it to
/// POST /reviews/trip/{tripId}. Returns true if a review was submitted.
Future<bool> showRatingDialog(BuildContext context, WidgetRef ref, String tripId, String subjectLabel) async {
  int stars = 5;
  final comment = TextEditingController();
  bool busy = false;

  final submitted = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => StatefulBuilder(
      builder: (c, setLocal) => AlertDialog(
        title: Text('Rate $subjectLabel'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(mainAxisAlignment: MainAxisAlignment.center, children: [
            for (int i = 1; i <= 5; i++)
              IconButton(
                onPressed: busy ? null : () => setLocal(() => stars = i),
                icon: Icon(i <= stars ? Icons.star : Icons.star_border, color: AppColors.warning, size: 32),
              ),
          ]),
          const SizedBox(height: 8),
          TextField(
            controller: comment,
            maxLines: 3,
            decoration: const InputDecoration(labelText: 'Comment (optional)', border: OutlineInputBorder()),
          ),
        ]),
        actions: [
          TextButton(onPressed: busy ? null : () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
          FilledButton(
            onPressed: busy
                ? null
                : () async {
                    setLocal(() => busy = true);
                    try {
                      await ref.read(dioProvider).post<dynamic>('reviews/trip/$tripId', data: {
                        'stars': stars,
                        'comment': comment.text.trim().isEmpty ? null : comment.text.trim(),
                      });
                      if (dialogContext.mounted) Navigator.pop(dialogContext, true);
                    } catch (_) {
                      setLocal(() => busy = false);
                      if (dialogContext.mounted) {
                        ScaffoldMessenger.of(dialogContext).showSnackBar(const SnackBar(content: Text('Could not submit the review.')));
                      }
                    }
                  },
            child: busy ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Submit'),
          ),
        ],
      ),
    ),
  );

  return submitted ?? false;
}
