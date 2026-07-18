import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class TimelineStop {
  const TimelineStop(this.title, this.subtitle, {this.done = false});
  final String title;
  final String subtitle;
  final bool done;
}

/// A vertical pickup → drop timeline used on load details and trips.
class RouteTimeline extends StatelessWidget {
  const RouteTimeline(this.stops, {super.key});
  final List<TimelineStop> stops;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Column(
      children: [
        for (int i = 0; i < stops.length; i++)
          IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Column(
                  children: [
                    Container(
                      width: 16, height: 16,
                      decoration: BoxDecoration(
                        color: stops[i].done ? AppColors.success : AppColors.primary,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 2),
                      ),
                    ),
                    if (i != stops.length - 1)
                      Expanded(child: Container(width: 2, color: Theme.of(context).colorScheme.outlineVariant)),
                  ],
                ),
                const SizedBox(width: 12),
                Padding(
                  padding: const EdgeInsets.only(bottom: 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(stops[i].title, style: text.titleSmall?.copyWith(fontWeight: FontWeight.w600)),
                      Text(stops[i].subtitle, style: text.bodySmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    ],
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
