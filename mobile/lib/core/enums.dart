/// Enum → label maps mirroring the backend domain enums (API serialises them as integers).
const cargoType = {0: 'General', 1: 'Perishable', 2: 'Fragile', 3: 'Hazardous', 4: 'Construction', 5: 'Liquid', 6: 'Refrigerated', 99: 'Other'};
const loadStatus = {0: 'Draft', 1: 'Open', 2: 'Matched', 3: 'Assigned', 4: 'In transit', 5: 'Delivered', 6: 'Cancelled'};
const vehicleStatus = {0: 'Pending review', 1: 'Verified', 2: 'Maintenance', 3: 'Suspended'};
const bookingStatus = {0: 'Pending', 1: 'Approved', 2: 'Rejected', 3: 'Withdrawn'};
const verificationStatus = {0: 'NotSubmitted', 1: 'Submitted', 2: 'UnderReview', 3: 'Verified', 4: 'Rejected', 5: 'Expired'};

/// The trip lifecycle (M4.3/M4.4). Index == the backend TripStatus integer.
const tripStatus = {
  0: 'Created', 1: 'Driver accepted', 2: 'En route', 3: 'Arrived pickup',
  4: 'Loaded', 5: 'In transit', 6: 'Arrived destination', 7: 'Unloaded',
  8: 'Completed', 9: 'Cancelled',
};

/// The ordered forward lifecycle as backend enum NAMES (what the advance API expects in the path).
const tripLifecycle = [
  'Created', 'DriverAccepted', 'DriverEnRoute', 'ArrivedPickup', 'Loaded',
  'InTransit', 'ArrivedDestination', 'Unloaded', 'Completed',
];

/// Friendly action label for advancing INTO each lifecycle state (driver's next button).
const tripActionLabel = {
  'DriverAccepted': 'Accept trip',
  'DriverEnRoute': 'Start journey',
  'ArrivedPickup': 'Arrived at pickup',
  'Loaded': 'Mark loaded',
  'InTransit': 'Start transit',
  'ArrivedDestination': 'Arrived at destination',
  'Unloaded': 'Mark unloaded',
  'Completed': 'Complete trip',
};

String labelOf(Map<int, String> map, Object? value) {
  final n = value is int ? value : int.tryParse('$value');
  return n == null ? '$value' : (map[n] ?? '$value');
}

/// The next lifecycle enum name after the given status integer, or null if terminal.
String? nextTripAction(Object? statusValue) {
  final i = statusValue is int ? statusValue : int.tryParse('$statusValue');
  if (i == null || i < 0 || i >= tripLifecycle.length - 1) return null;
  return tripLifecycle[i + 1];
}
