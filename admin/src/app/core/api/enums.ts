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
  0: 'Draft', 1: 'Posted', 2: 'Matched', 3: 'Booked', 4: 'InTransit', 5: 'Delivered', 6: 'Cancelled',
};

export const TRIP_STATUS: Record<number, string> = {
  0: 'Created', 1: 'Assigned', 2: 'Started', 3: 'InTransit', 4: 'Completed', 5: 'Cancelled',
};

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
