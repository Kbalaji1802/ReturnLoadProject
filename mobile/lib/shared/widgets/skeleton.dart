import 'package:flutter/material.dart';
import 'package:shimmer/shimmer.dart';

/// A shimmering placeholder list used while data loads.
class SkeletonList extends StatelessWidget {
  const SkeletonList({super.key, this.count = 4, this.height = 96});

  final int count;
  final double height;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Shimmer.fromColors(
      baseColor: scheme.surfaceContainerHighest,
      highlightColor: scheme.surfaceContainerHigh,
      child: Column(
        children: List.generate(
          count,
          (_) => Container(
            height: height,
            margin: const EdgeInsets.only(bottom: 14),
            decoration: BoxDecoration(color: scheme.surface, borderRadius: BorderRadius.circular(18)),
          ),
        ),
      ),
    );
  }
}
