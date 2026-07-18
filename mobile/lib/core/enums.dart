/// Enum → label maps mirroring the backend domain enums (API serialises them as integers).
const cargoType = {0: 'General', 1: 'Perishable', 2: 'Fragile', 3: 'Hazardous', 4: 'Construction', 5: 'Liquid', 6: 'Refrigerated', 99: 'Other'};
const loadStatus = {0: 'Draft', 1: 'Posted', 2: 'Matched', 3: 'Booked', 4: 'InTransit', 5: 'Delivered', 6: 'Cancelled'};
const tripStatus = {0: 'Created', 1: 'Assigned', 2: 'Started', 3: 'InTransit', 4: 'Completed', 5: 'Cancelled'};
const verificationStatus = {0: 'NotSubmitted', 1: 'Submitted', 2: 'UnderReview', 3: 'Verified', 4: 'Rejected', 5: 'Expired'};

String labelOf(Map<int, String> map, Object? value) {
  final n = value is int ? value : int.tryParse('$value');
  return n == null ? '$value' : (map[n] ?? '$value');
}
