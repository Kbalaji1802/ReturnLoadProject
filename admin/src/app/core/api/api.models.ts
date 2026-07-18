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

export interface DriverSummary {
  id: string;
  userProfileId: string;
  licence: string;
  status: string;
}

export interface DocumentView {
  id: string;
  ownerType: string;
  ownerId: string;
  type: string;
  verificationStatus: string;
  expiresOn: string | null;
}

export interface LoadView {
  id: string;
  shipperId: string;
  originAddress: string | null;
  destinationAddress: string | null;
  pickupStart: string;
  pickupEnd: string;
  cargoType: string;
  weightKg: number;
  offeredPriceInr: number | null;
  status: string;
}

/** Payload of GET /api/v1/health. */
export interface HealthStatus {
  status: string;
  service: string;
  timestampUtc: string;
}
