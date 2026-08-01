/// Enum → label maps mirroring the backend domain enums (API serialises them as integers).
const cargoType = {0: 'General', 1: 'Perishable', 2: 'Fragile', 3: 'Hazardous', 4: 'Construction', 5: 'Liquid', 6: 'Refrigerated', 99: 'Other'};
const loadStatus = {0: 'Draft', 1: 'Open', 2: 'Matched', 3: 'Assigned', 4: 'In transit', 5: 'Delivered', 6: 'Cancelled'};
const vehicleStatus = {0: 'Pending review', 1: 'Verified', 2: 'Maintenance', 3: 'Suspended'};
const bookingStatus = {0: 'Pending', 1: 'Approved', 2: 'Rejected', 3: 'Withdrawn'};
const verificationStatus = {0: 'NotSubmitted', 1: 'Submitted', 2: 'UnderReview', 3: 'Verified', 4: 'Rejected', 5: 'Expired'};

/// Driver verification/moderation lifecycle (DriverStatus). Distinct from availability below.
const driverStatus = {0: 'Pending', 1: 'Verified', 2: 'Suspended', 3: 'Blocked'};

/// Driver operational availability (correction-sprint Part 3). `Busy` is system-managed (set while
/// on a trip) — the driver picks the other four. Index == the backend DriverAvailability integer.
const driverAvailability = {0: 'Available', 1: 'Busy', 2: 'Offline', 3: 'On leave', 4: 'Vehicle service'};

/// The values a driver may pick (Busy is excluded — it is set automatically while on a trip).
const driverAvailabilityChoices = {0: 'Available', 2: 'Offline', 3: 'On leave', 4: 'Vehicle service'};

/// Pickup area type → the matching radius it maps to (Part 2). Backend AreaType integer.
const areaType = {0: 'Urban (≈5 km reach)', 1: 'Suburban (≈10 km reach)', 2: 'Highway (≈25 km reach)'};

/// The trip lifecycle (correction-sprint Part 5). Key == the backend TripStatus integer; the two
/// owner-confirmation gates (10, 11) were appended, so numeric value ≠ lifecycle position — use
/// [tripLifecycleOrder] for ordering, never the raw integer.
const tripStatus = {
  0: 'Created', 1: 'Driver accepted', 2: 'En route', 3: 'Arrived pickup',
  10: 'Pickup confirmed', 4: 'Loaded', 5: 'In transit', 6: 'Arrived destination',
  7: 'Unloaded', 11: 'Delivery confirmed', 8: 'Completed', 9: 'Cancelled',
};

/// Status integers in true lifecycle order (the confirmation gates sit where they belong, not at
/// their numeric value). Timeline + next-step logic index into THIS, not the raw status integer.
const tripLifecycleOrder = [0, 1, 2, 3, 10, 4, 5, 6, 7, 11, 8];

/// Status integer → backend enum NAME (what the advance API expects in the `/status/{name}` path).
const tripStatusName = {
  0: 'Created', 1: 'DriverAccepted', 2: 'DriverEnRoute', 3: 'ArrivedPickup',
  10: 'PickupConfirmed', 4: 'Loaded', 5: 'InTransit', 6: 'ArrivedDestination',
  7: 'Unloaded', 11: 'DeliveryConfirmed', 8: 'Completed', 9: 'Cancelled',
};

/// The two owner-confirmation gates — advanced by the load owner (or the driver after a wait).
const ownerConfirmStatuses = {10, 11};

/// Friendly action label for advancing INTO each state, keyed by the target status integer.
const tripActionLabel = {
  1: 'Accept trip', 2: 'Start journey', 3: 'Arrived at pickup', 10: 'Confirm pickup',
  4: 'Mark loaded', 5: 'Start transit', 6: 'Arrived at destination', 7: 'Mark unloaded',
  11: 'Confirm delivery', 8: 'Complete trip',
};

int? _asInt(Object? value) => value is int ? value : int.tryParse('$value');

String labelOf(Map<int, String> map, Object? value) {
  final n = _asInt(value);
  return n == null ? '$value' : (map[n] ?? '$value');
}

/// The next status integer in lifecycle order after [statusValue], or null if terminal.
int? nextTripStatus(Object? statusValue) {
  final i = _asInt(statusValue);
  if (i == null) return null;
  final pos = tripLifecycleOrder.indexOf(i);
  if (pos < 0 || pos >= tripLifecycleOrder.length - 1) return null;
  return tripLifecycleOrder[pos + 1];
}

/// A trip is active while not Completed(8) / Cancelled(9).
bool tripIsActive(Object? statusValue) {
  final i = _asInt(statusValue);
  return i != null && i != 8 && i != 9;
}

/// Position of a status in the ordered lifecycle (−1 if unknown/terminal-branch).
int tripLifecyclePosition(Object? statusValue) {
  final i = _asInt(statusValue);
  return i == null ? -1 : tripLifecycleOrder.indexOf(i);
}
