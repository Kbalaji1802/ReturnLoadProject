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
  licence: string;
  status: number;
}

export interface DocumentView {
  id: string;
  ownerType: number;
  ownerId: string;
  type: number;
  verificationStatus: number;
  expiresOn: string | null;
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
}

/** Payload of GET /api/v1/health. */
export interface HealthStatus {
  status: string;
  service: string;
  timestampUtc: string;
}
