import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Light/dark theme mode (toggled from Settings/Profile).
final themeModeProvider = StateProvider<ThemeMode>((ref) => ThemeMode.light);

/// Selected language — English or Tamil (market languages, ADR-0004).
final languageProvider = StateProvider<String>((ref) => 'en');

/// Minimal translation table for the strings that appear in chrome/nav. Screens use
/// [tr] for these; full localisation would move to ARB + flutter_localizations later.
const Map<String, Map<String, String>> _strings = {
  'en': {
    'home': 'Home', 'loads': 'Loads', 'trips': 'Trips', 'notifications': 'Alerts', 'profile': 'Profile',
    'good_day': 'Welcome back', 'available_loads': 'Available loads', 'todays_earnings': "Today's earnings",
    'todays_trips': "Today's trips", 'vehicle_status': 'Vehicle', 'quick_actions': 'Quick actions',
  },
  'ta': {
    'home': 'முகப்பு', 'loads': 'சரக்குகள்', 'trips': 'பயணங்கள்', 'notifications': 'அறிவிப்புகள்', 'profile': 'சுயவிவரம்',
    'good_day': 'மீண்டும் வரவேற்கிறோம்', 'available_loads': 'கிடைக்கும் சரக்குகள்', 'todays_earnings': 'இன்றைய வருமானம்',
    'todays_trips': 'இன்றைய பயணங்கள்', 'vehicle_status': 'வாகனம்', 'quick_actions': 'விரைவு செயல்கள்',
  },
};

String tr(WidgetRef ref, String key) {
  final lang = ref.watch(languageProvider);
  return _strings[lang]?[key] ?? _strings['en']![key] ?? key;
}
