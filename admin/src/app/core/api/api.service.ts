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
