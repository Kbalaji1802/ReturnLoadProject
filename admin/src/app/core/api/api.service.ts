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
      .pipe(map((r) => r.data));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<ApiEnvelope<T>>(`${this.baseUrl}/${path}`, body)
      .pipe(map((r) => r.data));
  }
}
