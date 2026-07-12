/** Success envelope returned by the ReturnLoad API (mirrors backend ApiResponse<T>). */
export interface ApiEnvelope<T> {
  data: T;
}

/** Payload of GET /api/v1/health (mirrors backend HealthStatusResponse). */
export interface HealthStatus {
  status: string;
  service: string;
  timestampUtc: string;
}
