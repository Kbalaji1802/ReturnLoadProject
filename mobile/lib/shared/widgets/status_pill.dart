import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// A small coloured status pill mapping a domain status label to a tone.
class StatusPill extends StatelessWidget {
  const StatusPill(this.label, {super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    final (Color color, IconData icon) = _style(label);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: color),
          const SizedBox(width: 5),
          Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12)),
        ],
      ),
    );
  }

  (Color, IconData) _style(String s) {
    switch (s) {
      case 'Active':
      case 'Verified':
      case 'Completed':
      case 'Delivered':
      case 'Booked':
        return (AppColors.success, Icons.check_circle);
      case 'Pending':
      case 'Submitted':
      case 'UnderReview':
      case 'Maintenance':
        return (AppColors.warning, Icons.schedule);
      case 'Rejected':
      case 'Expired':
      case 'Suspended':
      case 'Blocked':
      case 'Cancelled':
        return (AppColors.error, Icons.error);
      default:
        return (AppColors.primary, Icons.sync);
    }
  }
}
