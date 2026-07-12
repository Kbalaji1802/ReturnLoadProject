import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/widgets.dart';

import 'app.dart';

void main() {
  // ProviderScope is the root of Riverpod's state graph for the whole app.
  runApp(const ProviderScope(child: ReturnLoadApp()));
}
