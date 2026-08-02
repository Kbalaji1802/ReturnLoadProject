import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiEnvelope } from './api.models';

/**
 * Thin, typed gateway to the ReturnLoad REST API. Centralises the base URL and unwraps the
 * standard success envelope so feature code works with the payload directly. Auth headers
 * are attached by the HTTP interceptor, not here.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  get<T>(path: string): Observable<T> {
    return this.http
      .get<ApiEnvelope<T>>(`${this.baseUrl}/${path}`)
      .pipe(map((r) => unwrap(r)));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<ApiEnvelope<T>>(`${this.baseUrl}/${path}`, body)
      .pipe(map((r) => unwrap(r)));
  }

  /**
   * Fetches a raw binary payload (e.g. a document file) with the auth header attached by the
   * interceptor. Used by the document preview/download — the response is NOT the JSON envelope.
   */
  getBlob(path: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${path}`, { responseType: 'blob' });
  }
}

/**
 * The API's own explanation of a failure, for display.
 *
 * Every error the API returns carries a human-readable message in the standard envelope — the
 * rejection-reason minimum, an illegal trip transition, a validation failure. Replacing that with
 * a generic "Action failed." throws away the only text that tells the operator what to do
 * differently, and the platform rule is to fail loudly (01_PROJECT_RULES.md §1.5).
 *
 * Handles both shapes a failure arrives in: an `HttpErrorResponse` whose body is the envelope
 * (non-2xx), and the `Error` thrown by {@link unwrap} for a `success: false` body served with 200.
 */
export function apiErrorMessage(error: unknown, fallback: string): string {
  const envelope = (error as { error?: ApiEnvelope<unknown> })?.error;
  const fromBody = envelope?.errors?.[0]?.message ?? envelope?.message;
  if (typeof fromBody === 'string' && fromBody.trim().length > 0) {
    return fromBody;
  }

  const fromThrown = (error as Error)?.message;
  return typeof fromThrown === 'string' && fromThrown.trim().length > 0 ? fromThrown : fallback;
}

/**
 * Unwraps the standard envelope. A `success: false` body (which can arrive with a 200) is a
 * failure, not data — surface it as an error so callers don't silently render an empty payload.
 */
function unwrap<T>(envelope: ApiEnvelope<T>): T {
  if (envelope && envelope.success === false) {
    const first = envelope.errors?.[0]?.message;
    throw new Error(first ?? envelope.message ?? 'Request failed.');
  }
  return envelope.data;
}
