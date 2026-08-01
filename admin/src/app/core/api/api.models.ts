/** One error entry inside the API envelope (mirrors backend ApiError). */
export interface ApiError {
  field: string | null;
  code: string;
  message: string;
}

/** The envelope returned by every ReturnLoad API endpoint (mirrors backend ApiResponse<T>). */
export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T;
  errors: ApiError[];
  traceId: string;
}

export interface AuthTokens {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

// Enum fields are serialised by the API as integers; the console maps them via core/api/enums.ts.
export interface DriverSummary {
  id: string;
  userProfileId: string;
  fullName: string | null;
  licence: string;
  status: number;
  availability: number;
}

/** GET /documents/pending — enriched review-queue row (backend PendingDocumentView, Part 1). */
export interface PendingDocumentView {
  id: string;
  ownerType: number;
  ownerId: string;
  type: number;
  documentNumber: string | null;
  verificationStatus: number;
  expiresOn: string | null;
  uploadedAtUtc: string;
  driverName: string | null;
  driverPhotoUrl: string | null;
  company: string | null;
  vehicleRegistration: string | null;
}

/** GET /documents?ownerType&ownerId — a single owner's document (driver/vehicle-facing). */
export interface DocumentView {
  id: string;
  ownerType: number;
  ownerId: string;
  type: number;
  documentNumber: string | null;
  verificationStatus: number;
  status: number;
  expiresOn: string | null;
  uploadedAtUtc: string;
  rejectionReason: string | null;
}

/** One entry in a document's append-only review history (GET /documents/{id}/history). */
export interface DocumentReviewView {
  decision: number;
  reason: string | null;
  decidedByUserId: string;
  decidedAtUtc: string;
}

export interface VehicleView {
  id: string;
  carrierId: string;
  registrationNumber: string;
  type: number;
  maxPayloadKg: number;
  status: number;
}

/** GET /drivers/{id} — the admin driver-details aggregate (Part 11). */
export interface DriverDetailView {
  id: string;
  userProfileId: string;
  fullName: string;
  mobile: string;
  email: string | null;
  photoUrl: string | null;
  status: number;
  availability: number;
  licence: string;
  rating: number | null;
  ratingCount: number;
  completedTrips: number;
  currentTripId: string | null;
  currentTripStatus: number | null;
  company: string | null;
  lastKnownLatitude: number | null;
  lastKnownLongitude: number | null;
  vehicles: VehicleView[];
}

/** GET /vehicles/{id} — the admin vehicle-details aggregate (Part 11). */
export interface VehicleDetailView {
  id: string;
  registrationNumber: string;
  type: number;
  maxPayloadKg: number;
  volumeCubicMetres: number | null;
  status: number;
  carrierId: string;
  company: string | null;
  createdAtUtc: string;
}

/** GET /trips/{id}/detail — the admin trip-details aggregate (Part 11). */
export interface TripDetailView {
  id: string;
  status: number;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  statusChangedAtUtc: string;
  pickupAutoConfirmed: boolean;
  deliveryAutoConfirmed: boolean;
  driverProfileId: string;
  driverName: string | null;
  vehicleId: string;
  vehicleRegistration: string | null;
  loadId: string | null;
  originAddress: string | null;
  destinationAddress: string | null;
  originLat: number;
  originLng: number;
  destinationLat: number;
  destinationLng: number;
}

/** GET /reviews/for-user/{id} and /reviews/trip/{id}. */
export interface ReviewView {
  id: string;
  tripId: string;
  authorUserProfileId: string;
  subjectUserProfileId: string;
  stars: number;
  comment: string | null;
  createdAtUtc: string;
}

/** GET /bookings and /bookings/for-load/{id} — enriched candidate (backend BookingRequestView, Part 7). */
export interface BookingRequestView {
  id: string;
  loadId: string;
  driverProfileId: string;
  vehicleId: string;
  status: number;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  driverName: string | null;
  vehicleRegistration: string | null;
  driverRating: number | null;
  completedTrips: number;
  distanceFromPickupKm: number | null;
  etaToPickupMinutes: number | null;
  verificationStatus: number | null;
  availability: number | null;
}

/** GET /trips/{id}/tracking/live — live position + ETA. */
export interface TripLiveView {
  tripId: string;
  status: number;
  hasLocation: boolean;
  latitude: number | null;
  longitude: number | null;
  lastUpdateUtc: string | null;
  distanceRemainingKm: number | null;
  etaMinutes: number | null;
}

/** GET /trips/{id}/tracking — one breadcrumb point. */
export interface TrackingPointView {
  id: string;
  latitude: number;
  longitude: number;
  capturedAtUtc: string;
  speedKph: number | null;
  headingDegrees: number | null;
  accuracyMetres: number | null;
}

export interface TripView {
  id: string;
  carrierId: string;
  vehicleId: string;
  driverProfileId: string;
  status: number;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  originLat: number;
  originLng: number;
  destinationLat: number;
  destinationLng: number;
}

export interface LoadView {
  id: string;
  shipperId: string;
  originAddress: string | null;
  destinationAddress: string | null;
  pickupStart: string;
  pickupEnd: string;
  cargoType: number;
  weightKg: number;
  offeredPriceInr: number | null;
  status: number;
  distanceKm: number | null;
  estimatedDurationMinutes: number | null;
  pickupAreaType: number;
}

/** Payload of GET /api/v1/health. */
export interface HealthStatus {
  status: string;
  service: string;
  timestampUtc: string;
}
