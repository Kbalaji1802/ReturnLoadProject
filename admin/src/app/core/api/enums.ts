/**
 * Enum → label maps mirroring the backend domain enums. The API serialises enums as
 * integers, so the console maps them to human labels for display + status chips.
 * (Frontend-only; no API change.)
 */
export const DRIVER_STATUS: Record<number, string> = { 0: 'Pending', 1: 'Active', 2: 'Suspended', 3: 'Blocked' };

export const VERIFICATION_STATUS: Record<number, string> = {
  0: 'NotSubmitted', 1: 'Submitted', 2: 'UnderReview', 3: 'Verified', 4: 'Rejected', 5: 'Expired',
};

export const LOAD_STATUS: Record<number, string> = {
  0: 'Draft', 1: 'Open', 2: 'Matched', 3: 'Assigned', 4: 'In transit', 5: 'Delivered', 6: 'Cancelled',
};

export const TRIP_STATUS: Record<number, string> = {
  0: 'Created', 1: 'Driver accepted', 2: 'En route', 3: 'Arrived pickup', 4: 'Loaded',
  5: 'In transit', 6: 'Arrived destination', 7: 'Unloaded', 8: 'Completed', 9: 'Cancelled',
  10: 'Pickup confirmed', 11: 'Delivery confirmed',
};

/** TripStatus integers in true lifecycle order (the confirmation gates 10/11 sit in position). */
export const TRIP_LIFECYCLE_ORDER: number[] = [0, 1, 2, 3, 10, 4, 5, 6, 7, 11, 8];

/** A trip is "running" for dashboard/detail purposes when it is active (not Created/Completed/Cancelled). */
export const isRunningTrip = (status: number): boolean => status !== 0 && status !== 8 && status !== 9;

export const VEHICLE_STATUS: Record<number, string> = { 0: 'Pending', 1: 'Verified', 2: 'Maintenance', 3: 'Suspended' };

export const DRIVER_AVAILABILITY: Record<number, string> = {
  0: 'Available', 1: 'Busy', 2: 'Offline', 3: 'On leave', 4: 'Vehicle service',
};

export const AREA_TYPE: Record<number, string> = { 0: 'Urban', 1: 'Suburban', 2: 'Highway' };

export const VEHICLE_TYPE: Record<number, string> = {
  0: 'Open body', 1: 'Closed container', 2: 'Flatbed', 3: 'Reefer', 4: 'Tanker', 5: 'Tipper',
  6: 'Light commercial', 7: 'Trailer', 99: 'Other',
};

export const BOOKING_STATUS: Record<number, string> = { 0: 'Pending', 1: 'Approved', 2: 'Rejected', 3: 'Withdrawn' };

export const DOCUMENT_TYPE: Record<number, string> = {
  0: 'Driver KYC', 1: 'Registration Certificate', 2: 'Insurance', 3: 'Driving Licence', 4: 'Permit',
  5: 'Fitness Certificate', 6: 'Pollution Certificate', 7: 'Proof of Delivery', 99: 'Other',
};

export const DOCUMENT_OWNER: Record<number, string> = { 0: 'Driver', 1: 'Vehicle', 2: 'Carrier' };

export const CARGO_TYPE: Record<number, string> = {
  0: 'General', 1: 'Perishable', 2: 'Fragile', 3: 'Hazardous', 4: 'Construction', 5: 'Liquid',
  6: 'Refrigerated', 99: 'Other',
};

export const label = (map: Record<number, string>, value: number): string => map[value] ?? String(value);
